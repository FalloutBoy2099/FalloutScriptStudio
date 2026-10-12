namespace FalloutScriptStudio.Core.Tests.Compilation
{
	using System;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;
	using Xunit;

	/// <summary>The mass compilation with several threads: a cancel reaches every compilation in flight and nothing starts after it.</summary>
	public sealed class BatchCompileCancellationTests : IDisposable
	{
		private readonly TempWorkspace _workspace = new TempWorkspace();

		public void Dispose()
		{
			this._workspace.Dispose();
		}

		[Fact]
		public async Task Cancelling_stops_every_compilation_in_flight_and_starts_no_other()
		{
			using CancellationTokenSource cancellation = new CancellationTokenSource();
			HangingCompiler compiler = new HangingCompiler(expectedInFlight: 4);
			BatchCompileService service = new BatchCompileService(compiler);
			String[] scripts = Enumerable.Range(0, 20).Select(index => $"s{index}.ssl").ToArray();

			Task<BatchCompileResult> run = service.CompileAsync(
				scripts,
				this._workspace.CreateOptions(),
				new BatchCompileOptions { MultiThreaded = true, MaxDegreeOfParallelism = 4 },
				null,
				cancellation.Token);
			await compiler.AllStarted.WaitAsync(TimeSpan.FromSeconds(20));
			await cancellation.CancelAsync();
			BatchCompileResult result = await run.WaitAsync(TimeSpan.FromSeconds(20));

			Assert.True(result.Cancelled);
			Assert.Equal(4, compiler.Started);
			Assert.Equal(4, compiler.Cancelled);
			Assert.Equal(0, result.Compiled);
			Assert.Equal(0, result.Failed);
			Assert.Equal(20, result.Skipped);
		}

		[Fact]
		public async Task Scripts_finished_before_the_cancel_keep_their_result()
		{
			using CancellationTokenSource cancellation = new CancellationTokenSource();
			HangingCompiler compiler = new HangingCompiler(expectedInFlight: 1) { QuickScripts = 3 };
			BatchCompileService service = new BatchCompileService(compiler);
			String[] scripts = Enumerable.Range(0, 10).Select(index => $"s{index}.ssl").ToArray();

			Task<BatchCompileResult> run = service.CompileAsync(
				scripts,
				this._workspace.CreateOptions(),
				new BatchCompileOptions { MultiThreaded = false },
				null,
				cancellation.Token);
			await compiler.AllStarted.WaitAsync(TimeSpan.FromSeconds(20));
			await cancellation.CancelAsync();
			BatchCompileResult result = await run.WaitAsync(TimeSpan.FromSeconds(20));

			Assert.True(result.Cancelled);
			Assert.Equal(3, result.Compiled);
			Assert.Equal(7, result.Skipped);
		}

		private sealed class HangingCompiler : IScriptCompiler
		{
			private readonly Int32 _expectedInFlight;
			private readonly TaskCompletionSource _allStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
			private Int32 _started;
			private Int32 _cancelled;
			private Int32 _inFlight;

			public HangingCompiler(Int32 expectedInFlight)
			{
				this._expectedInFlight = expectedInFlight;
			}

			public Int32 QuickScripts { get; init; }

			public Task AllStarted => this._allStarted.Task;

			public Int32 Started => Volatile.Read(ref this._started);

			public Int32 Cancelled => Volatile.Read(ref this._cancelled);

			public async Task<CompilationResult> CompileAsync(CompilationRequest request, CancellationToken cancellationToken = default)
			{
				Int32 number = Interlocked.Increment(ref this._started);
				if (number <= this.QuickScripts)
				{
					return new CompilationResult { Success = true, Output = String.Empty };
				}

				if (Interlocked.Increment(ref this._inFlight) == this._expectedInFlight)
				{
					this._allStarted.TrySetResult();
				}

				try
				{
					await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
				}
				catch (OperationCanceledException)
				{
					Interlocked.Increment(ref this._cancelled);
					throw;
				}

				return new CompilationResult { Success = true, Output = String.Empty };
			}

			public Task<CompilationResult> PreprocessAsync(CompilationRequest request, CancellationToken cancellationToken = default)
			{
				throw new NotSupportedException();
			}

			public Task<DecompilationResult> DecompileAsync(DecompilationRequest request, IProgress<String>? outputProgress = null, CancellationToken cancellationToken = default)
			{
				throw new NotSupportedException();
			}

			public String GetOutputPath(String scriptPath, CompilerOptions options, Boolean temporaryOutput = false)
			{
				return scriptPath;
			}
		}
	}
}
