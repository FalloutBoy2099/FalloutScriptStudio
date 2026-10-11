namespace FalloutScriptStudio.Core.Tests.Native
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Core.Parsing.Interop;
	using Xunit;

	/// <summary>
	/// Runs the real <c>parser.dll</c> (inside the real parser host process) on small scripts. Skipped when the tools are absent.
	/// </summary>
	public sealed class NativeParserIntegrationTests : IClassFixture<NativeParserFixture>
	{
		private readonly NativeParserFixture _fixture;

		public NativeParserIntegrationTests(NativeParserFixture fixture)
		{
			this._fixture = fixture;
		}

		private static ScriptProcedure Procedure(NativeParseResult result, String name)
		{
			return result.Procedures.Single(p => String.Equals(p.Name, name, StringComparison.Ordinal));
		}

		private static ScriptVariable GlobalVariable(NativeParseResult result, String name)
		{
			return result.Variables.Single(v => String.Equals(v.Name, name, StringComparison.Ordinal));
		}

		private async Task<(NativeParseResult Result, String Script, String Header)> ParseBasicAsync()
		{
			String header = this._fixture.Write(Path.Combine("scripts", "lib.h"), SslSamples.LibraryHeader);
			NativeParseResult result = await this._fixture.ParseAsync("basic.ssl", SslSamples.Basic);
			String script = Path.Combine(this._fixture.ScriptsDirectory, "basic.ssl");
			return (result, script, header);
		}

		[RealToolFact(RealTools.Parser)]
		public async Task Parse_ValidScript_ReportsProceduresWithLinesAndArguments()
		{
			(NativeParseResult result, String script, String header) = await this.ParseBasicAsync();

			Assert.Equal(NativeParseStatus.Completed, result.Status);
			Assert.True(result.Succeeded, result.Log);
			Assert.True(result.HasData);
			Assert.Equal(0, result.ExitCode);
			Assert.Equal(String.Empty, result.Log);
			Assert.Equal(new[] { "add_values", "lib_helper", "start", "timed_proc" }, result.Procedures.Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray());

			ScriptProcedure start = Procedure(result, "start");
			Assert.Equal(0, start.ArgumentCount);
			Assert.Equal(9, start.DeclaredLine);
			Assert.Equal(13, start.DefinedLine);
			Assert.Equal(13, start.StartLine);
			Assert.Equal(19, start.EndLine);
			Assert.True(start.HasForwardDeclaration);
			Assert.Equal(script, start.DeclaredFile, ignoreCase: true);
			Assert.Equal(script, start.StartFile, ignoreCase: true);
			Assert.Equal("basic.ssl", start.FileName);
			Assert.True(start.IsStandard);

			ScriptProcedure add = Procedure(result, "add_values");
			Assert.Equal(2, add.ArgumentCount);
			Assert.Equal(10, add.DeclaredLine);
			Assert.Equal(21, add.StartLine);
			Assert.Equal(23, add.EndLine);
			Assert.Equal(new[] { "a", "b" }, add.Variables.Select(v => v.Name).ToArray());

			ScriptProcedure timed = Procedure(result, "timed_proc");
			Assert.Equal(11, timed.DeclaredLine);
			Assert.Equal(25, timed.StartLine);
			Assert.Equal(27, timed.EndLine);
			Assert.Empty(timed.Variables);

			// A procedure defined in an included header keeps the location in that header.
			ScriptProcedure helper = Procedure(result, "lib_helper");
			Assert.Equal(1, helper.ArgumentCount);
			Assert.Equal(header, helper.DeclaredFile, ignoreCase: true);
			Assert.Equal(header, helper.StartFile, ignoreCase: true);
			Assert.Equal("lib.h", helper.FileName);
			Assert.Equal(2, helper.StartLine);
			Assert.Equal(4, helper.EndLine);
			Assert.True(helper.IsDefinedIn(header));
			Assert.False(helper.IsDefinedIn(script));
		}

		[RealToolFact(RealTools.Parser)]
		public async Task Parse_ValidScript_ReportsReferencesOfProceduresAndVariables()
		{
			(NativeParseResult result, String script, String _) = await this.ParseBasicAsync();

			Assert.Equal(new[] { 16 }, Procedure(result, "add_values").References.Select(r => r.Line).ToArray());
			Assert.All(Procedure(result, "add_values").References, r => Assert.Equal(script, r.File, ignoreCase: true));
			Assert.Equal(new[] { 22 }, Procedure(result, "lib_helper").References.Select(r => r.Line).ToArray());
			Assert.Empty(Procedure(result, "start").References);

			ScriptVariable counter = GlobalVariable(result, "g_counter");
			Assert.Equal(new[] { 16, 17, 22, 26, 26 }, counter.References.Select(r => r.Line).OrderBy(l => l).ToArray());
			ScriptVariable one = Procedure(result, "start").Variables.Single(v => v.Name == "local_one");
			Assert.Equal(new[] { 16 }, one.References.Select(r => r.Line).ToArray());
		}

		[RealToolFact(RealTools.Parser)]
		public async Task Parse_ValidScript_ReportsGlobalVariablesWithInitialValues()
		{
			(NativeParseResult result, String script, String _) = await this.ParseBasicAsync();

			Assert.Equal(new[] { "g_counter", "g_name", "g_ratio", "g_plain", "g_exported" }, result.Variables.Select(v => v.Name).ToArray());

			ScriptVariable counter = GlobalVariable(result, "g_counter");
			Assert.Equal(VariableScope.Global, counter.Scope);
			Assert.Equal("5", counter.InitialValue);
			Assert.Equal(ValueKind.Int, counter.InitialValueKind);
			Assert.Equal(3, counter.DeclaredLine);
			Assert.Equal(script, counter.DeclaredFile, ignoreCase: true);
			Assert.Equal("basic.ssl", counter.FileName);
			Assert.Equal(0, counter.ArrayLength);

			ScriptVariable name = GlobalVariable(result, "g_name");
			Assert.Equal("\"hello\"", name.InitialValue);
			Assert.Equal(ValueKind.String, name.InitialValueKind);

			ScriptVariable ratio = GlobalVariable(result, "g_ratio");
			Assert.Equal("1.5", ratio.InitialValue);
			Assert.Equal(ValueKind.Float, ratio.InitialValueKind);

			ScriptVariable plain = GlobalVariable(result, "g_plain");
			Assert.Null(plain.InitialValue);
			Assert.Equal(ValueKind.None, plain.InitialValueKind);

			Assert.Equal(VariableScope.Export, GlobalVariable(result, "g_exported").Scope);
			Assert.Equal(7, GlobalVariable(result, "g_exported").DeclaredLine);
		}

		[RealToolFact(RealTools.Parser)]
		public async Task Parse_ValidScript_ReportsLocalVariablesAndArguments()
		{
			(NativeParseResult result, String _, String _) = await this.ParseBasicAsync();

			ScriptProcedure start = Procedure(result, "start");
			Assert.Equal(new[] { "local_one", "local_two" }, start.Variables.Select(v => v.Name).ToArray());
			ScriptVariable one = start.Variables[0];
			Assert.Equal(VariableScope.Local, one.Scope);
			Assert.Equal("10", one.InitialValue);
			Assert.Equal(14, one.DeclaredLine);
			Assert.False(one.IsArgument);
			Assert.Null(start.Variables[1].InitialValue);

			ScriptProcedure add = Procedure(result, "add_values");
			Assert.All(add.Variables, v => Assert.True(v.IsArgument));
			Assert.All(add.Variables, v => Assert.Equal(21, v.ArgumentDeclaredLine));
			Assert.Equal("add_values (a, b)", add.ShortDescription);
		}

		[RealToolFact(RealTools.Parser)]
		public async Task Parse_SyntaxError_ReportsStatus1WithTheErrorInTheLog()
		{
			NativeParseResult result = await this._fixture.ParseAsync("syntax.ssl", SslSamples.SyntaxError);

			Assert.Equal(NativeParseStatus.Completed, result.Status);
			Assert.Equal(1, result.ExitCode);
			Assert.False(result.Succeeded);
			Assert.False(result.HasData);
			Assert.Empty(result.Procedures);
			String script = Path.Combine(this._fixture.ScriptsDirectory, "syntax.ssl");
			Assert.Contains("[Error] <" + script + ">:3:7: Expected ';' at end of variable declaration.", result.Log, StringComparison.OrdinalIgnoreCase);
		}

		[RealToolFact(RealTools.Parser)]
		public async Task Parse_SemanticError_ReportsStatusMinus1ButKeepsTheData()
		{
			NativeParseResult result = await this._fixture.ParseAsync("semantic.ssl", SslSamples.SemanticError);

			Assert.Equal(NativeParseStatus.Completed, result.Status);
			Assert.Equal(-1, result.ExitCode);
			Assert.False(result.Succeeded);
			Assert.True(result.HasData);
			Assert.Equal("start", Assert.Single(result.Procedures).Name);
			Assert.Equal("a", Assert.Single(Assert.Single(result.Procedures).Variables).Name);
			Assert.Contains("[Error] <Semantic> <", result.Log, StringComparison.Ordinal);
			Assert.Contains(":3:10: Undefined symbol b in factor", result.Log, StringComparison.Ordinal);
		}

		[RealToolFact(RealTools.Parser)]
		public async Task Parse_MissingInclude_ReportsStatus2WithTheFileNameInTheLog()
		{
			NativeParseResult result = await this._fixture.ParseAsync("noinclude.ssl", "#include \"does_not_exist.h\"\r\nprocedure start begin\r\nend\r\n");

			Assert.Equal(2, result.ExitCode);
			Assert.False(result.HasData);
			Assert.Contains("does_not_exist.h", result.Log, StringComparison.Ordinal);
			Assert.Contains("preprocessor", result.Log, StringComparison.OrdinalIgnoreCase);
		}

		[RealToolFact(RealTools.Parser)]
		public async Task Parse_IncludeFoundThroughTheHeadersFolder_Works()
		{
			this._fixture.Write(Path.Combine("headers", "shared", "common.h"), "#define COMMON_VALUE 3\r\nprocedure common_proc begin\r\n   return COMMON_VALUE;\r\nend\r\n");
			ParserOptions options = this._fixture.CreateOptions(headersDirectory: Path.Combine(this._fixture.HeadersDirectory, "shared"));

			NativeParseResult result = await this._fixture.ParseAsync("viaheaders.ssl", "#include \"common.h\"\r\nprocedure start begin\r\n   variable x := common_proc;\r\nend\r\n", options);

			Assert.True(result.Succeeded, result.Log);
			ScriptProcedure common = Procedure(result, "common_proc");
			Assert.Equal(Path.Combine(this._fixture.HeadersDirectory, "shared", "common.h"), common.StartFile, ignoreCase: true);
			Assert.Equal(2, common.StartLine);
		}

		[RealToolFact(RealTools.Parser)]
		public async Task Parse_NestedIncludes_ReportTheFileOfEachProcedure()
		{
			this._fixture.Write(Path.Combine("scripts", "nested", "level2.h"), "procedure deep_proc begin\r\nend\r\n");
			this._fixture.Write(Path.Combine("scripts", "nested", "level1.h"), "#include \"level2.h\"\r\nprocedure mid_proc begin\r\n   call deep_proc;\r\nend\r\n");

			NativeParseResult result = await this._fixture.ParseAsync("nesting.ssl", "#include \"nested/level1.h\"\r\nprocedure start begin\r\n   call mid_proc;\r\nend\r\n");

			Assert.True(result.Succeeded, result.Log);
			Assert.Equal("level2.h", Procedure(result, "deep_proc").FileName);
			Assert.Equal("level1.h", Procedure(result, "mid_proc").FileName);
			Assert.Equal("nesting.ssl", Procedure(result, "start").FileName);
			Assert.Equal(new[] { 3 }, Procedure(result, "deep_proc").References.Select(r => r.Line).ToArray());
			Assert.EndsWith("level1.h", Procedure(result, "deep_proc").References.Single().File, StringComparison.OrdinalIgnoreCase);
		}

		[RealToolFact(RealTools.Parser)]
		public async Task Parse_PreprocessorDefinitions_SelectConditionalCode()
		{
			const String Script = "#ifdef FEATURE_ON\r\nprocedure feature_proc begin\r\nend\r\n#endif\r\nprocedure start begin\r\nend\r\n";

			NativeParseResult without = await this._fixture.ParseAsync("conditional.ssl", Script);
			NativeParseResult with = await this._fixture.ParseAsync("conditional.ssl", Script, this._fixture.CreateOptions(preprocessorDefinitions: "FEATURE_ON"));

			Assert.DoesNotContain(without.Procedures, p => p.Name == "feature_proc");
			Assert.Contains(with.Procedures, p => p.Name == "feature_proc");
			Assert.Equal(2, Procedure(with, "feature_proc").StartLine);
		}

		[RealToolFact(RealTools.Parser)]
		public async Task Parse_ScriptInAFolderWithNonAsciiCharacters_Works()
		{
			String path = this._fixture.Write(Path.Combine("scripts", "café été", "unicode.ssl"), "// café\r\nprocedure start begin\r\nend\r\n");

			NativeParseResult result = await this._fixture.ParseFileAsync(path, "// café\r\nprocedure start begin\r\nend\r\n");

			Assert.True(result.Succeeded, result.Log + result.Message);
			Assert.Equal(path, Procedure(result, "start").StartFile, ignoreCase: true);
		}

		[RealToolFact(RealTools.Parser)]
		public async Task Parse_ManyScriptsOneAfterAnother_ReusesTheHostWithoutLeakingState()
		{
			NativeParseResult first = await this._fixture.ParseAsync("seq1.ssl", "variable a_one;\r\nprocedure first_proc begin\r\nend\r\n");
			NativeParseResult second = await this._fixture.ParseAsync("seq2.ssl", "variable b_two;\r\nprocedure second_proc begin\r\nend\r\n");
			NativeParseResult broken = await this._fixture.ParseAsync("seq3.ssl", SslSamples.SyntaxError);
			NativeParseResult third = await this._fixture.ParseAsync("seq4.ssl", "procedure third_proc begin\r\nend\r\n");

			Assert.Equal("first_proc", Assert.Single(first.Procedures).Name);
			Assert.Equal("a_one", Assert.Single(first.Variables).Name);
			Assert.Equal("second_proc", Assert.Single(second.Procedures).Name);
			Assert.Equal("b_two", Assert.Single(second.Variables).Name);
			Assert.Equal(1, broken.ExitCode);
			Assert.Equal("third_proc", Assert.Single(third.Procedures).Name);
			Assert.Empty(third.Variables);
			Assert.Equal(String.Empty, third.Log);
		}

		[RealToolFact(RealTools.Parser)]
		public async Task Parse_ScriptWithoutProcedures_ReturnsEmptyData()
		{
			NativeParseResult result = await this._fixture.ParseAsync("empty.ssl", String.Empty);

			Assert.True(result.Succeeded);
			Assert.Empty(result.Procedures);
			Assert.Empty(result.Variables);
		}

		[RealToolFact(RealTools.Parser)]
		public async Task Parse_ProcedureModifiers_AreReportedAsFlags()
		{
			NativeParseResult result = await this._fixture.ParseAsync(
				"flags.ssl",
				SslSamples.Join(
					"import procedure imported_proc;",
					"export procedure exported_proc;",
					"pure procedure pure_proc;",
					"critical procedure critical_proc;",
					"import variable imported_var;",
					"export variable exported_var;",
					"procedure start begin",
					"end"));

			Assert.True(result.HasData, result.Log + result.Message);
			Assert.Equal(ProcedureFlags.Import, Procedure(result, "imported_proc").Flags);
			Assert.True(Procedure(result, "imported_proc").IsImported);
			Assert.Equal(ProcedureFlags.Export, Procedure(result, "exported_proc").Flags);
			Assert.True(Procedure(result, "exported_proc").IsExported);
			Assert.Equal(ProcedureFlags.Pure, Procedure(result, "pure_proc").Flags);
			Assert.Equal(ProcedureFlags.Critical, Procedure(result, "critical_proc").Flags);
			Assert.Equal(ProcedureFlags.None, Procedure(result, "start").Flags);

			// A procedure that is only declared has a declaration line but no body.
			ScriptProcedure imported = Procedure(result, "imported_proc");
			Assert.Equal(1, imported.DeclaredLine);
			Assert.Equal(-1, imported.StartLine);
			Assert.Equal(-1, imported.EndLine);

			Assert.Equal(VariableScope.Import, GlobalVariable(result, "imported_var").Scope);
			Assert.Equal(VariableScope.Export, GlobalVariable(result, "exported_var").Scope);
		}

		[RealToolFact(RealTools.Parser)]
		public async Task Parse_ScriptThatCrashesTheNativeLibrary_ReportsFailedAndTheNextParseWorks()
		{
			// "variable name[size]" makes the shipped parser.dll write through a null pointer (access violation).
			// In-process this would have terminated the editor; in the host only this request is lost.
			NativeParseResult crashed = await this._fixture.ParseAsync("crash.ssl", "variable arr[10];\r\nprocedure start begin\r\nend\r\n");
			NativeParseResult after = await this._fixture.ParseAsync("after_crash.ssl", "procedure start begin\r\nend\r\n");

			Assert.Equal(NativeParseStatus.Failed, crashed.Status);
			Assert.False(crashed.HasData);
			Assert.Contains("stopped unexpectedly", crashed.Message, StringComparison.Ordinal);
			Assert.Contains("0xC0000005", crashed.Message, StringComparison.Ordinal);
			Assert.True(after.Succeeded, after.Message + after.Log);
			Assert.Equal("start", Assert.Single(after.Procedures).Name);
		}

		[RealToolFact(RealTools.Parser)]
		public async Task Parse_RealLogs_AreUnderstoodByTheParserLogParser()
		{
			var logParser = new ParserLogParser();
			NativeParseResult syntax = await this._fixture.ParseAsync("log_syntax.ssl", SslSamples.SyntaxError);
			NativeParseResult semantic = await this._fixture.ParseAsync("log_semantic.ssl", SslSamples.SemanticError);
			NativeParseResult include = await this._fixture.ParseAsync("log_include.ssl", "#include \"missing_header.h\"\r\nprocedure start begin\r\nend\r\n");

			BuildError syntaxError = Assert.Single(logParser.Parse(syntax.Log, "log_syntax.ssl", true, DateTime.Now).Errors);
			BuildError semanticError = Assert.Single(logParser.Parse(semantic.Log, "log_semantic.ssl", true, DateTime.Now).Errors);
			BuildError includeError = Assert.Single(logParser.Parse(include.Log, "log_include.ssl", true, DateTime.Now).Errors);

			Assert.Equal(new BuildError(BuildErrorType.Error, "Expected ';' at end of variable declaration.", Path.Combine(this._fixture.ScriptsDirectory, "log_syntax.ssl"), 3, 7), syntaxError);
			Assert.Equal(new BuildError(BuildErrorType.Error, "Undefined symbol b in factor", Path.Combine(this._fixture.ScriptsDirectory, "log_semantic.ssl"), 3, 10), semanticError);
			Assert.Equal(BuildErrorType.Error, includeError.Type);
			Assert.Equal(Path.Combine(this._fixture.ScriptsDirectory, "log_include.ssl"), includeError.FileName);
			Assert.Equal(1, includeError.Line);
			Assert.Equal(-1, includeError.Column);
			Assert.Equal("Can't open include file \"missing_header.h\"", includeError.Message);
		}

		[RealToolTheory(RealTools.Parser)]
		[InlineData("procedure update_box(variable w);procedure update_box(variable w);", 0xC0000005)]
		[InlineData("procedure y(variable i variable y)begin n false", 0xC0000409)]
		public async Task Parse_HalfTypedScriptsThatCrashTheNativeLibrary_EndQuicklyAndSilentlyAndTheHostRecovers(String crashingText, UInt32 expectedExitCode)
		{
			// Two real crashes found by typing/deleting characters in real scripts: a procedure declared twice (access violation) and a
			// procedure header with a missing comma (stack buffer overrun). The host must be gone within moments, with no crash box.
			var watch = System.Diagnostics.Stopwatch.StartNew();
			NativeParseResult crashed = await this._fixture.ParseAsync("typing.ssl", crashingText);
			TimeSpan elapsed = watch.Elapsed;
			NativeParseResult recovered = await this._fixture.ParseAsync("typing.ssl", "procedure start begin\r\nend\r\n");

			Assert.Equal(NativeParseStatus.Failed, crashed.Status);
			Assert.Contains("0x" + expectedExitCode.ToString("X8", System.Globalization.CultureInfo.InvariantCulture), crashed.Message, StringComparison.Ordinal);
			Assert.True(elapsed < TimeSpan.FromSeconds(10), "The crash took " + elapsed);
			Assert.True(recovered.Succeeded, recovered.Message + recovered.Log);
		}
	}
}
