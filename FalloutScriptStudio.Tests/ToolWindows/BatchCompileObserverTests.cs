namespace FalloutScriptStudio.Tests.ToolWindows
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.ViewModels.ToolWindows;
	using Xunit;

	/// <summary>The status bar of the main window follows a mass compilation through the observer of the batch window, and can cancel it.</summary>
	public sealed class BatchCompileObserverTests
	{
		private static CompilerOptions CreateOptions()
		{
			return new CompilerOptions { ProgramDirectory = @"C:\p", ResourcesDirectory = @"C:\p\resources", ScratchDirectory = @"C:\p\scrTemp" };
		}

		[Fact]
		public async Task The_observer_receives_every_finished_script_as_well_as_the_window()
		{
			ReportingService service = new ReportingService();
			BatchCompileViewModel viewModel = new BatchCompileViewModel(service);
			List<BatchCompileProgress> seen = [];

			BatchCompileResult result = await viewModel.RunAsync(new[] { @"C:\s\a.ssl", @"C:\s\b.ssl" }, CreateOptions(), new BatchCompileOptions { MultiThreaded = false }, new ListProgress(seen));
			Assert.True(await Task.Run(() => SpinWait.SpinUntil(() => { lock (seen) { return seen.Count == 2; } }, TimeSpan.FromSeconds(10))));

			Assert.False(result.Cancelled);
			lock (seen)
			{
				Assert.Equal(new[] { @"C:\s\a.ssl", @"C:\s\b.ssl" }, seen.Select(item => item.ScriptPath).Order());
			}

			Assert.Equal(2, viewModel.Completed);
		}

		[Fact]
		public async Task Requesting_the_cancel_of_a_running_batch_cancels_the_token_the_service_got()
		{
			ReportingService service = new ReportingService { Hang = true };
			BatchCompileViewModel viewModel = new BatchCompileViewModel(service);

			Task<BatchCompileResult> run = viewModel.RunAsync(new[] { @"C:\s\a.ssl" }, CreateOptions());
			Assert.True(await Task.Run(() => SpinWait.SpinUntil(() => service.Started, TimeSpan.FromSeconds(10))));
			viewModel.RequestCancel();
			BatchCompileResult result = await run;

			Assert.True(result.Cancelled);
			Assert.True(service.TokenWasCancelled);
			Assert.False(viewModel.IsRunning);
			Assert.True(viewModel.CanClose);
		}

		private sealed class ListProgress : IProgress<BatchCompileProgress>
		{
			private readonly List<BatchCompileProgress> _target;

			public ListProgress(List<BatchCompileProgress> target)
			{
				this._target = target;
			}

			public void Report(BatchCompileProgress value)
			{
				lock (this._target)
				{
					this._target.Add(value);
				}
			}
		}

		private sealed class ReportingService : IBatchCompileService
		{
			public Boolean Hang { get; init; }

			public Boolean Started { get; private set; }

			public Boolean TokenWasCancelled { get; private set; }

			public IReadOnlyList<String> FindScripts(String directory)
			{
				return Array.Empty<String>();
			}

			public async Task<BatchCompileResult> CompileAsync(
				IReadOnlyList<String> scripts,
				CompilerOptions compilerOptions,
				BatchCompileOptions? batchOptions = null,
				IProgress<BatchCompileProgress>? progress = null,
				CancellationToken cancellationToken = default)
			{
				this.Started = true;
				if (this.Hang)
				{
					try
					{
						await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
					}
					catch (OperationCanceledException)
					{
						this.TokenWasCancelled = true;
						return new BatchCompileResult(scripts.Count, 0, Array.Empty<BatchCompileFailure>(), true, 1);
					}
				}

				for (Int32 index = 0; index < scripts.Count; index++)
				{
					progress?.Report(new BatchCompileProgress(scripts[index], true, index + 1, 0, scripts.Count));
				}

				return new BatchCompileResult(scripts.Count, scripts.Count, Array.Empty<BatchCompileFailure>(), false, 1);
			}
		}
	}
}