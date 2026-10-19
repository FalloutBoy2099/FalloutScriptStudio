namespace FalloutScriptStudio.Tests.ToolWindows
{
	using System;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.ViewModels.ToolWindows;
	using FalloutScriptStudio.Workspace;
	using Xunit;

	/// <summary>The scripts a mass compilation skips because they are up to date, in the batch window and the status bar.</summary>
	public sealed class BatchCompileUpToDateTests
	{
		private static readonly String[] _scripts = { @"C:\s\a.ssl", @"C:\s\b.ssl", @"C:\s\c.ssl", @"C:\s\d.ssl" };

		private static CompilerOptions CreateOptions()
		{
			return new CompilerOptions { ProgramDirectory = @"C:\p", ResourcesDirectory = @"C:\p\resources", ScratchDirectory = @"C:\p\scrTemp" };
		}

		[Fact]
		public async Task The_window_counts_only_the_scripts_it_compiles_and_names_the_ones_it_skipped()
		{
			FakeBatchCompileService service = new FakeBatchCompileService { UpToDate = new[] { @"C:\s\c.ssl", @"C:\s\d.ssl" } };
			BatchCompileViewModel viewModel = new BatchCompileViewModel(service);

			Task<BatchCompileResult> run = viewModel.RunAsync(_scripts, CreateOptions(), new BatchCompileOptions { MultiThreaded = false });
			viewModel.ReportProgress(new BatchCompileProgress(String.Empty, true, 0, 0, 2) { UpToDate = 2 });

			Assert.Equal(2, viewModel.Total);
			Assert.Equal(2, viewModel.UpToDate);
			Assert.True(viewModel.HasUpToDate);
			Assert.Equal("Up to date: 2", viewModel.UpToDateText);
			Assert.Equal(String.Empty, viewModel.CurrentFile);
			Assert.Empty(viewModel.Failures);

			service.Release();
			BatchCompileResult result = await run;

			Assert.Equal(2, result.UpToDate);
			Assert.Equal(2, viewModel.Total);
			Assert.Equal(2, viewModel.Completed);
			Assert.Equal(2, viewModel.Compiled);
			Assert.Contains("Up to date, not compiled: 2", viewModel.LogText, StringComparison.Ordinal);
			Assert.Contains("0 skipped.", viewModel.LogText, StringComparison.Ordinal);
			Assert.True(viewModel.IsFinished);
		}

		[Fact]
		public async Task A_run_without_skipped_scripts_looks_as_before()
		{
			FakeBatchCompileService service = new FakeBatchCompileService();
			BatchCompileViewModel viewModel = new BatchCompileViewModel(service);

			Task<BatchCompileResult> run = viewModel.RunAsync(_scripts, CreateOptions());
			service.Release();
			await run;

			Assert.False(viewModel.HasUpToDate);
			Assert.Equal(4, viewModel.Total);
			Assert.DoesNotContain("Up to date", viewModel.LogText, StringComparison.Ordinal);
		}

		[Fact]
		public void The_result_does_not_count_a_skipped_script_as_unprocessed()
		{
			BatchCompileResult result = new BatchCompileResult(10, 3, Array.Empty<BatchCompileFailure>(), true, 2) { UpToDateScripts = new[] { "a", "b", "c", "d" } };

			Assert.Equal(4, result.UpToDate);
			Assert.Equal(3, result.Skipped);
			Assert.Equal(0, new BatchCompileResult(2, 2, Array.Empty<BatchCompileFailure>(), false, 1).UpToDate);
		}

		[Fact]
		public void The_operation_tracker_keeps_the_number_of_skipped_scripts()
		{
			OperationTracker tracker = new OperationTracker();
			using IOperation operation = tracker.TryBegin(OperationKind.MassCompile, String.Empty, 20)!;

			operation.Report("a.ssl", 3, 12, 9);

			OperationInfo info = tracker.Current!;
			Assert.Equal(9, info.UpToDate);
			Assert.Equal(12, info.Total);
			Assert.Equal(0.25, info.Fraction);

			operation.Report("b.ssl", 4);
			Assert.Equal(0, tracker.Current!.UpToDate);
			Assert.Equal(12, tracker.Current.Total);
		}
	}
}