namespace FalloutScriptStudio.Core.Tests.Native
{
	using System;
	using System.Diagnostics;
	using System.IO;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Tests.Compilation;
	using Microsoft.Extensions.Logging.Abstractions;
	using Xunit;

	/// <summary>
	/// Cancels real compilations: through <c>ScriptCompiler</c> and the real <c>ProcessRunner</c> a batch file that hangs (usercomp.bat, mcpp.bat
	/// through cmd.exe) loses its whole process tree, and the real <c>compile.exe</c> leaves no process and no file behind whenever the cancel arrives.
	/// </summary>
	public sealed class RealCancellationTests : IDisposable
	{
		private const String ChildName = "fsscancelchild";

		private readonly RealCompilerWorkspace _workspace = new RealCompilerWorkspace();

		public void Dispose()
		{
			ProcessProbe.KillAll(ChildName);

			this._workspace.Dispose();
		}

		private static async Task WaitForChildAsync()
		{
			Stopwatch watch = Stopwatch.StartNew();
			while (ProcessProbe.CountAlive(ChildName) == 0)
			{
				Assert.True(watch.Elapsed < TimeSpan.FromSeconds(20), "the batch file did not start its program");
				await Task.Delay(25);
			}
		}

		private void InstallHangingBatch(String name, String before)
		{
			File.Copy(Path.Combine(Environment.SystemDirectory, "ping.exe"), Path.Combine(this._workspace.Resources, ChildName + ".exe"), true);
			File.WriteAllText(Path.Combine(this._workspace.Resources, name), "@echo off\r\n" + before + "\"%~dp0" + ChildName + ".exe\" -n 120 127.0.0.1 > nul\r\n");
		}

		[Fact]
		public async Task Cancelling_the_user_batch_file_kills_cmd_and_its_program()
		{
			this.InstallHangingBatch("usercomp.bat", String.Empty);
			String script = this._workspace.Write(@"scripts\test.ssl", "procedure start begin end");
			CompilerOptions options = this._workspace.CreateOptions() with { UseUserCommand = true };
			using CancellationTokenSource cancellation = new CancellationTokenSource();

			Task<CompilationResult> run = this._workspace.Compiler.CompileAsync(new CompilationRequest { ScriptPath = script, Options = options }, cancellation.Token);
			await WaitForChildAsync();
			await cancellation.CancelAsync();

			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
			Assert.Equal(0, ProcessProbe.CountAlive(ChildName));
		}

		[Fact]
		public async Task Cancelling_the_external_preprocessor_kills_its_tree_and_removes_what_it_wrote()
		{
			this.InstallHangingBatch("mcpp.bat", "echo half of the output> %2\r\necho half of the log> \"%~dp0mcpp.err\"\r\n");
			String script = this._workspace.Write(@"scripts\test.ssl", "procedure start begin end");
			CompilerOptions options = this._workspace.CreateOptions() with { Preprocessor = PreprocessorKind.Mcpp };
			using CancellationTokenSource cancellation = new CancellationTokenSource();

			Task<CompilationResult> run = this._workspace.Compiler.CompileAsync(new CompilationRequest { ScriptPath = script, Options = options }, cancellation.Token);
			await WaitForChildAsync();
			await cancellation.CancelAsync();

			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
			Assert.Equal(0, ProcessProbe.CountAlive(ChildName));
			Assert.Empty(Directory.GetFiles(this._workspace.Scratch));
			Assert.False(File.Exists(Path.Combine(this._workspace.Resources, "mcpp.err")));
		}

		[Fact]
		public async Task Cancelling_a_batch_preprocessing_removes_the_random_file()
		{
			this.InstallHangingBatch("mcpp.bat", "echo half of the output> %2\r\n");
			String script = this._workspace.Write(@"scripts\test.ssl", "procedure start begin end");
			CompilerOptions options = this._workspace.CreateOptions() with { Preprocessor = PreprocessorKind.Mcpp };
			using CancellationTokenSource cancellation = new CancellationTokenSource();

			Task<CompilationResult> run = this._workspace.Compiler.CompileAsync(new CompilationRequest { ScriptPath = script, Options = options, Batch = true }, cancellation.Token);
			await WaitForChildAsync();
			await cancellation.CancelAsync();

			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
			Assert.Equal(0, ProcessProbe.CountAlive(ChildName));
			Assert.Empty(Directory.GetFiles(this._workspace.Scratch));
		}

		[RealToolFact(RealTools.Compiler)]
		public async Task Cancelling_the_real_compiler_at_any_moment_leaves_no_process_and_no_file()
		{
			String script = this._workspace.Write(@"scripts\ok.ssl", SslSamples.Join(
				"procedure start;",
				"procedure start begin",
				"   variable x := 1;",
				"   display_msg(\"hi \" + x);",
				"end"));
			String intFile = Path.Combine(this._workspace.Scripts, "ok.int");
			File.WriteAllText(intFile, "PREVIOUS");
			Int32 cancelled = 0;
			Int32 completed = 0;

			for (Int32 delay = 0; delay < 40; delay += 2)
			{
				using CancellationTokenSource cancellation = new CancellationTokenSource();
				Task<CompilationResult> run = this._workspace.Compiler.CompileAsync(new CompilationRequest { ScriptPath = script, Options = this._workspace.CreateOptions() }, cancellation.Token);
				await Task.Delay(delay);
				await cancellation.CancelAsync();
				try
				{
					CompilationResult result = await run;
					completed++;
					Assert.True(result.Success, result.Output);
					Assert.NotEqual("PREVIOUS", File.ReadAllText(intFile));
					File.WriteAllText(intFile, "PREVIOUS");
				}
				catch (OperationCanceledException)
				{
					cancelled++;

					// Whatever the moment, the previous compiled script is back and nothing else stays in the scratch folder.
					Assert.Equal("PREVIOUS", File.ReadAllText(intFile));
				}

				Assert.Equal(0, ProcessProbe.CountAlive("compile", this._workspace.Resources));
				Assert.Empty(Directory.GetFiles(this._workspace.Scratch));
			}

			Assert.Equal(20, cancelled + completed);
		}
	}
}