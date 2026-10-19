namespace FalloutScriptStudio.Core.Tests.Parsing
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Core.Parsing.Interop;
	using Xunit;

	public class ScriptParserTests
	{
		private readonly FakeNativeParser _native = new FakeNativeParser();
		private ParserOptions _options = new ParserOptions();

		private ScriptParser CreateParser()
		{
			return new ScriptParser(this._native, () => this._options);
		}

		private static NativeParseResult NativeSuccess(String file, Int32 startLine, Int32 endLine)
		{
			String full = Path.GetFullPath(file);
			var local = new ScriptVariable("who", VariableScope.Local) { ArgumentDeclaredLine = startLine, DeclaredFile = full };
			var procedure = new ScriptProcedure("start")
			{
				DeclaredFile = full,
				StartFile = full,
				DeclaredLine = 5,
				DefinedLine = startLine,
				StartLine = startLine,
				EndLine = endLine,
				ArgumentCount = 1,
				Variables = new[] { local },
				References = new[] { new SourceReference(full, 9) },
			};
			return new NativeParseResult(NativeParseStatus.Completed) { ExitCode = 0, Procedures = new[] { procedure } };
		}

		[Fact]
		public async Task ParseAsync_NativeDisabled_UsesInternalParserAndCollectsMacros()
		{
			this._options = new ParserOptions { EnableNativeParser = false };
			String text = SslText.BasicScript() + "\r\nprocedure lonely;";

			ParseResult result = await this.CreateParser().ParseAsync(new ParseRequest(text) { FilePath = @"C:\s\test.ssl" }, CancellationToken.None);

			Assert.Equal(ParseOutcome.NativeDisabled, result.Outcome);
			Assert.False(result.UsedNativeParser);
			Assert.Empty(this._native.ParsedTexts);
			Assert.True(result.ProgramInfo.IsParsed);
			Assert.False(result.ProgramInfo.HasNativeData);
			Assert.False(result.ProgramInfo.HasParseError);
			Assert.Equal(3, result.ProgramInfo.Procedures.Count);
			Assert.Equal("SCRIPT_TEST", result.ProgramInfo.ScriptMacros["NAME"].Code);

			// With the native parser off, the internal parser messages form the log (legacy intParserPrint).
			Assert.Contains("'lonely'", result.LogText, StringComparison.Ordinal);
			Assert.Single(result.Diagnostics, d => d.Severity == ParseDiagnosticSeverity.Warning);
		}

		[Fact]
		public async Task ParseAsync_NativeSucceeds_UsesNativeData()
		{
			this._native.Result = NativeSuccess(@"C:\s\test.ssl", 8, 10);

			ParseResult result = await this.CreateParser().ParseAsync(
				new ParseRequest(SslText.BasicScript()) { FilePath = @"C:\s\test.ssl" },
				CancellationToken.None);

			Assert.Equal(ParseOutcome.NativeSucceeded, result.Outcome);
			Assert.True(result.UsedNativeParser);
			Assert.Equal(0, result.NativeStatusCode);
			Assert.True(result.ProgramInfo.HasNativeData);
			Assert.False(result.ProgramInfo.HasParseError);
			ScriptProcedure procedure = Assert.Single(result.ProgramInfo.Procedures);
			Assert.Equal("who", procedure.Variables[0].Name);
			Assert.Equal("SCRIPT_TEST", result.ProgramInfo.ScriptMacros["NAME"].Code);
			Assert.Equal(Path.GetFullPath(@"C:\s\test.ssl"), result.ProgramInfo.ScriptFilePath);
		}

		[Fact]
		public async Task ParseAsync_NativeUnavailable_KeepsPreviousDataAndUpdatesLines()
		{
			ScriptParser parser = this.CreateParser();
			this._native.Result = NativeSuccess(@"C:\s\test.ssl", 8, 10);
			ParseResult first = await parser.ParseAsync(new ParseRequest(SslText.BasicScript()) { FilePath = @"C:\s\test.ssl", IsFirstPass = true }, CancellationToken.None);

			// Two lines are inserted and the native parser disappears.
			this._native.Result = new NativeParseResult(NativeParseStatus.Unavailable) { Message = "parser.dll is missing" };
			String edited = "// one\r\n// two\r\n" + SslText.BasicScript();
			ParseResult second = await parser.ParseAsync(
				new ParseRequest(edited) { FilePath = @"C:\s\test.ssl", PreviousInfo = first.ProgramInfo },
				CancellationToken.None);

			Assert.Equal(ParseOutcome.NativeUnavailable, second.Outcome);
			Assert.False(second.HasParseError);
			Assert.True(second.ProgramInfo.HasNativeData);
			ScriptProcedure start = second.ProgramInfo.Procedures.Single(p => p.Name == "start");
			Assert.Equal(10, start.StartLine);
			Assert.Equal("who", start.Variables[0].Name);
			Assert.Single(start.References);
			Assert.Contains(second.ProgramInfo.Procedures, p => p.Name == "Talk_p_proc");
			Assert.Contains("parser.dll is missing", second.LogText, StringComparison.Ordinal);
			Assert.Single(first.ProgramInfo.Procedures);
		}

		[Fact]
		public async Task ParseAsync_NativeReportsErrors_FlagsParseErrorAndKeepsTheLog()
		{
			this._native.Result = new NativeParseResult(NativeParseStatus.Completed) { ExitCode = 1, Log = "test.ssl(3): syntax error\r\n" };

			ParseResult result = await this.CreateParser().ParseAsync(
				new ParseRequest(SslText.BasicScript()) { FilePath = @"C:\s\test.ssl" },
				CancellationToken.None);

			Assert.Equal(ParseOutcome.NativeReportedErrors, result.Outcome);
			Assert.Equal(1, result.NativeStatusCode);
			Assert.True(result.HasParseError);
			Assert.False(result.ProgramInfo.HasNativeData);
			Assert.Equal(2, result.ProgramInfo.Procedures.Count);
			Assert.Equal("test.ssl(3): syntax error\r\n", result.LogText);
		}

		[Fact]
		public async Task ParseAsync_NativeReportsSemanticErrors_UsesTheNativeDataAndFlagsParseError()
		{
			// parse_main returns -1 when only the semantic analysis found errors; the procedures and variables are still valid.
			NativeParseResult semantic = NativeSuccess(@"C:\s\test.ssl", 10, 15);
			this._native.Result = semantic with { ExitCode = -1, Log = @"[Error] <Semantic> <C:\s\test.ssl>:12:3: Undefined symbol x in factor" + "\r\n" };

			ParseResult result = await this.CreateParser().ParseAsync(
				new ParseRequest(SslText.BasicScript()) { FilePath = @"C:\s\test.ssl" },
				CancellationToken.None);

			Assert.Equal(ParseOutcome.NativeReportedErrors, result.Outcome);
			Assert.Equal(-1, result.NativeStatusCode);
			Assert.True(result.HasParseError);
			Assert.True(result.ProgramInfo.HasNativeData);
			ScriptProcedure start = Assert.Single(result.ProgramInfo.Procedures);
			Assert.Equal(10, start.StartLine);
			Assert.Contains("Undefined symbol x", result.LogText, StringComparison.Ordinal);
		}

		[Fact]
		public async Task ParseAsync_NativeFails_FallsBackToInternalParserAndFlagsError()
		{
			this._native.Result = new NativeParseResult(NativeParseStatus.Failed) { Message = "boom" };

			ParseResult result = await this.CreateParser().ParseAsync(
				new ParseRequest(SslText.BasicScript()) { FilePath = @"C:\s\test.ssl" },
				CancellationToken.None);

			Assert.Equal(ParseOutcome.NativeFailed, result.Outcome);
			Assert.True(result.HasParseError);
			Assert.Equal(2, result.ProgramInfo.Procedures.Count);
			Assert.Equal("boom", result.Message);
		}

		[Fact]
		public async Task ParseAsync_UnsavedScript_SkipsNativeParserButCollectsMacros()
		{
			ParseResult result = await this.CreateParser().ParseAsync(new ParseRequest(SslText.BasicScript()), CancellationToken.None);

			Assert.Equal(ParseOutcome.NativeDisabled, result.Outcome);
			Assert.Empty(this._native.ParsedTexts);
			Assert.Equal(2, result.ProgramInfo.Procedures.Count);
			Assert.Null(result.ProgramInfo.ScriptFilePath);
			Assert.True(result.ProgramInfo.ScriptMacros.ContainsKey("NAME"));
		}

		[Fact]
		public async Task ParseAsync_ReadsOptionsOnEveryCall()
		{
			ScriptParser parser = this.CreateParser();
			var request = new ParseRequest(SslText.BasicScript()) { FilePath = @"C:\s\test.ssl" };

			this._options = new ParserOptions { EnableNativeParser = false };
			ParseResult off = await parser.ParseAsync(request, CancellationToken.None);
			this._options = new ParserOptions { EnableNativeParser = true };
			ParseResult on = await parser.ParseAsync(request, CancellationToken.None);

			Assert.Equal(ParseOutcome.NativeDisabled, off.Outcome);
			Assert.Equal(ParseOutcome.NativeUnavailable, on.Outcome);
			Assert.Single(this._native.ParsedTexts);
		}

		[Fact]
		public async Task ParseAsync_PathChange_RebindsPreviousData()
		{
			ScriptParser parser = this.CreateParser();
			this._native.Result = NativeSuccess(@"C:\s\old.ssl", 8, 10);
			ParseResult first = await parser.ParseAsync(new ParseRequest(SslText.BasicScript()) { FilePath = @"C:\s\old.ssl" }, CancellationToken.None);

			this._native.Result = new NativeParseResult(NativeParseStatus.Unavailable);
			ParseResult second = await parser.ParseAsync(
				new ParseRequest(SslText.BasicScript()) { FilePath = @"C:\s\new.ssl", PreviousInfo = first.ProgramInfo },
				CancellationToken.None);

			ScriptProcedure start = second.ProgramInfo.Procedures.Single();
			Assert.Equal(Path.GetFullPath(@"C:\s\new.ssl"), start.DeclaredFile);
			Assert.Equal(Path.GetFullPath(@"C:\s\new.ssl"), start.References[0].File);
			Assert.Equal(Path.GetFullPath(@"C:\s\new.ssl"), second.ProgramInfo.ScriptFilePath);
			Assert.Equal(Path.GetFullPath(@"C:\s\old.ssl"), first.ProgramInfo.Procedures[0].DeclaredFile);
		}

		[Fact]
		public async Task ParseAsync_FirstPass_LoadsGlobalMacros()
		{
			using var headers = new TempDirectory();
			headers.Write("define.h", "#define GLOBAL_ONLY 42");
			this._options = new ParserOptions { EnableNativeParser = false, GlobalMacroDirectory = headers.Path };
			ScriptParser parser = this.CreateParser();

			ParseResult result = await parser.ParseAsync(
				new ParseRequest("procedure a begin\r\nend") { FilePath = @"C:\s\a.ssl", IsFirstPass = true },
				CancellationToken.None);

			Assert.True(parser.GlobalMacros.Contains("GLOBAL_ONLY"));
			Assert.Equal("42", result.ProgramInfo.FindMacro("GLOBAL_ONLY")?.Code);
			Assert.False(result.ProgramInfo.ScriptMacros.ContainsKey("GLOBAL_ONLY"));
		}

		[Fact]
		public async Task ParseAsync_ResolvesIncludesWithTheConfiguredHeadersDirectory()
		{
			using var root = new TempDirectory();
			root.Write(Path.Combine("headers", "define.h"), "#define FROM_HEADERS 1");
			String scriptPath = Path.Combine(root.Path, "scripts", "a.ssl");
			this._options = new ParserOptions { EnableNativeParser = false, HeadersDirectory = Path.Combine(root.Path, "headers") };

			ParseResult result = await this.CreateParser().ParseAsync(
				new ParseRequest("#include \"define.h\"\r\nprocedure a begin\r\nend") { FilePath = scriptPath },
				CancellationToken.None);

			Assert.True(result.ProgramInfo.ScriptMacros.ContainsKey("FROM_HEADERS"));
		}

		[Fact]
		public async Task ParseAsync_CancelledToken_Throws()
		{
			using var cts = new CancellationTokenSource();
			cts.Cancel();

			await Assert.ThrowsAnyAsync<OperationCanceledException>(
				() => this.CreateParser().ParseAsync(new ParseRequest("procedure a begin\r\nend"), cts.Token));
		}
	}
}