namespace FalloutScriptStudio.Core.Tests.Compilation
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.IO;
	using Microsoft.Extensions.Logging.Abstractions;
	using Xunit;

	public sealed class ScriptCompilerRobustnessTests : IDisposable
	{
		private readonly TempWorkspace _workspace = new TempWorkspace();
		private readonly FakeProcessRunner _runner = new FakeProcessRunner();

		public void Dispose()
		{
			foreach (String file in Directory.GetFiles(this._workspace.Root, "*", SearchOption.AllDirectories))
			{
				File.SetAttributes(file, FileAttributes.Normal);
			}

			this._workspace.Dispose();
		}

		private ScriptCompiler CreateCompiler()
		{
			return new ScriptCompiler(this._runner, NullLogger<ScriptCompiler>.Instance, new FixedTimeProvider());
		}

		[Fact]
		public async Task UserCommand_ReadsTheBatchOutputInTheOemCodePage()
		{
			this._workspace.CreateTools("usercomp.bat");
			String script = this._workspace.CreateFile(@"scripts\test.ssl");
			CompilerOptions options = this._workspace.CreateOptions() with { UseUserCommand = true };

			await this.CreateCompiler().CompileAsync(new CompilationRequest { ScriptPath = script, Options = options });

			// cmd.exe prints in the OEM code page, whatever the ANSI code page is.
			ProcessRunRequest request = Assert.Single(this._runner.Requests);
			Assert.Same(TextEncodings.Oem, request.OutputEncoding);
		}

		[Fact]
		public async Task Tools_ReceiveTheScratchFolderInTheEnvironment()
		{
			this._workspace.CreateTools("usercomp.bat");
			String script = this._workspace.CreateFile(@"scripts\test.ssl");
			CompilerOptions options = this._workspace.CreateOptions() with { UseUserCommand = true };

			await this.CreateCompiler().CompileAsync(new CompilationRequest { ScriptPath = script, Options = options });

			ProcessRunRequest request = Assert.Single(this._runner.Requests);
			Assert.NotNull(request.Environment);
			Assert.Equal(this._workspace.Scratch, request.Environment[ScriptCompiler.ScratchDirectoryVariable]);
		}

		[Fact]
		public async Task PreprocessorErrorLog_IsReadInTheAnsiCodePage()
		{
			this._workspace.CreateTools("compile.exe", "mcpp.bat");
			String script = this._workspace.CreateFile(@"scripts\test.ssl");
			String accent = TextEncodings.Ansi.GetString(new Byte[] { 0xE9 });
			this._runner.Handler = request =>
			{
				if (request.FileName.EndsWith("mcpp.bat", StringComparison.Ordinal))
				{
					File.WriteAllText(Path.Combine(this._workspace.Scratch, "preprocess.ssl"), "preprocessed");
					File.WriteAllBytes(Path.Combine(this._workspace.Resources, "mcpp.err"), TextEncodings.Ansi.GetBytes("[Warning] <caf" + accent + @"\test.ssl>:3: warn" + "\r\n"));
				}

				return new ProcessRunResult(0, String.Empty);
			};

			CompilationResult result = await this.CreateCompiler().CompileAsync(
				new CompilationRequest { ScriptPath = script, Options = this._workspace.CreateOptions() });

			Assert.Contains("caf" + accent, result.Output, StringComparison.Ordinal);
			Assert.DoesNotContain('\uFFFD', result.Output);
		}

		[Fact]
		public void IntermediateFile_IsDifferentForTwoScriptsOfTheSameName()
		{
			CompilerPaths paths = new CompilerPaths(this._workspace.CreateOptions());
			String first = paths.GetIntermediatePreprocessedPath(@"C:\mod\a\dialog.ssl");
			String second = paths.GetIntermediatePreprocessedPath(@"C:\mod\b\dialog.ssl");

			Assert.NotEqual(first, second, StringComparer.OrdinalIgnoreCase);
			Assert.Equal(first, paths.GetIntermediatePreprocessedPath(@"C:\mod\a\dialog.ssl"));
			Assert.Equal(first, paths.GetIntermediatePreprocessedPath(@"C:\MOD\A\Dialog.ssl"), ignoreCase: true);
			Assert.StartsWith("dialog_[pre]~", Path.GetFileName(first), StringComparison.Ordinal);
		}

		[Fact]
		public void OutputName_IgnoresTheHashOfTheIntermediateFile()
		{
			CompilerOptions options = this._workspace.CreateOptions();
			CompilerPaths paths = new CompilerPaths(options);
			String intermediate = paths.GetIntermediatePreprocessedPath(@"C:\mod\a\dialog.ssl");

			Assert.Equal(Path.Combine(@"C:\mod\a", "dialog.int"), paths.GetOutputPath(intermediate, @"C:\mod\a"));
		}

		[Fact]
		public async Task BatchCompile_OfTwoScriptsOfTheSameName_UsesTwoIntermediateFiles()
		{
			this._workspace.CreateTools("compile.exe", "mcpp.bat");
			String first = this._workspace.CreateFile(@"scripts\a\dialog.ssl");
			String second = this._workspace.CreateFile(@"scripts\b\dialog.ssl");
			this._runner.Handler = request =>
			{
				if (request.FileName.EndsWith("mcpp.bat", StringComparison.Ordinal))
				{
					String name = request.Arguments.Split('"', StringSplitOptions.RemoveEmptyEntries).First(part => part.StartsWith(this._workspace.Scratch, StringComparison.Ordinal));
					File.WriteAllText(name, "preprocessed " + request.Arguments);
				}

				return new ProcessRunResult(0, String.Empty);
			};
			CompilerOptions options = this._workspace.CreateOptions();

			CompilationResult one = await this.CreateCompiler().CompileAsync(new CompilationRequest { ScriptPath = first, Options = options, Batch = true });
			CompilationResult two = await this.CreateCompiler().CompileAsync(new CompilationRequest { ScriptPath = second, Options = options, Batch = true });

			Assert.True(one.Success);
			Assert.True(two.Success);
			String[] intermediates = Directory.GetFiles(this._workspace.Scratch, "dialog_[pre]~*.ssl");
			Assert.Equal(2, intermediates.Length);
		}

		[Fact]
		public async Task Compile_SucceedingOverAReadOnlyScript_LeavesNoBackupBehind()
		{
			this._workspace.CreateTools("compile.exe");
			String script = this._workspace.CreateFile(@"scripts\test.ssl");
			String intFile = this._workspace.CreateFile(@"scripts\test.int", "OLD");
			File.SetAttributes(intFile, FileAttributes.ReadOnly);
			CompilerOptions options = this._workspace.CreateOptions() with { Preprocessor = PreprocessorKind.Builtin };

			CompilationResult result = await this.CreateCompiler().CompileAsync(new CompilationRequest { ScriptPath = script, Options = options });

			// The backup copy of a read-only file is read-only too and could never be deleted (nor removed at the next start-up).
			Assert.True(result.Success);
			Assert.Empty(Directory.GetFiles(this._workspace.Scratch));
		}

		[Fact]
		public async Task Compile_WhenTheIntermediateFileCannotBeReplaced_FailsWithoutThrowing()
		{
			this._workspace.CreateTools("compile.exe", "mcpp.bat");
			String script = this._workspace.CreateFile(@"scripts\test.ssl");
			CompilerOptions options = this._workspace.CreateOptions();
			String intermediate = new CompilerPaths(options).GetIntermediatePreprocessedPath(script);
			File.WriteAllText(intermediate, "stale");
			File.SetAttributes(intermediate, FileAttributes.ReadOnly);
			this._runner.Handler = request =>
			{
				if (request.FileName.EndsWith("mcpp.bat", StringComparison.Ordinal))
				{
					File.WriteAllText(Path.Combine(this._workspace.Scratch, "preprocess.ssl"), "preprocessed");
				}

				return new ProcessRunResult(0, String.Empty);
			};

			CompilationResult result = await this.CreateCompiler().CompileAsync(new CompilationRequest { ScriptPath = script, Options = options });

			Assert.False(result.Success);
			Assert.Contains("preprocessed file", result.Output, StringComparison.OrdinalIgnoreCase);
		}

		[Fact]
		public async Task Preprocess_WhenTheCopyCannotBeReplaced_ReturnsNoCopyWithoutThrowing()
		{
			this._workspace.CreateTools("mcpp.bat");
			String script = this._workspace.CreateFile(@"scripts\test.ssl");
			String copy = Path.Combine(this._workspace.Scratch, "test_[preproc].ssl");
			File.WriteAllText(copy, "stale");
			File.SetAttributes(copy, FileAttributes.ReadOnly);
			this._runner.Handler = _ =>
			{
				File.WriteAllText(Path.Combine(this._workspace.Scratch, "preprocess.ssl"), "preprocessed");
				return new ProcessRunResult(0, String.Empty);
			};

			CompilationResult result = await this.CreateCompiler().PreprocessAsync(new CompilationRequest { ScriptPath = script, Options = this._workspace.CreateOptions() });

			Assert.True(result.Success);
			Assert.Null(result.PreprocessedFilePath);
		}
	}
}
