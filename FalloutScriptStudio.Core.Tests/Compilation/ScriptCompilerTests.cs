namespace FalloutScriptStudio.Core.Tests.Compilation
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Decompilation;
	using Microsoft.Extensions.Logging.Abstractions;
	using Xunit;

	public sealed class ScriptCompilerTests : IDisposable
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
		public async Task Compile_WithBuiltinPreprocessor_RunsSslcInTheScriptFolder()
		{
			this._workspace.CreateTools("compile.exe");
			String script = this._workspace.CreateFile(@"scripts\test.ssl", "procedure start begin end");
			String expectedInt = Path.Combine(this._workspace.Scripts, "test.int");
			CompilerOptions options = this._workspace.CreateOptions() with { Preprocessor = PreprocessorKind.Builtin };
			this._runner.Handler = _ =>
			{
				File.WriteAllText(expectedInt, "compiled");
				return new ProcessRunResult(0, "Compilation OK\r\n");
			};

			CompilationResult result = await this.CreateCompiler().CompileAsync(new CompilationRequest { ScriptPath = script, Options = options });

			Assert.True(result.Success);
			Assert.Equal(expectedInt, result.OutputFilePath);
			ProcessRunRequest request = Assert.Single(this._runner.Requests);
			Assert.Equal(Path.Combine(this._workspace.Resources, "compile.exe"), request.FileName);
			Assert.Equal(this._workspace.Scripts, request.WorkingDirectory);
			Assert.Equal($"-I\"{this._workspace.Root}\" -p -q -l -O1 -d  \"test.ssl\" -o \"{expectedInt}\"", request.Arguments);
			Assert.StartsWith("****** 12:00:00 ******\r\n----------------------", result.Output, StringComparison.Ordinal);
			Assert.Contains("Compilation OK", result.Output, StringComparison.Ordinal);
		}

		[Fact]
		public async Task Compile_ParsesErrorsFromTheOutput()
		{
			this._workspace.CreateTools("compile.exe");
			String script = this._workspace.CreateFile(@"scripts\test.ssl");
			CompilerOptions options = this._workspace.CreateOptions() with { Preprocessor = PreprocessorKind.Builtin };
			this._runner.Handler = _ => new ProcessRunResult(1, $"[Error] <{script}>:4:2: oops\r\n");

			CompilationResult result = await this.CreateCompiler().CompileAsync(new CompilationRequest { ScriptPath = script, Options = options });

			Assert.False(result.Success);
			Assert.Null(result.OutputFilePath);
			BuildError error = Assert.Single(result.Errors);
			Assert.Equal(new BuildError(BuildErrorType.Error, "oops", script, 4, 2), error);
		}

		[Fact]
		public async Task Compile_FailureRestoresThePreviousIntFile()
		{
			this._workspace.CreateTools("compile.exe");
			String script = this._workspace.CreateFile(@"scripts\test.ssl");
			String intFile = this._workspace.CreateFile(@"scripts\test.int", "OLD");
			CompilerOptions options = this._workspace.CreateOptions() with { Preprocessor = PreprocessorKind.Builtin };
			this._runner.Handler = _ =>
			{
				File.WriteAllText(intFile, "BROKEN");
				return new ProcessRunResult(1, String.Empty);
			};

			CompilationResult result = await this.CreateCompiler().CompileAsync(new CompilationRequest { ScriptPath = script, Options = options });

			Assert.False(result.Success);
			Assert.Equal("OLD", File.ReadAllText(intFile));
			Assert.Empty(Directory.GetFiles(this._workspace.Scratch));
		}

		[Fact]
		public async Task Compile_SuccessReplacesThePreviousIntFileAndDropsTheBackup()
		{
			this._workspace.CreateTools("compile.exe");
			String script = this._workspace.CreateFile(@"scripts\test.ssl");
			String intFile = this._workspace.CreateFile(@"scripts\test.int", "OLD");
			CompilerOptions options = this._workspace.CreateOptions() with { Preprocessor = PreprocessorKind.Builtin };
			this._runner.Handler = _ =>
			{
				File.WriteAllText(intFile, "NEW");
				return new ProcessRunResult(0, String.Empty);
			};

			CompilationResult result = await this.CreateCompiler().CompileAsync(new CompilationRequest { ScriptPath = script, Options = options });

			Assert.True(result.Success);
			Assert.Equal("NEW", File.ReadAllText(intFile));
			Assert.Empty(Directory.GetFiles(this._workspace.Scratch));
		}

		[Fact]
		public async Task Compile_CancellationRestoresThePreviousIntFile()
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
		}

		[Fact]
		public async Task Compile_WithOutputFolder_WritesThereAndRequiresItToBeSet()
		{
			this._workspace.CreateTools("compile.exe");
			String script = this._workspace.CreateFile(@"scripts\test.ssl");
			String outputFolder = Path.Combine(this._workspace.Root, "out");
			CompilerOptions options = this._workspace.CreateOptions() with
			{
				Preprocessor = PreprocessorKind.Builtin,
				CompileToSourceDirectory = false,
				OutputDirectory = outputFolder,
			};

			await this.CreateCompiler().CompileAsync(new CompilationRequest { ScriptPath = script, Options = options });
			Assert.EndsWith($"-o \"{Path.Combine(outputFolder, "test.int")}\"", this._runner.Requests[0].Arguments, StringComparison.Ordinal);

			CompilationResult missing = await this.CreateCompiler().CompileAsync(
				new CompilationRequest { ScriptPath = script, Options = options with { OutputDirectory = null } });
			Assert.False(missing.Success);
			Assert.Contains("output folder", missing.Output, StringComparison.Ordinal);
			Assert.Single(this._runner.Requests);
		}

		[Fact]
		public async Task Compile_TemporaryOutputGoesToTheScratchFolder()
		{
			this._workspace.CreateTools("compile.exe");
			String script = this._workspace.CreateFile(@"scripts\test.ssl");
			CompilerOptions options = this._workspace.CreateOptions() with { Preprocessor = PreprocessorKind.Builtin };

			CompilationResult result = await this.CreateCompiler().CompileAsync(
				new CompilationRequest { ScriptPath = script, Options = options, TemporaryOutput = true });

			Assert.Equal(Path.Combine(this._workspace.Scratch, "test.int"), result.OutputFilePath);
			Assert.Contains(Path.Combine(this._workspace.Scratch, "test.int"), this._runner.Requests[0].Arguments, StringComparison.Ordinal);
			Assert.Equal(Path.Combine(this._workspace.Scratch, "test.int"), this.CreateCompiler().GetOutputPath(script, options, true));
		}

		[Fact]
		public async Task Compile_WithMcpp_PreprocessesFirstAndCompilesTheIntermediateScript()
		{
			this._workspace.CreateTools("compile.exe", "mcpp.bat");
			String script = this._workspace.CreateFile(@"scripts\test.ssl");
			CompilerOptions options = this._workspace.CreateOptions();
			this._runner.Handler = request =>
			{
				if (request.FileName.EndsWith("mcpp.bat", StringComparison.Ordinal))
				{
					File.WriteAllText(Path.Combine(this._workspace.Scratch, "preprocess.ssl"), "preprocessed");
					File.WriteAllText(Path.Combine(this._workspace.Resources, "mcpp.err"), "[Warning] mcpp says hi\r\n");
				}

				return new ProcessRunResult(0, String.Empty);
			};

			CompilationResult result = await this.CreateCompiler().CompileAsync(new CompilationRequest { ScriptPath = script, Options = options });

			Assert.True(result.Success);
			Assert.Equal(2, this._runner.Requests.Count);
			ProcessRunRequest preprocess = this._runner.Requests[0];
			Assert.Equal(Path.Combine(this._workspace.Resources, "mcpp.bat"), preprocess.FileName);
			Assert.Equal(this._workspace.Resources, preprocess.WorkingDirectory);
			Assert.Equal($"\"{script}\" \"{Path.Combine(this._workspace.Scratch, "preprocess.ssl")}\" 1 ..", preprocess.Arguments);

			ProcessRunRequest compile = this._runner.Requests[1];
			Assert.Equal(this._workspace.Scratch, compile.WorkingDirectory);
			String expectedInt = Path.Combine(this._workspace.Scripts, "test.int");
			String intermediate = new CompilerPaths(options).GetIntermediatePreprocessedPath(script);
			Assert.Matches(@"^test_\[pre\]~[0-9A-F]{8}\.ssl$", Path.GetFileName(intermediate));
			Assert.Equal($"-I\"{this._workspace.Root}\" -q -l -O1 -d  \"{Path.GetFileName(intermediate)}\" -o \"{expectedInt}\"", compile.Arguments);

			Assert.True(File.Exists(intermediate));
			Assert.False(File.Exists(Path.Combine(this._workspace.Scratch, "preprocess.ssl")));
			Assert.False(File.Exists(Path.Combine(this._workspace.Resources, "mcpp.err")));
			Assert.Contains("External MCPP preprocessing script: test.ssl", result.Output, StringComparison.Ordinal);
			Assert.Contains("[Done] Preprocessing script successfully completed.", result.Output, StringComparison.Ordinal);
			Assert.Contains(result.Errors, error => error.Type == BuildErrorType.Warning && error.Message.Contains("mcpp says hi", StringComparison.Ordinal));
		}

		[Fact]
		public async Task Compile_WithMcpp_FailureStopsBeforeSslcAndReportsIt()
		{
			this._workspace.CreateTools("compile.exe", "mcpp.bat");
			String script = this._workspace.CreateFile(@"scripts\test.ssl");
			this._runner.Handler = _ => new ProcessRunResult(1, String.Empty);

			CompilationResult result = await this.CreateCompiler().CompileAsync(
				new CompilationRequest { ScriptPath = script, Options = this._workspace.CreateOptions() });

			Assert.False(result.Success);
			Assert.Single(this._runner.Requests);
			Assert.Contains("[Error] Preprocessing script failed...", result.Output, StringComparison.Ordinal);
		}

		[Fact]
		public async Task Preprocess_WithMcpp_ReturnsTheRenamedPreprocessedFile()
		{
			this._workspace.CreateTools("mcpp.bat");
			String script = this._workspace.CreateFile(@"scripts\test.ssl");
			this._runner.Handler = _ =>
			{
				File.WriteAllText(Path.Combine(this._workspace.Scratch, "preprocess.ssl"), "preprocessed");
				return new ProcessRunResult(0, String.Empty);
			};

			CompilationResult result = await this.CreateCompiler().PreprocessAsync(
				new CompilationRequest { ScriptPath = script, Options = this._workspace.CreateOptions() });

			Assert.True(result.Success);
			Assert.Equal(Path.Combine(this._workspace.Scratch, "test_[preproc].ssl"), result.PreprocessedFilePath);
			Assert.NotNull(result.PreprocessedFilePath);
			Assert.Equal("preprocessed", File.ReadAllText(result.PreprocessedFilePath));
			Assert.EndsWith(" -P", Assert.Single(this._runner.Requests).Arguments, StringComparison.Ordinal);
		}

		[Fact]
		public async Task Preprocess_WithBuiltinPreprocessor_UsesSslcPreprocessOnlyMode()
		{
			this._workspace.CreateTools("compile.exe");
			String script = this._workspace.CreateFile(@"scripts\test.ssl");
			this._runner.Handler = _ =>
			{
				File.WriteAllText(Path.Combine(this._workspace.Scratch, "preprocess.ssl"), "preprocessed");
				return new ProcessRunResult(0, String.Empty);
			};

			CompilationResult result = await this.CreateCompiler().PreprocessAsync(
				new CompilationRequest { ScriptPath = script, Options = this._workspace.CreateOptions() with { Preprocessor = PreprocessorKind.Builtin } });

			Assert.True(result.Success);
			Assert.NotNull(result.PreprocessedFilePath);
			Assert.Contains(" -P -q ", Assert.Single(this._runner.Requests).Arguments, StringComparison.Ordinal);
		}

		[Fact]
		public async Task Compile_WithUserCommand_RunsTheBatchFileAndSkipsLogParsing()
		{
			this._workspace.CreateTools("usercomp.bat");
			String script = this._workspace.CreateFile(@"scripts\test.ssl");
			CompilerOptions options = this._workspace.CreateOptions() with { UseUserCommand = true };
			this._runner.Handler = _ => new ProcessRunResult(0, "[Error] <x.ssl>:1: ignored\r\n");

			CompilationResult result = await this.CreateCompiler().CompileAsync(new CompilationRequest { ScriptPath = script, Options = options });

			Assert.True(result.Success);
			Assert.Empty(result.Errors);
			ProcessRunRequest request = Assert.Single(this._runner.Requests);
			Assert.Equal(Path.Combine(this._workspace.Resources, "usercomp.bat"), request.FileName);
			Assert.Equal(this._workspace.Resources, request.WorkingDirectory);
			Assert.Equal($"\"{script}\" ..\\ \"{this._workspace.Scripts}\" 0 1 \"{this._workspace.Scripts}\"", request.Arguments);
		}

		[Fact]
		public async Task Compile_MissingToolIsReportedInsteadOfThrown()
		{
			String script = this._workspace.CreateFile(@"scripts\test.ssl");
			CompilerOptions options = this._workspace.CreateOptions() with { Preprocessor = PreprocessorKind.Builtin };

			CompilationResult result = await this.CreateCompiler().CompileAsync(new CompilationRequest { ScriptPath = script, Options = options });

			Assert.False(result.Success);
			Assert.Empty(this._runner.Requests);
			BuildError error = Assert.Single(result.Errors);
			Assert.Contains("Required tool not found", error.Message, StringComparison.Ordinal);
		}

		[Fact]
		public async Task Compile_BatchModeOmitsTheLogHeader()
		{
			this._workspace.CreateTools("compile.exe");
			String script = this._workspace.CreateFile(@"scripts\test.ssl");
			CompilerOptions options = this._workspace.CreateOptions() with { Preprocessor = PreprocessorKind.Builtin };

			CompilationResult result = await this.CreateCompiler().CompileAsync(
				new CompilationRequest { ScriptPath = script, Options = options, Batch = true });

			Assert.DoesNotContain("******", result.Output, StringComparison.Ordinal);
		}

		[Fact]
		public async Task Decompile_FallsBackToTheOldDecompilerAndReportsProgress()
		{
			this._workspace.CreateTools("int2ssl.exe", "int2ssl_v35.exe");
			String intFile = this._workspace.CreateFile(@"scripts\test.int", "binary");
			this._runner.Handler = request =>
			{
				request.OutputLineProgress?.Report("working " + Path.GetFileName(request.FileName));
				if (request.FileName.EndsWith("int2ssl_v35.exe", StringComparison.Ordinal))
				{
					File.WriteAllText(Path.Combine(this._workspace.Scratch, "decomp.ssl"), "decompiled");
					return new ProcessRunResult(0, "ok\r\n");
				}

				return new ProcessRunResult(1, "failed\r\n");
			};
			List<String> lines = new List<String>();

			DecompilationResult result = await this.CreateCompiler().DecompileAsync(
				new DecompilationRequest { ScriptPath = intFile, Options = this._workspace.CreateOptions() with { Decompiler = DecompilerKind.ExternalInt2Ssl } },
				new SynchronousProgress(lines));

			Assert.True(result.Success);
			Assert.Equal(Path.Combine(this._workspace.Scratch, "test_[decomp].ssl"), result.OutputFilePath);
			Assert.NotNull(result.OutputFilePath);
			Assert.Equal("decompiled", File.ReadAllText(result.OutputFilePath));
			Assert.Equal(new[] { "working int2ssl.exe", "working int2ssl_v35.exe" }, lines);
			Assert.Equal(2, this._runner.Requests.Count);
			Assert.Equal($" -s3 \"{intFile}\" \"{Path.Combine(this._workspace.Scratch, "decomp.ssl")}\"", this._runner.Requests[0].Arguments);
		}

		[Fact]
		public async Task Decompile_OldDecompilerOptionSkipsTheNewOne_AndFailureIsReported()
		{
			this._workspace.CreateTools("int2ssl.exe", "int2ssl_v35.exe");
			String intFile = this._workspace.CreateFile(@"scripts\test.int", "binary");
			this._runner.Handler = _ => new ProcessRunResult(1, "failed\r\n");

			DecompilationResult result = await this.CreateCompiler().DecompileAsync(
				new DecompilationRequest { ScriptPath = intFile, Options = this._workspace.CreateOptions() with { Decompiler = DecompilerKind.ExternalInt2Ssl, UseLegacyDecompiler = true }, TemporaryOutput = true });

			Assert.False(result.Success);
			Assert.Null(result.OutputFilePath);
			Assert.EndsWith("int2ssl_v35.exe", Assert.Single(this._runner.Requests).FileName, StringComparison.Ordinal);
		}

		[Fact]
		public async Task Decompile_TemporaryOutputUsesTheTempSuffix()
		{
			this._workspace.CreateTools("int2ssl.exe");
			String intFile = this._workspace.CreateFile(@"scripts\test.int", "binary");
			this._runner.Handler = _ =>
			{
				File.WriteAllText(Path.Combine(this._workspace.Scratch, "decomp.ssl"), "x");
				return new ProcessRunResult(0, String.Empty);
			};

			DecompilationResult result = await this.CreateCompiler().DecompileAsync(
				new DecompilationRequest { ScriptPath = intFile, Options = this._workspace.CreateOptions() with { Decompiler = DecompilerKind.ExternalInt2Ssl }, TemporaryOutput = true });

			Assert.Equal(Path.Combine(this._workspace.Scratch, "test_[temp].ssl"), result.OutputFilePath);
		}

		[Fact]
		public async Task Decompile_UsesTheBuiltInDecompilerByDefault_WithoutStartingAnyProcess()
		{
			String intFile = this._workspace.CreateFile(@"scripts\test.int", "not a compiled script");

			DecompilationResult result = await this.CreateCompiler().DecompileAsync(
				new DecompilationRequest { ScriptPath = intFile, Options = this._workspace.CreateOptions() });

			Assert.False(result.Success);
			Assert.Null(result.OutputFilePath);
			Assert.Empty(this._runner.Requests);
		}

		[Fact]
		public async Task Decompile_IsDelegatedToTheInjectedDecompiler()
		{
			RecordingDecompiler decompiler = new RecordingDecompiler();
			ScriptCompiler compiler = new ScriptCompiler(this._runner, NullLogger<ScriptCompiler>.Instance, new FixedTimeProvider(), decompiler);
			DecompilationRequest request = new DecompilationRequest { ScriptPath = "a.int", Options = this._workspace.CreateOptions(), TemporaryOutput = true };
			SynchronousProgress progress = new SynchronousProgress(new List<String>());

			DecompilationResult result = await compiler.DecompileAsync(request, progress);

			Assert.Same(request, decompiler.Request);
			Assert.Same(progress, decompiler.Progress);
			Assert.Equal("done", result.OutputFilePath);
		}

		[Fact]
		public void CompilerPaths_RemovesThePreprocessSuffixOnlyForExternalPreprocessors()
		{
			CompilerOptions mcpp = this._workspace.CreateOptions();
			CompilerOptions builtin = mcpp with { Preprocessor = PreprocessorKind.Builtin };
			String intermediate = Path.Combine(this._workspace.Scratch, "test_[pre].ssl");

			Assert.Equal(Path.Combine(@"C:\src", "test.int"), new CompilerPaths(mcpp).GetOutputPath(intermediate, @"C:\src"));
			Assert.Equal(Path.Combine(@"C:\src", "test_[pre].int"), new CompilerPaths(builtin).GetOutputPath(intermediate, @"C:\src"));
		}

		private sealed class RecordingDecompiler : IScriptDecompiler
		{
			public DecompilationRequest? Request { get; private set; }

			public IProgress<String>? Progress { get; private set; }

			public Task<DecompilationResult> DecompileAsync(DecompilationRequest request, IProgress<String>? outputProgress = null, CancellationToken cancellationToken = default)
			{
				this.Request = request;
				this.Progress = outputProgress;
				return Task.FromResult(new DecompilationResult(true, "done", String.Empty));
			}
		}

		private sealed class SynchronousProgress : IProgress<String>
		{
			private readonly List<String> _target;

			public SynchronousProgress(List<String> target)
			{
				this._target = target;
			}

			public void Report(String value)
			{
				lock (this._target)
				{
					this._target.Add(value);
				}
			}
		}
	}
}
