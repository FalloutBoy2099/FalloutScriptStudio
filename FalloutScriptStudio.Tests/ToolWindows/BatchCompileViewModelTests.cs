namespace FalloutScriptStudio.Tests.ToolWindows
{
	using System;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.ViewModels.ToolWindows;
	using Xunit;

	public sealed class BatchCompileViewModelTests
	{
		private static readonly String[] _scripts = { @"C:\s\a.ssl", @"C:\s\b.ssl", @"C:\s\c.ssl" };

		private static CompilerOptions CreateOptions()
		{
			return new CompilerOptions { ProgramDirectory = @"C:\p", ResourcesDirectory = @"C:\p\resources", ScratchDirectory = @"C:\p\scrTemp" };
		}

		[Theory]
		[InlineData(true, 8, 100, 8)]
		[InlineData(true, 8, 3, 3)]
		[InlineData(true, 2, 100, 2)]
		[InlineData(false, 8, 100, 1)]
		[InlineData(true, 8, 0, 1)]
		public void GetThreadCount_FollowsTheServiceRule(Boolean multiThreaded, Int32 maxDegree, Int32 count, Int32 expected)
		{
			BatchCompileOptions options = new BatchCompileOptions { MultiThreaded = multiThreaded, MaxDegreeOfParallelism = maxDegree };

			Assert.Equal(expected, BatchCompileViewModel.GetThreadCount(count, options));
		}

		[Fact]
		public async Task Run_ReportsProgressAndFinalResult()
		{
			FakeBatchCompileService service = new FakeBatchCompileService
			{
				Failures = new[] { new BatchCompileFailure(@"C:\s\b.ssl", "error: boom") },
			};
			BatchCompileViewModel viewModel = new BatchCompileViewModel(service);

			Task<BatchCompileResult> run = viewModel.RunAsync(_scripts, CreateOptions(), new BatchCompileOptions { MultiThreaded = false });
			Assert.True(viewModel.IsRunning);
			Assert.False(viewModel.IsFinished);
			Assert.Equal(3, viewModel.Total);
			Assert.Equal("Batch compilation", viewModel.Title);
			Assert.Contains("3 scripts found.", viewModel.LogText, StringComparison.Ordinal);

			viewModel.ReportProgress(new BatchCompileProgress(@"C:\s\a.ssl", true, 1, 0, 3));
			viewModel.ReportProgress(new BatchCompileProgress(@"C:\s\b.ssl", false, 2, 1, 3));
			Assert.Equal(2, viewModel.Completed);
			Assert.Equal(1, viewModel.Failed);
			Assert.Equal(1, viewModel.Compiled);
			Assert.Equal("b.ssl", viewModel.CurrentFile);
			Assert.Single(viewModel.Failures);
			Assert.Contains("Failed: b.ssl", viewModel.LogText, StringComparison.Ordinal);

			service.Release();
			BatchCompileResult result = await run;

			Assert.False(result.Cancelled);
			Assert.False(viewModel.IsRunning);
			Assert.True(viewModel.IsFinished);
			Assert.Equal(3, viewModel.Completed);
			Assert.Equal(2, viewModel.Compiled);
			Assert.Single(viewModel.Failures);
			Assert.Equal("error: boom", viewModel.Failures[0].Output);
			Assert.Same(viewModel.Failures[0], viewModel.SelectedFailure);
			Assert.Contains("2 successfully compiled.", viewModel.LogText, StringComparison.Ordinal);
			Assert.Contains("1 failed to compile.", viewModel.LogText, StringComparison.Ordinal);
			Assert.Contains("0 skipped.", viewModel.LogText, StringComparison.Ordinal);
		}

		[Fact]
		public async Task Run_AddsFailuresThatWereNotReportedByProgress()
		{
			FakeBatchCompileService service = new FakeBatchCompileService
			{
				Failures = new[] { new BatchCompileFailure(@"C:\s\c.ssl", "x"), new BatchCompileFailure(@"C:\s\a.ssl", "y") },
			};
			BatchCompileViewModel viewModel = new BatchCompileViewModel(service);

			Task<BatchCompileResult> run = viewModel.RunAsync(_scripts, CreateOptions());
			service.Release();
			await run;

			Assert.Equal("a.ssl", viewModel.Failures[0].FileName);
			Assert.Equal("c.ssl", viewModel.Failures[1].FileName);
			Assert.Equal("y", viewModel.Failures[0].Output);
		}

		[Fact]
		public async Task Cancel_StopsTheRunAndCountsSkippedScripts()
		{
			FakeBatchCompileService service = new FakeBatchCompileService();
			BatchCompileViewModel viewModel = new BatchCompileViewModel(service);

			Task<BatchCompileResult> run = viewModel.RunAsync(_scripts, CreateOptions());
			Assert.True(viewModel.CancelCommand.CanExecute(null));

			viewModel.CancelCommand.Execute(null);
			Assert.True(viewModel.IsCancelling);
			Assert.False(viewModel.CancelCommand.CanExecute(null));

			BatchCompileResult result = await run;

			Assert.True(result.Cancelled);
			Assert.False(viewModel.IsRunning);
			Assert.True(viewModel.IsFinished);
			Assert.Equal("Cancelled.", viewModel.StatusText);
			Assert.Contains("3 skipped.", viewModel.LogText, StringComparison.Ordinal);
		}

		[Fact]
		public async Task Run_ShowsThreadCountInTitle()
		{
			FakeBatchCompileService service = new FakeBatchCompileService();
			BatchCompileViewModel viewModel = new BatchCompileViewModel(service);

			Task<BatchCompileResult> run = viewModel.RunAsync(_scripts, CreateOptions(), new BatchCompileOptions { MultiThreaded = true, MaxDegreeOfParallelism = 2 });
			Assert.Equal("Batch compilation [Threads: 2]", viewModel.Title);

			service.Release();
			await run;
		}

		[Fact]
		public async Task ReportProgress_IsIgnoredAfterTheRunIsFinished()
		{
			FakeBatchCompileService service = new FakeBatchCompileService();
			BatchCompileViewModel viewModel = new BatchCompileViewModel(service);
			Task<BatchCompileResult> run = viewModel.RunAsync(_scripts, CreateOptions());
			service.Release();
			await run;

			viewModel.ReportProgress(new BatchCompileProgress(@"C:\s\z.ssl", false, 1, 1, 3));

			Assert.Empty(viewModel.Failures);
			Assert.Equal(3, viewModel.Completed);
		}

		[Fact]
		public void Close_RequestsClose()
		{
			BatchCompileViewModel viewModel = new BatchCompileViewModel(new FakeBatchCompileService());
			Boolean? closed = null;
			viewModel.CloseRequested += (_, accepted) => closed = accepted;

			viewModel.CloseCommand.Execute(null);

			Assert.True(closed);
		}
	}
}
