namespace FalloutScriptStudio.Core.Tests.Compilation
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.Diagnostics;
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;
	using Xunit;

	public sealed class ProcessRunnerTests : IDisposable
	{
		private readonly TempWorkspace _workspace = new TempWorkspace();
		private readonly ProcessRunner _runner = new ProcessRunner();

		public void Dispose()
		{
			this._workspace.Dispose();
		}

		[Fact]
		public async Task Run_CapturesOutputAndExitCode()
		{
			ProcessRunResult result = await this._runner.RunAsync(new ProcessRunRequest { FileName = "cmd.exe", Arguments = "/c echo hello&exit 3" });

			Assert.Equal(3, result.ExitCode);
			Assert.Contains("hello", result.Output, StringComparison.Ordinal);
		}

		[Fact]
		public async Task Run_UsesTheWorkingDirectory()
		{
			ProcessRunResult result = await this._runner.RunAsync(
				new ProcessRunRequest { FileName = "cmd.exe", Arguments = "/c cd", WorkingDirectory = this._workspace.Scripts });

			Assert.Equal(0, result.ExitCode);
			Assert.Contains(this._workspace.Scripts, result.Output, StringComparison.OrdinalIgnoreCase);
		}

		[Fact]
		public async Task Run_WithoutCaptureReturnsEmptyOutput()
		{
			ProcessRunResult result = await this._runner.RunAsync(new ProcessRunRequest { FileName = "cmd.exe", Arguments = "/c echo hidden", CaptureOutput = false });

			Assert.Equal(0, result.ExitCode);
			Assert.Equal(String.Empty, result.Output);
		}

		[Fact]
		public async Task Run_ReportsEveryOutputLineToTheProgressReceiver()
		{
			List<String> lines = new List<String>();

			ProcessRunResult result = await this._runner.RunAsync(
				new ProcessRunRequest { FileName = "cmd.exe", Arguments = "/c echo one&echo two", OutputLineProgress = new LineCollector(lines) });

			Assert.Equal(0, result.ExitCode);
			Assert.Equal(new[] { "one", "two" }, lines);
		}

		[Fact]
		public async Task Run_CancellationKillsTheProcess()
		{
			using CancellationTokenSource cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
			Stopwatch watch = Stopwatch.StartNew();

			await Assert.ThrowsAnyAsync<OperationCanceledException>(
				() => this._runner.RunAsync(new ProcessRunRequest { FileName = "cmd.exe", Arguments = "/c ping -n 30 127.0.0.1 > nul" }, cancellation.Token));

			Assert.True(watch.Elapsed < TimeSpan.FromSeconds(15), "the process was not stopped");
		}

		[Fact]
		public async Task Run_ThrowsWhenTheProgramCannotBeStarted()
		{
			await Assert.ThrowsAsync<Win32Exception>(
				() => this._runner.RunAsync(new ProcessRunRequest { FileName = Path.Combine(this._workspace.Root, "does-not-exist.exe") }));
		}

		private sealed class LineCollector : IProgress<String>
		{
			private readonly List<String> _target;

			public LineCollector(List<String> target)
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
