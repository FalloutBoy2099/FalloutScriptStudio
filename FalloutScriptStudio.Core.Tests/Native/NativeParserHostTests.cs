namespace FalloutScriptStudio.Core.Tests.Native
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Core.Parsing.Interop;
	using FalloutScriptStudio.Core.Parsing.Interop.Host;
	using Xunit;

	/// <summary>The request/response loop of the parser host, driven through in-memory streams.</summary>
	public sealed class NativeParserHostTests
	{
		private static String Line(HostRequest request)
		{
			return HostProtocol.SerializeRequest(request) + "\n";
		}

		private static HostRequest Parse(Int64 id, String text = "procedure start begin end", String file = @"C:\s\a.ssl")
		{
			return new HostRequest
			{
				Id = id,
				ScriptText = text,
				FilePath = file,
				ParserLibraryPath = @"C:\r\parser.dll",
				PreprocessorDefinitions = "FOO=1",
				HeadersDirectory = @"C:\h",
				BackwardCompatibilityMode = 2,
				ScratchDirectory = @"C:\scr",
				ScratchFileName = "p.ssl",
				NativeLogPath = @"C:\log.txt",
			};
		}

		private static async Task<(List<HostResponse> Responses, String Raw, Int32 Served)> RunAsync(INativeParser engine, String input)
		{
			var output = new StringWriter();
			Int32 served = await new NativeParserHost(engine).RunAsync(new StringReader(input), output, CancellationToken.None);
			String raw = output.ToString();
			var responses = new List<HostResponse>();
			foreach (String line in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries))
			{
				Assert.True(HostProtocol.TryReadResponse(line, out HostResponse? response), "Not a response line: " + line);
				responses.Add(response!);
			}

			return (responses, raw, served);
		}

		private sealed class RecordingEngine : INativeParser
		{
			public List<(String Text, String File, ParserOptions Options)> Calls { get; } = [];

			public Func<NativeParseResult> Result { get; set; } = () => new NativeParseResult(NativeParseStatus.Completed) { ExitCode = 0, Log = "ok" };

			public Task<NativeParseResult> ParseAsync(String scriptText, String filePath, ParserOptions options, CancellationToken cancellationToken)
			{
				this.Calls.Add((scriptText, filePath, options));
				return Task.FromResult(this.Result());
			}
		}

		[Fact]
		public async Task Run_ParseRequest_PassesTheScriptAndTheOptionsToTheEngineAndAnswers()
		{
			var engine = new RecordingEngine();

			(List<HostResponse> responses, String raw, Int32 served) = await RunAsync(engine, Line(Parse(7, "text \u00e9")));

			(String text, String file, ParserOptions options) = Assert.Single(engine.Calls);
			Assert.Equal("text \u00e9", text);
			Assert.Equal(@"C:\s\a.ssl", file);
			Assert.Equal(@"C:\r\parser.dll", options.NativeParserPath);
			Assert.Equal("FOO=1", options.PreprocessorDefinitions);
			Assert.Equal(@"C:\h", options.HeadersDirectory);
			Assert.Equal(2, options.BackwardCompatibilityMode);
			Assert.Equal(@"C:\scr", options.ScratchDirectory);
			Assert.Equal("p.ssl", options.ScratchFileName);
			Assert.Equal(@"C:\log.txt", options.NativeLogPath);
			Assert.True(options.EnableNativeParser);

			HostResponse response = Assert.Single(responses);
			Assert.Equal(7, response.Id);
			Assert.Equal("Completed", response.Status);
			Assert.Equal(0, response.ExitCode);
			Assert.Equal("ok", response.Log);
			Assert.Equal(1, served);
			Assert.StartsWith(HostProtocol.ResponsePrefix, raw, StringComparison.Ordinal);
			Assert.Single(raw.Split('\n', StringSplitOptions.RemoveEmptyEntries));
		}

		[Fact]
		public async Task Run_EmptyScratchFileName_FallsBackToTheDefault()
		{
			var engine = new RecordingEngine();
			HostRequest request = Parse(1);
			request.ScratchFileName = " ";

			await RunAsync(engine, Line(request));

			Assert.Equal(ParserOptions.DefaultScratchFileName, engine.Calls[0].Options.ScratchFileName);
		}

		[Fact]
		public async Task Run_SeveralRequests_AreAnsweredInOrderUntilTheInputEnds()
		{
			var engine = new RecordingEngine();

			(List<HostResponse> responses, String _, Int32 served) = await RunAsync(engine, Line(Parse(1)) + "\r\n\n" + Line(Parse(2)) + Line(Parse(3)));

			Assert.Equal(new Int64[] { 1, 2, 3 }, responses.Select(r => r.Id).ToArray());
			Assert.Equal(3, served);
		}

		[Fact]
		public async Task Run_Ping_AnswersWithoutTouchingTheEngine()
		{
			var engine = new RecordingEngine();

			(List<HostResponse> responses, String _, Int32 served) = await RunAsync(engine, Line(new HostRequest { Id = 4, Command = HostProtocol.PingCommand }));

			Assert.Empty(engine.Calls);
			Assert.Equal(0, served);
			Assert.Equal((4L, "Completed", 0), (responses[0].Id, responses[0].Status, responses[0].ExitCode));
		}

		[Fact]
		public async Task Run_ExitCommand_AnswersAndStopsReading()
		{
			var engine = new RecordingEngine();

			(List<HostResponse> responses, String _, Int32 served) = await RunAsync(engine, Line(Parse(1)) + Line(new HostRequest { Id = 2, Command = HostProtocol.ExitCommand }) + Line(Parse(3)));

			Assert.Equal(new Int64[] { 1, 2 }, responses.Select(r => r.Id).ToArray());
			Assert.Equal(1, served);
			Assert.Single(engine.Calls);
		}

		[Fact]
		public async Task Run_MalformedLine_IsAnsweredWithAFailureAndTheLoopContinues()
		{
			var engine = new RecordingEngine();

			(List<HostResponse> responses, String _, Int32 served) = await RunAsync(engine, "this is not json\n" + "{\"version\":1,\"id\":\n" + Line(Parse(5)));

			Assert.Equal(3, responses.Count);
			Assert.Equal("Failed", responses[0].Status);
			Assert.Contains("not valid JSON", responses[0].Message, StringComparison.Ordinal);
			Assert.Equal(0, responses[0].Id);
			Assert.Equal("Failed", responses[1].Status);
			Assert.Equal((5L, "Completed"), (responses[2].Id, responses[2].Status));
			Assert.Equal(1, served);
		}

		[Fact]
		public async Task Run_UnknownCommandOrVersion_IsAnsweredWithAFailure()
		{
			var engine = new RecordingEngine();

			(List<HostResponse> responses, String _, Int32 _) = await RunAsync(
				engine,
				Line(new HostRequest { Id = 1, Command = "format-disk" }) + Line(new HostRequest { Id = 2, Version = 42 }));

			Assert.All(responses, r => Assert.Equal("Failed", r.Status));
			Assert.Contains("Unknown command 'format-disk'", responses[0].Message, StringComparison.Ordinal);
			Assert.Contains("Unsupported protocol version 42", responses[1].Message, StringComparison.Ordinal);
			Assert.Equal(new Int64[] { 1, 2 }, responses.Select(r => r.Id).ToArray());
			Assert.Empty(engine.Calls);
		}

		[Fact]
		public async Task Run_EngineThrows_IsAnsweredWithAFailureAndTheLoopContinues()
		{
			var engine = new RecordingEngine();
			Int32 call = 0;
			engine.Result = () => call++ == 0 ? throw new InvalidOperationException("kaboom") : new NativeParseResult(NativeParseStatus.Completed) { ExitCode = 0 };

			(List<HostResponse> responses, String _, Int32 _) = await RunAsync(engine, Line(Parse(1)) + Line(Parse(2)));

			Assert.Equal("Failed", responses[0].Status);
			Assert.Contains("InvalidOperationException: kaboom", responses[0].Message, StringComparison.Ordinal);
			Assert.Equal("Completed", responses[1].Status);
		}

		[Fact]
		public async Task Run_EngineResultWithData_IsSerializedCompletely()
		{
			var engine = new RecordingEngine();
			var procedure = new ScriptProcedure("start") { Flags = ProcedureFlags.Pure, StartLine = 3, EndLine = 8, References = new[] { new SourceReference(@"C:\s\a.ssl", 2) } };
			var variable = new ScriptVariable("v", VariableScope.Export) { DeclaredLine = 1, InitialValue = "5", InitialValueKind = ValueKind.Int };
			engine.Result = () => new NativeParseResult(NativeParseStatus.Completed) { ExitCode = -1, Procedures = new[] { procedure }, Variables = new[] { variable }, Message = "m" };

			(List<HostResponse> responses, String _, Int32 _) = await RunAsync(engine, Line(Parse(1)));

			NativeParseResult decoded = HostMapper.ToResult(responses[0]);
			Assert.Equal(-1, decoded.ExitCode);
			Assert.Equal("m", decoded.Message);
			ScriptProcedure decodedProcedure = Assert.Single(decoded.Procedures);
			Assert.Equal((ProcedureFlags.Pure, 3, 8), (decodedProcedure.Flags, decodedProcedure.StartLine, decodedProcedure.EndLine));
			Assert.Equal(new SourceReference(@"C:\s\a.ssl", 2), Assert.Single(decodedProcedure.References));
			Assert.Equal(("v", VariableScope.Export, "5"), (Assert.Single(decoded.Variables).Name, decoded.Variables[0].Scope, decoded.Variables[0].InitialValue));
		}

		[Fact]
		public async Task Run_CancelledToken_StopsBeforeReadingAnything()
		{
			using var cts = new CancellationTokenSource();
			cts.Cancel();
			var engine = new RecordingEngine();

			Int32 served = await new NativeParserHost(engine).RunAsync(new StringReader(Line(Parse(1))), new StringWriter(), cts.Token);

			Assert.Equal(0, served);
			Assert.Empty(engine.Calls);
		}

		[Fact]
		public async Task Run_RealInProcessParserWithoutALibrary_ReportsUnavailableThroughTheProtocol()
		{
			using var engine = new NativeParser();

			(List<HostResponse> responses, String _, Int32 _) = await RunAsync(engine, Line(Parse(1)));

			Assert.Equal("Unavailable", responses[0].Status);
			Assert.Contains("was not found", responses[0].Message, StringComparison.Ordinal);
		}
	}
}