namespace FalloutScriptStudio.Core.Tests.Native
{
	using System;
	using System.Text.Json;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Core.Parsing.Interop;
	using FalloutScriptStudio.Core.Parsing.Interop.Host;
	using Xunit;

	/// <summary>The wire format of the parser host protocol.</summary>
	public sealed class HostProtocolTests
	{
		[Fact]
		public void Request_RoundTripsOnASingleLine()
		{
			var request = HostMapper.ToRequest(
				12,
				"line1\r\nline2 \"quoted\" \\ \u00e9 \u4e2d\u6587\t",
				@"C:\s\a.ssl",
				new ParserOptions { NativeParserPath = @"C:\r\parser.dll", PreprocessorDefinitions = "A=1;B", HeadersDirectory = @"C:\h", BackwardCompatibilityMode = 1, ScratchDirectory = @"C:\t", ScratchFileName = "x.ssl", NativeLogPath = @"C:\l.txt" });

			String line = HostProtocol.SerializeRequest(request);
			HostRequest decoded = HostProtocol.DeserializeRequest(line);

			Assert.DoesNotContain('\n', line);
			Assert.DoesNotContain('\r', line);
			Assert.Equal(request.ScriptText, decoded.ScriptText);
			Assert.Equal(12, decoded.Id);
			Assert.Equal(HostProtocol.ParseCommand, decoded.Command);
			Assert.Equal(@"C:\r\parser.dll", decoded.ParserLibraryPath);
			Assert.Equal("A=1;B", decoded.PreprocessorDefinitions);
			Assert.Equal(1, decoded.BackwardCompatibilityMode);
			Assert.Equal("x.ssl", decoded.ScratchFileName);
		}

		[Fact]
		public void Request_UsesCamelCaseNames()
		{
			String line = HostProtocol.SerializeRequest(new HostRequest { Id = 3 });

			Assert.Contains("\"scriptText\":", line, StringComparison.Ordinal);
			Assert.Contains("\"id\":3", line, StringComparison.Ordinal);
			Assert.Contains("\"version\":1", line, StringComparison.Ordinal);
		}

		[Fact]
		public void Response_IsPrefixedAndSurvivesNoiseInFrontOfIt()
		{
			String line = HostProtocol.SerializeResponse(new HostResponse { Id = 9, Status = "Completed", ExitCode = 0, Log = "a\r\nb" });

			Assert.StartsWith(HostProtocol.ResponsePrefix, line, StringComparison.Ordinal);
			Assert.True(HostProtocol.TryReadResponse("some native output" + line, out HostResponse? response));
			Assert.Equal(9, response!.Id);
			Assert.Equal("a\r\nb", response.Log);
		}

		[Theory]
		[InlineData("")]
		[InlineData("hello")]
		[InlineData("{\"id\":1,\"status\":\"Completed\"}")]
		public void Response_LineWithoutThePrefix_IsNoise(String line)
		{
			Assert.False(HostProtocol.TryReadResponse(line, out HostResponse? response));
			Assert.Null(response);
		}

		[Theory]
		[InlineData("@@FSS-PARSER@@")]
		[InlineData("@@FSS-PARSER@@{")]
		[InlineData("@@FSS-PARSER@@[1,2]")]
		[InlineData("@@FSS-PARSER@@null")]
		public void Response_PrefixWithBrokenJson_Throws(String line)
		{
			Assert.ThrowsAny<JsonException>(() => HostProtocol.TryReadResponse(line, out _));
		}

		[Fact]
		public void Mapper_RoundTripsEveryField()
		{
			var argument = new ScriptVariable("arg", VariableScope.Local) { DeclaredFile = @"C:\a.ssl", FileName = "a.ssl", DeclaredLine = 4, ArgumentDeclaredLine = 3, InitialValue = "\"s\"", InitialValueKind = ValueKind.String, ArrayLength = 2, References = new[] { new SourceReference(@"C:\a.ssl", 7), new SourceReference(null, -1) } };
			var procedure = new ScriptProcedure("Proc") { Flags = ProcedureFlags.Import | ProcedureFlags.Timed, DeclaredFile = @"C:\a.ssl", StartFile = @"C:\b.h", FileName = "a.ssl", DeclaredLine = 1, DefinedLine = 2, StartLine = 3, EndLine = 4, ArgumentCount = 5, TimeValue = 6, Variables = new[] { argument }, References = new[] { new SourceReference(@"C:\c.h", 8) } };
			var global = new ScriptVariable("G", VariableScope.Import) { DeclaredLine = 9 };
			var original = new NativeParseResult(NativeParseStatus.Completed) { ExitCode = -1, Message = "msg", Log = "log", Procedures = new[] { procedure }, Variables = new[] { global } };

			NativeParseResult decoded = HostMapper.ToResult(HostProtocol.TryReadResponse(HostProtocol.SerializeResponse(HostMapper.ToResponse(1, original)), out HostResponse? response) ? response! : throw new InvalidOperationException());

			Assert.Equal(original.Status, decoded.Status);
			Assert.Equal(original.ExitCode, decoded.ExitCode);
			Assert.Equal(original.Message, decoded.Message);
			Assert.Equal(original.Log, decoded.Log);
			ScriptProcedure p = Assert.Single(decoded.Procedures);
			Assert.Equal(("Proc", "proc", ProcedureFlags.Import | ProcedureFlags.Timed), (p.Name, p.NormalizedName, p.Flags));
			Assert.Equal((@"C:\a.ssl", @"C:\b.h", "a.ssl"), (p.DeclaredFile, p.StartFile, p.FileName));
			Assert.Equal((1, 2, 3, 4, 5, 6), (p.DeclaredLine, p.DefinedLine, p.StartLine, p.EndLine, p.ArgumentCount, p.TimeValue));
			Assert.Equal(new SourceReference(@"C:\c.h", 8), Assert.Single(p.References));
			ScriptVariable a = Assert.Single(p.Variables);
			Assert.Equal(("arg", VariableScope.Local, @"C:\a.ssl", "a.ssl"), (a.Name, a.Scope, a.DeclaredFile, a.FileName));
			Assert.Equal((4, 3, "\"s\"", ValueKind.String, 2), (a.DeclaredLine, a.ArgumentDeclaredLine, a.InitialValue, a.InitialValueKind, a.ArrayLength));
			Assert.Equal(new[] { new SourceReference(@"C:\a.ssl", 7), new SourceReference(null, -1) }, a.References);
			ScriptVariable g = Assert.Single(decoded.Variables);
			Assert.Equal(("G", VariableScope.Import, 9), (g.Name, g.Scope, g.DeclaredLine));
			Assert.Null(g.DeclaredFile);
			Assert.Null(g.InitialValue);
		}

		[Fact]
		public void Mapper_UnknownStatus_Throws()
		{
			Assert.Throws<FormatException>(() => HostMapper.ToResult(new HostResponse { Status = "Nope" }));
			Assert.Throws<FormatException>(() => HostMapper.ToResult(new HostResponse { Status = "42" }));
		}
	}
}