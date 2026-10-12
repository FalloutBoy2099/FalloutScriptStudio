namespace FalloutScriptStudio.Core.Tests.Compilation
{
	using System;
	using System.Diagnostics;
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;
	using Xunit;

	/// <summary>
	/// Cancelling a real process ends the whole tree, also when the tool is a batch file run by cmd.exe (usercomp.bat, mcpp.bat, wcc.bat).
	/// The grandchild is a private copy of ping.exe so that only this test can have such a process.
	/// </summary>
	public sealed class ProcessRunnerTreeTests : IDisposable
	{
		private const String ChildName = "fsstreechild";

		private readonly TempWorkspace _workspace = new TempWorkspace();
		private readonly ProcessRunner _runner = new ProcessRunner();

		public void Dispose()
		{
			ProcessProbe.KillAll(ChildName);

			this._workspace.Dispose();
		}

		[Fact]
		public async Task Cancelling_a_batch_file_ends_the_cmd_process_and_the_program_it_started()
		{
			String batch = this.CreateBatch("@\"%~dp0" + ChildName + ".exe\" -n 120 127.0.0.1 > nul\r\n");
			using CancellationTokenSource cancellation = new CancellationTokenSource();

			Task<ProcessRunResult> run = this._runner.RunAsync(new ProcessRunRequest { FileName = batch, WorkingDirectory = this._workspace.Root }, cancellation.Token);
			await WaitForChildAsync();
			Assert.Equal(1, ProcessProbe.CountAlive(ChildName));
			Stopwatch watch = Stopwatch.StartNew();
			await cancellation.CancelAsync();

			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);

			// The runner returns only when the tree is gone: no polling is needed here.
			Assert.Equal(0, ProcessProbe.CountAlive(ChildName));
			Assert.True(watch.Elapsed < TimeSpan.FromSeconds(10), "the tree was not stopped quickly");
		}

		[Fact]
		public async Task Cancelling_with_the_output_captured_line_by_line_also_ends_the_tree()
		{
			String batch = this.CreateBatch("@echo started\r\n@\"%~dp0" + ChildName + ".exe\" -n 120 127.0.0.1 > nul\r\n");
			using CancellationTokenSource cancellation = new CancellationTokenSource();
			using ManualResetEventSlim firstLine = new ManualResetEventSlim();

			Task<ProcessRunResult> run = this._runner.RunAsync(
				new ProcessRunRequest { FileName = batch, WorkingDirectory = this._workspace.Root, OutputLineProgress = new SignalProgress(firstLine) },
				cancellation.Token);
			Assert.True(firstLine.Wait(TimeSpan.FromSeconds(20)));
			await WaitForChildAsync();
			await cancellation.CancelAsync();

			await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);

			Assert.Equal(0, ProcessProbe.CountAlive(ChildName));
		}

		[Fact]
		public async Task A_token_cancelled_before_the_start_never_starts_the_process()
		{
			String batch = this.CreateBatch("@\"%~dp0" + ChildName + ".exe\" -n 120 127.0.0.1 > nul\r\n");
			using CancellationTokenSource cancellation = new CancellationTokenSource();
			await cancellation.CancelAsync();

			await Assert.ThrowsAnyAsync<OperationCanceledException>(
				() => this._runner.RunAsync(new ProcessRunRequest { FileName = batch, WorkingDirectory = this._workspace.Root }, cancellation.Token));

			Assert.Equal(0, ProcessProbe.CountAlive(ChildName));
		}

		[Fact]
		public async Task A_batch_file_that_ends_normally_returns_its_exit_code_and_output()
		{
			String batch = this.CreateBatch("@echo done\r\n@exit /b 7\r\n");

			ProcessRunResult result = await this._runner.RunAsync(new ProcessRunRequest { FileName = batch, WorkingDirectory = this._workspace.Root });

			Assert.Equal(7, result.ExitCode);
			Assert.Contains("done", result.Output, StringComparison.Ordinal);
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

		private String CreateBatch(String content)
		{
			String ping = Path.Combine(Environment.SystemDirectory, "ping.exe");
			File.Copy(ping, Path.Combine(this._workspace.Root, ChildName + ".exe"), true);
			String batch = Path.Combine(this._workspace.Root, "tool.bat");
			File.WriteAllText(batch, content);
			return batch;
		}

		private sealed class SignalProgress : IProgress<String>
		{
			private readonly ManualResetEventSlim _signal;

			public SignalProgress(ManualResetEventSlim signal)
			{
				this._signal = signal;
			}

			public void Report(String value)
			{
				this._signal.Set();
			}
		}
	}
}
