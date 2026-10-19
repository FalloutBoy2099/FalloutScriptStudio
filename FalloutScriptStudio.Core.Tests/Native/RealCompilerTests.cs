namespace FalloutScriptStudio.Core.Tests.Native
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.IO;
	using Xunit;

	/// <summary>
	/// Runs the real <c>compile.exe</c> and <c>int2ssl.exe</c> through <see cref="ScriptCompiler"/>. Skipped when the tools are absent,
	/// see <see cref="SslcTools"/>.
	/// </summary>
	public sealed class RealCompilerTests : IDisposable
	{
		private const String HeaderName = "lib.h";

		private static readonly String _okScript = SslSamples.Join(
			"procedure start;",
			"procedure helper(variable a, variable b);",
			"",
			"procedure start begin",
			"   variable x := 1;",
			"   display_msg(\"hi \" + helper(x, 2));",
			"end",
			"",
			"procedure helper(variable a, variable b) begin",
			"   return a + b;",
			"end");

		private static readonly String _syntaxErrorScript = SslSamples.Join(
			"procedure start begin",
			"   variable x := ;",
			"   foo(1;",
			"end");

		private static readonly String _semanticErrorScript = SslSamples.Join(
			"procedure start begin",
			"   variable a := 1;",
			"   a := missing_symbol;",
			"end");

		private readonly RealCompilerWorkspace _workspace = new RealCompilerWorkspace();

		public void Dispose()
		{
			this._workspace.Dispose();
		}

		private Task<CompilationResult> CompileAsync(String script, CompilerOptions? options = null, Boolean shortCircuit = false)
		{
			return this._workspace.Compiler.CompileAsync(new CompilationRequest { ScriptPath = script, Options = options ?? this._workspace.CreateOptions(), ShortCircuit = shortCircuit });
		}

		[RealToolFact(RealTools.Compiler)]
		public async Task Compile_ValidScript_ProducesTheIntFileNextToTheSource()
		{
			String script = this._workspace.Write(@"scripts\ok.ssl", _okScript);

			CompilationResult result = await this.CompileAsync(script);

			Assert.True(result.Success, result.Output);
			String expected = Path.Combine(this._workspace.Scripts, "ok.int");
			Assert.Equal(expected, result.OutputFilePath);
			Assert.True(File.Exists(expected));
			Assert.True(new FileInfo(expected).Length > 0);
			Assert.Contains("Compiling ok.ssl", result.Output, StringComparison.Ordinal);
			Assert.DoesNotContain(result.Errors, e => e.Type == BuildErrorType.Error);
			Assert.Empty(Directory.GetFiles(this._workspace.Scratch));
		}

		[RealToolFact(RealTools.Compiler)]
		public async Task Compile_SyntaxError_ReportsFileLineColumnAndMessage()
		{
			String script = this._workspace.Write(@"scripts\bad.ssl", _syntaxErrorScript);

			CompilationResult result = await this.CompileAsync(script);

			Assert.False(result.Success);
			Assert.Null(result.OutputFilePath);
			Assert.False(File.Exists(Path.Combine(this._workspace.Scripts, "bad.int")));
			Assert.Contains("THERE WERE ERRORS", result.Output, StringComparison.Ordinal);
			BuildError error = Assert.Single(result.Errors);
			Assert.Equal(new BuildError(BuildErrorType.Error, "Expected ';' at end of variable declaration.", script, 3, 7), error);
		}

		[RealToolFact(RealTools.Compiler)]
		public async Task Compile_SemanticError_IsReportedWithoutTheCategoryInTheMessage()
		{
			String script = this._workspace.Write(@"scripts\semantic.ssl", _semanticErrorScript);

			CompilationResult result = await this.CompileAsync(script);

			Assert.False(result.Success);
			BuildError error = Assert.Single(result.Errors);
			Assert.Equal(BuildErrorType.Error, error.Type);
			Assert.Equal(script, error.FileName);
			Assert.Equal((3, 23), (error.Line, error.Column));
			Assert.Equal("Undefined symbol missing_symbol in factor", error.Message);
		}

		[RealToolFact(RealTools.Compiler)]
		public async Task Compile_MissingInclude_IsReportedAsAnError()
		{
			String script = this._workspace.Write(@"scripts\noinclude.ssl", "#include \"nothing_here.h\"\r\nprocedure start begin\r\nend\r\n");

			CompilationResult result = await this.CompileAsync(script);

			Assert.False(result.Success);
			BuildError error = Assert.Single(result.Errors, e => e.Type == BuildErrorType.Error);
			Assert.Equal(script, error.FileName);
			Assert.Equal(1, error.Line);
			Assert.Contains("nothing_here.h", error.Message, StringComparison.Ordinal);
		}

		[RealToolFact(RealTools.Compiler)]
		public async Task Compile_IncludeNextToTheScriptAndInTheHeadersFolder_BothAreFound()
		{
			this._workspace.Write(@"scripts\" + HeaderName, "#define LOCAL_VALUE 5\r\n");
			this._workspace.Write(@"headers\scripts.h", "// marker file that enables the include search\r\n");
			this._workspace.Write(@"headers\shared.h", "#define SHARED_VALUE 6\r\n");
			String script = this._workspace.Write(@"scripts\inc.ssl", "#include \"lib.h\"\r\n#include \"shared.h\"\r\nprocedure start begin\r\n   variable v := LOCAL_VALUE;\r\n   v += SHARED_VALUE;\r\nend\r\n");
			CompilerOptions options = this._workspace.CreateOptions() with
			{
				HeadersDirectory = Path.Combine(this._workspace.Root, "headers"),
				ScriptsHeaderFile = Path.Combine(this._workspace.Root, "headers", "scripts.h"),
			};

			CompilationResult result = await this.CompileAsync(script, options);

			Assert.True(result.Success, result.Output);
			Assert.True(File.Exists(Path.Combine(this._workspace.Scripts, "inc.int")));
		}

		[RealToolFact(RealTools.Compiler)]
		public async Task Compile_HeadersFolderWithoutTheScriptsHeader_IsNotSearched()
		{
			this._workspace.Write(@"headers\shared.h", "#define SHARED_VALUE 6\r\n");
			String script = this._workspace.Write(@"scripts\inc2.ssl", "#include \"shared.h\"\r\nprocedure start begin\r\nend\r\n");
			CompilerOptions options = this._workspace.CreateOptions() with { HeadersDirectory = Path.Combine(this._workspace.Root, "headers") };

			CompilationResult result = await this.CompileAsync(script, options);

			Assert.False(result.Success);
			Assert.Contains(result.Errors, e => e.Type == BuildErrorType.Error && e.Message.Contains("shared.h", StringComparison.Ordinal));
		}

		[RealToolFact(RealTools.Compiler)]
		public async Task Compile_WarningOnlyScript_SucceedsAndTheWarningIsListed()
		{
			// The optimizer reports the unreachable statement after "return" as a message (full optimization only).
			String script = this._workspace.Write(@"scripts\warn.ssl", SslSamples.Join(
				"procedure start begin",
				"   return 1;",
				"   display_msg(\"never\");",
				"end"));

			CompilationResult result = await this.CompileAsync(script, this._workspace.CreateOptions() with { Optimization = OptimizationLevel.Full });

			Assert.True(result.Success, result.Output);
			Assert.True(File.Exists(Path.Combine(this._workspace.Scripts, "warn.int")));
			Assert.Contains(result.Errors, e => e.Type == BuildErrorType.Message && e.Message.Contains("Eating statement hidden by return", StringComparison.Ordinal) && e.Line == 3 && e.FileName == script);
		}

		[RealToolFact(RealTools.Compiler)]
		public async Task Compile_SslcWarnings_AreListedOneByOneWithoutGluingTheNextEntry()
		{
			// sslc ends every warning with an empty line; the legacy rule "append the next line to a warning" glued the second warning to the first.
			String script = this._workspace.Write(@"scripts\twice.ssl", SslSamples.Join(
				"procedure start begin",
				"   variable arr := 0;",
				"   foreach (variable p: d in arr) begin",
				"      display_msg(\"a\");",
				"   end",
				"   foreach (variable p: d in arr) begin",
				"      display_msg(\"b\");",
				"   end",
				"end"));

			CompilationResult result = await this.CompileAsync(script);

			Assert.True(result.Success, result.Output);
			Assert.Equal(
				new[]
				{
					new BuildError(BuildErrorType.Warning, "Variable foreach 'p' already exists.", script, 6, 23),
					new BuildError(BuildErrorType.Warning, "Variable foreach 'd' already exists.", script, 6, 26),
				},
				result.Errors.Where(e => e.Type == BuildErrorType.Warning).ToArray());
		}

		[RealToolFact(RealTools.Compiler)]
		public async Task Compile_NoWarningsOption_HidesTheWarningsButNotTheOptimizerMessages()
		{
			String script = this._workspace.Write(@"scripts\quiet.ssl", SslSamples.Join(
				"procedure start begin",
				"   variable arr := 0;",
				"   foreach (variable p: d in arr) begin",
				"      return 1;",
				"      display_msg(\"never\");",
				"   end",
				"   foreach (variable p: d in arr) begin",
				"   end",
				"end"));
			CompilerOptions full = this._workspace.CreateOptions() with { Optimization = OptimizationLevel.Full };

			CompilationResult loud = await this.CompileAsync(script, full);
			CompilationResult quiet = await this.CompileAsync(script, full with { ShowWarnings = false });

			Assert.True(loud.Success && quiet.Success, loud.Output + quiet.Output);
			Assert.Contains(loud.Errors, e => e.Type == BuildErrorType.Warning);
			Assert.DoesNotContain(quiet.Errors, e => e.Type == BuildErrorType.Warning);
			Assert.Contains(quiet.Errors, e => e.Type == BuildErrorType.Message && e.Message.Contains("Eating statement hidden by return", StringComparison.Ordinal));
		}

		[RealToolTheory(RealTools.Compiler)]
		[InlineData(OptimizationLevel.None)]
		[InlineData(OptimizationLevel.Basic)]
		[InlineData(OptimizationLevel.Full)]
		public async Task Compile_EveryOptimizationLevel_IsAccepted(OptimizationLevel level)
		{
			String script = this._workspace.Write(@"scripts\opt.ssl", _okScript);

			CompilationResult result = await this.CompileAsync(script, this._workspace.CreateOptions() with { Optimization = level });

			Assert.True(result.Success, result.Output);
			Assert.DoesNotContain("Unknown option", result.Output, StringComparison.Ordinal);
		}

		[RealToolFact(RealTools.Compiler)]
		public async Task Compile_ShortCircuitOption_ChangesTheGeneratedCode()
		{
			String source = SslSamples.Join(
				"procedure check(variable a) begin",
				"   return a;",
				"end",
				"procedure start begin",
				"   variable r := 0;",
				"   if (check(0) and check(1)) then r := 1;",
				"   if (check(1) or check(0)) then r := 2;",
				"end");
			String plain = this._workspace.Write(@"scripts\plain.ssl", source);
			String shortCircuit = this._workspace.Write(@"scripts\sc.ssl", source);

			CompilationResult normal = await this.CompileAsync(plain);
			CompilationResult forced = await this.CompileAsync(shortCircuit, shortCircuit: true);
			CompilationResult configured = await this.CompileAsync(this._workspace.Write(@"scripts\sc2.ssl", source), this._workspace.CreateOptions() with { ShortCircuit = true });

			Assert.True(normal.Success, normal.Output);
			Assert.True(forced.Success, forced.Output);
			Assert.True(configured.Success, configured.Output);
			Assert.DoesNotContain("Unknown option", forced.Output, StringComparison.Ordinal);
			Assert.NotEqual(File.ReadAllBytes(normal.OutputFilePath!), File.ReadAllBytes(forced.OutputFilePath!));
			Assert.Equal(File.ReadAllBytes(forced.OutputFilePath!).Length, File.ReadAllBytes(configured.OutputFilePath!).Length);
		}

		[RealToolFact(RealTools.Compiler)]
		public async Task Compile_BackwardCompatibilityDebugInfoAndDefine_AreAccepted()
		{
			String script = this._workspace.Write(@"scripts\flags.ssl", SslSamples.Join(
				"#ifdef FEATURE_ON",
				"procedure feature_proc begin",
				"end",
				"#endif",
				"procedure start begin",
				"   variable v := FEATURE_VALUE;",
				"end"));

			CompilationResult result = await this.CompileAsync(script, this._workspace.CreateOptions() with { BackwardCompatibility = true, DebugInfo = true, PreprocessorDefine = "FEATURE_VALUE=2" });
			CompilationResult withoutDefine = await this.CompileAsync(script);

			Assert.True(result.Success, result.Output);
			Assert.DoesNotContain("Unknown option", result.Output, StringComparison.Ordinal);
			Assert.Contains("Define macro: FEATURE_VALUE=2", result.Output, StringComparison.Ordinal);
			Assert.False(withoutDefine.Success);
			Assert.Contains(withoutDefine.Errors, e => e.Message.Contains("FEATURE_VALUE", StringComparison.Ordinal));
		}

		[RealToolFact(RealTools.Compiler)]
		public async Task Compile_EmptyPreprocessorDefine_IsTreatedAsNoDefine()
		{
			String script = this._workspace.Write(@"scripts\emptydef.ssl", _okScript);

			CompilationResult result = await this.CompileAsync(script, this._workspace.CreateOptions() with { PreprocessorDefine = String.Empty });

			Assert.True(result.Success, result.Output);
		}

		[RealToolFact(RealTools.Compiler)]
		public async Task Preprocess_ExpandsMacrosAndIncludesIntoACopyInTheScratchFolder()
		{
			this._workspace.Write(@"scripts\lib.h", "#define GREETING \"hello from the header\"\r\n");
			String script = this._workspace.Write(@"scripts\pre.ssl", "#include \"lib.h\"\r\nprocedure start begin\r\n   display_msg(GREETING);\r\nend\r\n");

			CompilationResult result = await this._workspace.Compiler.PreprocessAsync(new CompilationRequest { ScriptPath = script, Options = this._workspace.CreateOptions() });

			Assert.True(result.Success, result.Output);
			Assert.Null(result.OutputFilePath);
			Assert.Equal(Path.Combine(this._workspace.Scratch, "pre_[preproc].ssl"), result.PreprocessedFilePath);
			String text = File.ReadAllText(result.PreprocessedFilePath!, TextEncodings.Ansi);
			Assert.Contains("display_msg( \"hello from the header\" );", text, StringComparison.Ordinal);
			Assert.Contains("#line 1 \"lib.h\"", text, StringComparison.Ordinal);
			Assert.False(File.Exists(Path.Combine(this._workspace.Scripts, "pre.int")));
			Assert.False(File.Exists(Path.Combine(this._workspace.Scratch, "preprocess.ssl")));
		}

		[RealToolFact(RealTools.Compiler)]
		public async Task Compile_Failure_RestoresThePreviousIntFileAndLeavesNoBackup()
		{
			String script = this._workspace.Write(@"scripts\again.ssl", _okScript);
			CompilationResult first = await this.CompileAsync(script);
			Byte[] good = File.ReadAllBytes(first.OutputFilePath!);
			File.WriteAllText(script, _syntaxErrorScript);

			CompilationResult second = await this.CompileAsync(script);

			Assert.True(first.Success, first.Output);
			Assert.False(second.Success);
			Assert.Equal(good, File.ReadAllBytes(first.OutputFilePath!));
			Assert.Empty(Directory.GetFiles(this._workspace.Scratch));
		}

		[RealToolFact(RealTools.Compiler)]
		public async Task Compile_Success_ReplacesThePreviousIntFileAndLeavesNoBackup()
		{
			String script = this._workspace.Write(@"scripts\replace.ssl", _okScript);
			CompilationResult first = await this.CompileAsync(script);
			Byte[] before = File.ReadAllBytes(first.OutputFilePath!);
			File.WriteAllText(script, _okScript.Replace("hi ", "changed text ", StringComparison.Ordinal));

			CompilationResult second = await this.CompileAsync(script);

			Assert.True(second.Success, second.Output);
			Assert.NotEqual(before, File.ReadAllBytes(second.OutputFilePath!));
			Assert.Empty(Directory.GetFiles(this._workspace.Scratch));
		}

		[RealToolFact(RealTools.Compiler)]
		public async Task Compile_ScriptPathWithSpacesAndAccents_WorksAndErrorsKeepTheName()
		{
			String good = this._workspace.Write(Path.Combine("scripts", "my café folder", "gl_my script é.ssl"), _okScript);
			String bad = this._workspace.Write(Path.Combine("scripts", "my café folder", "bad é.ssl"), _syntaxErrorScript);

			CompilationResult ok = await this.CompileAsync(good);
			CompilationResult failed = await this.CompileAsync(bad);

			Assert.True(ok.Success, ok.Output);
			Assert.True(File.Exists(Path.ChangeExtension(good, ".int")));
			Assert.False(failed.Success);
			BuildError error = Assert.Single(failed.Errors);
			Assert.Equal(bad, error.FileName);
			Assert.Equal(3, error.Line);
		}

		[RealToolFact(RealTools.Compiler)]
		public async Task Compile_ToAnOutputFolder_AndToTheScratchFolder()
		{
			String script = this._workspace.Write(@"scripts\out.ssl", _okScript);
			String outputDirectory = Path.Combine(this._workspace.Root, "compiled");
			Directory.CreateDirectory(outputDirectory);

			CompilationResult redirected = await this.CompileAsync(script, this._workspace.CreateOptions() with { CompileToSourceDirectory = false, OutputDirectory = outputDirectory });
			CompilationResult temporary = await this._workspace.Compiler.CompileAsync(new CompilationRequest { ScriptPath = script, Options = this._workspace.CreateOptions(), TemporaryOutput = true });

			Assert.Equal(Path.Combine(outputDirectory, "out.int"), redirected.OutputFilePath);
			Assert.True(File.Exists(redirected.OutputFilePath));
			Assert.Equal(Path.Combine(this._workspace.Scratch, "out.int"), temporary.OutputFilePath);
			Assert.True(File.Exists(temporary.OutputFilePath));
			Assert.False(File.Exists(Path.Combine(this._workspace.Scripts, "out.int")));
		}

		[RealToolFact(RealTools.Compiler)]
		public async Task Compile_ToleratesTheOutputFileBeingReadOnlyFromAnEarlierRun()
		{
			String script = this._workspace.Write(@"scripts\locked.ssl", _okScript);
			CompilationResult first = await this.CompileAsync(script);

			CompilationResult second = await this.CompileAsync(script);

			Assert.True(first.Success && second.Success, first.Output + second.Output);
		}

		[RealToolFact(RealTools.Compiler | RealTools.Decompiler)]
		public async Task Decompile_CompiledScript_WithTheBuiltInDecompilerFindsTheProcedures()
		{
			String script = this._workspace.Write(@"scripts\round.ssl", _okScript);
			CompilationResult compiled = await this.CompileAsync(script);

			DecompilationResult result = await this._workspace.Compiler.DecompileAsync(new DecompilationRequest { ScriptPath = compiled.OutputFilePath!, Options = this._workspace.CreateOptions() with { Decompiler = DecompilerKind.BuiltIn } });

			Assert.True(compiled.Success, compiled.Output);
			Assert.True(result.Success, result.Output);
			String text = File.ReadAllText(result.OutputFilePath!, TextEncodings.Ansi);
			Assert.Contains("procedure start", text, StringComparison.OrdinalIgnoreCase);
			Assert.Contains("procedure helper", text, StringComparison.OrdinalIgnoreCase);
			Assert.Equal(Path.Combine(this._workspace.Scratch, "round_[decomp].ssl"), result.OutputFilePath);
		}

		[RealToolFact(RealTools.Compiler | RealTools.Decompiler)]
		public async Task Decompile_CompiledScript_WithInt2SslFindsTheProcedures()
		{
			String script = this._workspace.Write(@"scripts\round2.ssl", _okScript);
			CompilationResult compiled = await this.CompileAsync(script);
			CompilerOptions options = this._workspace.CreateOptions() with { Decompiler = DecompilerKind.ExternalInt2Ssl, ExternalDecompilerPath = Path.Combine(this._workspace.Resources, "int2ssl.exe") };

			DecompilationResult result = await this._workspace.Compiler.DecompileAsync(new DecompilationRequest { ScriptPath = compiled.OutputFilePath!, Options = options });

			Assert.True(result.Success, result.Output);
			String text = File.ReadAllText(result.OutputFilePath!, TextEncodings.Ansi);
			Assert.Contains("procedure start", text, StringComparison.OrdinalIgnoreCase);
			Assert.Contains("procedure helper", text, StringComparison.OrdinalIgnoreCase);
		}

		[RealToolFact(RealTools.Compiler | RealTools.Decompiler)]
		public async Task Decompile_WithInt2SslFromTheResourcesFolder_ForTheTemporaryRoundTrip()
		{
			String script = this._workspace.Write(@"scripts\round3.ssl", _okScript);
			CompilationResult compiled = await this._workspace.Compiler.CompileAsync(new CompilationRequest { ScriptPath = script, Options = this._workspace.CreateOptions(), TemporaryOutput = true });
			CompilerOptions options = this._workspace.CreateOptions() with { Decompiler = DecompilerKind.ExternalInt2Ssl, TabsToSpaces = false };

			DecompilationResult result = await this._workspace.Compiler.DecompileAsync(new DecompilationRequest { ScriptPath = compiled.OutputFilePath!, Options = options, TemporaryOutput = true });

			Assert.True(result.Success, result.Output);
			Assert.Equal(Path.Combine(this._workspace.Scratch, "round3_[temp].ssl"), result.OutputFilePath);
			Assert.Contains("procedure helper", File.ReadAllText(result.OutputFilePath!, TextEncodings.Ansi), StringComparison.OrdinalIgnoreCase);
		}

		[RealToolFact(RealTools.Compiler | RealTools.Decompiler)]
		public async Task Decompile_NotACompiledScript_FailsWithoutOutput()
		{
			String junk = this._workspace.Write(@"scripts\junk.int", "this is not a compiled script at all");

			DecompilationResult builtIn = await this._workspace.Compiler.DecompileAsync(new DecompilationRequest { ScriptPath = junk, Options = this._workspace.CreateOptions() });
			DecompilationResult external = await this._workspace.Compiler.DecompileAsync(new DecompilationRequest { ScriptPath = junk, Options = this._workspace.CreateOptions() with { Decompiler = DecompilerKind.ExternalInt2Ssl } });

			Assert.False(builtIn.Success, builtIn.Output);
			Assert.False(external.Success, external.Output + " -> " + external.OutputFilePath + " " + (external.OutputFilePath is null ? "" : File.ReadAllText(external.OutputFilePath)));
			Assert.Null(external.OutputFilePath);
			Assert.DoesNotContain(Directory.GetFiles(this._workspace.Scratch), f => f.EndsWith("[decomp].ssl", StringComparison.Ordinal));
		}
	}
}