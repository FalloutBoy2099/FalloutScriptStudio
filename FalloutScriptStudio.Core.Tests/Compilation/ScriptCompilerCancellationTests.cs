namespace FalloutScriptStudio.Core.Tests.Compilation
{
	using System;
	using System.ComponentModel;
	using System.IO;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Decompilation;
	using Microsoft.Extensions.Logging.Abstractions;
	using Xunit;

	/// <summary>What a cancelled compilation, preprocessing or decompilation leaves behind (nothing), and how missing tools are reported.</summary>
	public sealed class ScriptCompilerCancellationTests : IDisposable
	{
		private readonly TempWorkspace _workspace = new TempWorkspace();
		private readonly FakeProcessRunner _runner = new FakeProcessRunner();

		public void Dispose()
		{
			this._workspace.Dispose();
		}

		private ScriptCompiler CreateCompiler()
		{
			return new ScriptCompiler(this._runner, NullLogger<ScriptCompiler>.Instance, new FixedTimeProvider());
		}

		[Fact]
		public async Task A_cancelled_compile_without_a_previous_int_removes_the_partial_output()
		{
			this._workspace.CreateTools("compile.exe");
			String script = this._workspace.CreateFile(@"scripts\test.ssl");
			String intFile = Path.Combine(this._workspace.Scripts, "test.int");
			CompilerOptions options = this._workspace.CreateOptions() with { Preprocessor = PreprocessorKind.Builtin };
			this._runner.Handler = _ =>
			{
				File.WriteAllText(intFile, "PARTIAL");
				throw new OperationCanceledException();
			};

			await Assert.ThrowsAsync<OperationCanceledException>(
				() => this.CreateCompiler().CompileAsync(new CompilationRequest { ScriptPath = script, Options = options }));

			Assert.False(File.Exists(intFile));
			Assert.Empty(Directory.GetFiles(this._workspace.Scratch));
		}

		[Fact]
		public async Task A_cancelled_compile_with_a_previous_int_restores_it_and_drops_the_backup()
		{
			this._workspace.CreateTools("compile.exe");
			String script = this._workspace.CreateFile(@"scripts\test.ssl");
			String intFile = this._workspace.CreateFile(@"scripts\test.int", "OLD");
			CompilerOptions options = this._workspace.CreateOptions() with { Preprocessor = PreprocessorKind.Builtin };
			this._runner.Handler = _ =>
			{
				File.WriteAllText(intFile, "PARTIAL");
				throw new OperationCanceledException();
			};

			await Assert.ThrowsAsync<OperationCanceledException>(
				() => this.CreateCompiler().CompileAsync(new CompilationRequest { ScriptPath = script, Options = options }));

			Assert.Equal("OLD", File.ReadAllText(intFile));
			Assert.Empty(Directory.GetFiles(this._workspace.Scratch));
		}

		[Fact]
		public async Task A_cancelled_round_trip_compile_removes_the_temporary_int()
		{
			this._workspace.CreateTools("compile.exe");
			String script = this._workspace.CreateFile(@"scripts\test.ssl");
			CompilerOptions options = this._workspace.CreateOptions() with { Preprocessor = PreprocessorKind.Builtin };
			this._runner.Handler = _ =>
			{
				File.WriteAllText(Path.Combine(this._workspace.Scratch, "test.int"), "OLD TEMPORARY");
				throw new OperationCanceledException();
			};

			await Assert.ThrowsAsync<OperationCanceledException>(
				() => this.CreateCompiler().CompileAsync(new CompilationRequest { ScriptPath = script, Options = options, TemporaryOutput = true }));

			Assert.Empty(Directory.GetFiles(this._workspace.Scratch));
		}

		[Fact]
		public async Task A_cancelled_builtin_preprocessing_removes_the_partial_preprocess_file()
		{
			this._workspace.CreateTools("compile.exe");
			String script = this._workspace.CreateFile(@"scripts\test.ssl");
			CompilerOptions options = this._workspace.CreateOptions() with { Preprocessor = PreprocessorKind.Builtin };
			this._runner.Handler = _ =>
			{
				File.WriteAllText(Path.Combine(this._workspace.Scratch, "preprocess.ssl"), "half of it");
				throw new OperationCanceledException();
			};

			await Assert.ThrowsAsync<OperationCanceledException>(
				() => this.CreateCompiler().PreprocessAsync(new CompilationRequest { ScriptPath = script, Options = options }));

			Assert.Empty(Directory.GetFiles(this._workspace.Scratch));
		}

		[Fact]
		public async Task A_cancelled_external_preprocessing_removes_the_preprocess_file_and_the_error_log()
		{
			this._workspace.CreateTools("compile.exe", "mcpp.bat");
			String script = this._workspace.CreateFile(@"scripts\test.ssl");
			CompilerOptions options = this._workspace.CreateOptions();
			this._runner.Handler = _ =>
			{
				File.WriteAllText(Path.Combine(this._workspace.Scratch, "preprocess.ssl"), "half of it");
				File.WriteAllText(Path.Combine(this._workspace.Resources, "mcpp.err"), "[Warning] half\r\n");
				throw new OperationCanceledException();
			};

			await Assert.ThrowsAsync<OperationCanceledException>(
				() => this.CreateCompiler().CompileAsync(new CompilationRequest { ScriptPath = script, Options = options }));

			Assert.Empty(Directory.GetFiles(this._workspace.Scratch));
			Assert.False(File.Exists(Path.Combine(this._workspace.Resources, "mcpp.err")));
			Assert.Single(this._runner.Requests);
		}

		[Fact]
		public async Task A_batch_compile_cancelled_during_sslc_removes_the_random_preprocess_file_and_the_intermediate_script()
		{
			this._workspace.CreateTools("compile.exe", "mcpp.bat");
			String script = this._workspace.CreateFile(@"scripts\test.ssl");
			CompilerOptions options = this._workspace.CreateOptions();
			this._runner.Handler = request =>
			{
				if (request.FileName.EndsWith("mcpp.bat", StringComparison.Ordinal))
				{
					// The batch run writes the preprocessed script under the random name given on its command line.
					String target = request.Arguments.Split('"')[3];
					File.WriteAllText(target, "preprocessed");
					return new ProcessRunResult(0, String.Empty);
				}

				File.WriteAllText(Path.Combine(this._workspace.Scripts, "test.int"), "PARTIAL");
				throw new OperationCanceledException();
			};

			await Assert.ThrowsAsync<OperationCanceledException>(
				() => this.CreateCompiler().CompileAsync(new CompilationRequest { ScriptPath = script, Options = options, Batch = true }));

			Assert.Empty(Directory.GetFiles(this._workspace.Scratch));
			Assert.False(File.Exists(Path.Combine(this._workspace.Scripts, "test.int")));
			Assert.Equal(2, this._runner.Requests.Count);
		}

		[Fact]
		public async Task A_token_that_is_already_cancelled_does_not_start_any_tool_or_touch_a_file()
		{
			this._workspace.CreateTools("compile.exe");
			String script = this._workspace.CreateFile(@"scripts\test.ssl");
			String intFile = this._workspace.CreateFile(@"scripts\test.int", "OLD");
			CompilerOptions options = this._workspace.CreateOptions() with { Preprocessor = PreprocessorKind.Builtin };
			using CancellationTokenSource cancellation = new CancellationTokenSource();
			await cancellation.CancelAsync();
			this._runner.Handler = _ => throw new InvalidOperationException("no tool may run");

			await Assert.ThrowsAnyAsync<OperationCanceledException>(
				() => this.CreateCompiler().CompileAsync(new CompilationRequest { ScriptPath = script, Options = options }, cancellation.Token));

			Assert.Equal("OLD", File.ReadAllText(intFile));
			Assert.Empty(Directory.GetFiles(this._workspace.Scratch));
		}

		[Fact]
		public async Task A_cancelled_external_decompilation_removes_the_work_file()
		{
			this._workspace.CreateTools("int2ssl.exe");
			String intFile = this._workspace.CreateFile(@"scripts\test.int", "binary");
			this._runner.Handler = _ =>
			{
				File.WriteAllText(Path.Combine(this._workspace.Scratch, "decomp.ssl"), "half of it");
				throw new OperationCanceledException();
			};

			await Assert.ThrowsAsync<OperationCanceledException>(
				() => this.CreateCompiler().DecompileAsync(
					new DecompilationRequest { ScriptPath = intFile, Options = this._workspace.CreateOptions() with { Decompiler = DecompilerKind.ExternalInt2Ssl } }));

			Assert.Empty(Directory.GetFiles(this._workspace.Scratch));
		}

		[Fact]
		public async Task A_decompilation_cancelled_before_it_starts_throws_and_leaves_no_file()
		{
			String intFile = this._workspace.CreateFile(@"scripts\test.int", "binary");
			using CancellationTokenSource cancellation = new CancellationTokenSource();
			await cancellation.CancelAsync();

			await Assert.ThrowsAnyAsync<OperationCanceledException>(
				() => new BuiltInScriptDecompiler().DecompileAsync(
					new DecompilationRequest { ScriptPath = intFile, Options = this._workspace.CreateOptions() },
					null,
					cancellation.Token));

			Assert.Empty(Directory.GetFiles(this._workspace.Scratch));
		}

		[Fact]
		public async Task A_missing_compiler_is_reported_as_a_structured_tool_failure()
		{
			String script = this._workspace.CreateFile(@"scripts\test.ssl");
			CompilerOptions options = this._workspace.CreateOptions() with { Preprocessor = PreprocessorKind.Builtin };

			CompilationResult result = await this.CreateCompiler().CompileAsync(new CompilationRequest { ScriptPath = script, Options = options });

			ToolFailure failure = Assert.Single(result.ToolFailures);
			Assert.Equal(ToolFailureKind.NotFound, failure.Kind);
			Assert.Equal(Path.Combine(this._workspace.Resources, "compile.exe"), failure.ToolPath);
			Assert.Equal("compile.exe", failure.ToolName);
			Assert.Contains(failure.LogMessage, Assert.Single(result.Errors).Message, StringComparison.Ordinal);
		}

		[Fact]
		public async Task A_tool_that_cannot_be_started_is_reported_with_the_reason()
		{
			this._workspace.CreateTools("compile.exe");
			String script = this._workspace.CreateFile(@"scripts\test.ssl");
			CompilerOptions options = this._workspace.CreateOptions() with { Preprocessor = PreprocessorKind.Builtin };
			this._runner.Handler = _ => throw new Win32Exception("The specified executable is not a valid application");

			CompilationResult result = await this.CreateCompiler().CompileAsync(new CompilationRequest { ScriptPath = script, Options = options });

			Assert.False(result.Success);
			ToolFailure failure = Assert.Single(result.ToolFailures);
			Assert.Equal(ToolFailureKind.StartFailed, failure.Kind);
			Assert.Contains("not a valid application", failure.Detail, StringComparison.Ordinal);
			Assert.Contains(failure.LogMessage, Assert.Single(result.Errors).Message, StringComparison.Ordinal);
		}

		[Fact]
		public async Task A_missing_external_preprocessor_and_a_missing_decompiler_are_tool_failures_too()
		{
			this._workspace.CreateTools("compile.exe");
			String script = this._workspace.CreateFile(@"scripts\test.ssl");
			String intFile = this._workspace.CreateFile(@"scripts\test.int", "binary");

			CompilationResult compiled = await this.CreateCompiler().CompileAsync(new CompilationRequest { ScriptPath = script, Options = this._workspace.CreateOptions() });
			DecompilationResult decompiled = await this.CreateCompiler().DecompileAsync(
				new DecompilationRequest { ScriptPath = intFile, Options = this._workspace.CreateOptions() with { Decompiler = DecompilerKind.ExternalInt2Ssl } });

			Assert.Equal("mcpp.bat", Assert.Single(compiled.ToolFailures).ToolName);
			Assert.False(decompiled.Success);
			Assert.Equal(new[] { "int2ssl.exe", "int2ssl_v35.exe" }, decompiled.ToolFailures.Select(failure => failure.ToolName));
		}
	}
}
