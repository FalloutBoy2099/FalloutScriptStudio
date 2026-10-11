namespace FalloutScriptStudio.Core.Tests.Compilation
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;
	using Xunit;

	public sealed class BatchCompileServiceTests : IDisposable
	{
		private readonly TempWorkspace _workspace = new TempWorkspace();

		public void Dispose()
		{
			this._workspace.Dispose();
		}

		[Fact]
		public async Task Compile_CountsSuccessesAndFailuresAndReportsProgress()
		{
			StubCompiler compiler = new StubCompiler(script => !script.EndsWith("b.ssl", StringComparison.Ordinal));
			BatchCompileService service = new BatchCompileService(compiler);
			List<BatchCompileProgress> progress = new List<BatchCompileProgress>();
			String[] scripts = { "a.ssl", "b.ssl", "c.ssl", "d.ssl" };

			BatchCompileResult result = await service.CompileAsync(
				scripts,
				this._workspace.CreateOptions(),
				new BatchCompileOptions { MultiThreaded = false },
				new ListProgress(progress));

			Assert.Equal(4, result.Found);
			Assert.Equal(3, result.Compiled);
			Assert.Equal(1, result.Failed);
			Assert.Equal(0, result.Skipped);
			Assert.False(result.Cancelled);
			Assert.Equal(1, result.Threads);
			Assert.Equal("b.ssl", Assert.Single(result.Failures).ScriptPath);
			Assert.Equal(4, progress.Count);
			Assert.Equal(new[] { 1, 2, 3, 4 }, progress.Select(item => item.Completed).OrderBy(value => value));
			Assert.All(progress, item => Assert.Equal(4, item.Total));
			Assert.Equal(1, progress.Max(item => item.Failed));
			Assert.Equal(1, compiler.MaxConcurrency);
			Assert.All(compiler.Requests, request => Assert.True(request.Batch));
		}

		[Fact]
		public async Task Compile_UsesAtMostTheConfiguredNumberOfParallelCompilations()
		{
			StubCompiler compiler = new StubCompiler(_ => true) { Delay = TimeSpan.FromMilliseconds(30) };
			BatchCompileService service = new BatchCompileService(compiler);
			String[] scripts = Enumerable.Range(0, 12).Select(index => $"s{index}.ssl").ToArray();

			BatchCompileResult result = await service.CompileAsync(
				scripts,
				this._workspace.CreateOptions(),
				new BatchCompileOptions { MultiThreaded = true, MaxDegreeOfParallelism = 3 });

			Assert.Equal(3, result.Threads);
			Assert.Equal(12, result.Compiled);
			Assert.InRange(compiler.MaxConcurrency, 1, 3);
		}

		[Fact]
		public async Task Compile_CancellationStopsAndReportsSkippedScripts()
		{
			using CancellationTokenSource cancellation = new CancellationTokenSource();
			StubCompiler compiler = new StubCompiler(_ => true);
			compiler.OnCompiled = () => cancellation.Cancel();
			BatchCompileService service = new BatchCompileService(compiler);
			String[] scripts = Enumerable.Range(0, 10).Select(index => $"s{index}.ssl").ToArray();

			BatchCompileResult result = await service.CompileAsync(
				scripts,
				this._workspace.CreateOptions(),
				new BatchCompileOptions { MultiThreaded = false },
				null,
				cancellation.Token);

			Assert.True(result.Cancelled);
			Assert.True(result.Skipped > 0);
			Assert.Equal(10, result.Found);
		}

		[Fact]
		public async Task Compile_ExceptionsFromTheFileSystemCountAsFailures()
		{
			StubCompiler compiler = new StubCompiler(_ => true) { ThrowIo = true };
			BatchCompileService service = new BatchCompileService(compiler);

			BatchCompileResult result = await service.CompileAsync(new[] { "a.ssl" }, this._workspace.CreateOptions());

			Assert.Equal(1, result.Failed);
			Assert.Equal("disk is full", result.Failures[0].Output);
		}

		[Fact]
		public void FindScripts_ReturnsSslFilesRecursively()
		{
			this._workspace.CreateFile(@"scripts\a.ssl");
			this._workspace.CreateFile(@"scripts\sub\b.ssl");
			this._workspace.CreateFile(@"scripts\sub\c.int");

			IReadOnlyList<String> found = new BatchCompileService(new StubCompiler(_ => true)).FindScripts(this._workspace.Scripts);

			Assert.Equal(2, found.Count);
			Assert.Contains(Path.Combine(this._workspace.Scripts, "a.ssl"), found);
			Assert.Contains(Path.Combine(this._workspace.Scripts, "sub", "b.ssl"), found);
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

		private sealed class StubCompiler : IScriptCompiler
		{
			private readonly Func<String, Boolean> _succeeds;
			private readonly List<CompilationRequest> _requests = new List<CompilationRequest>();
			private Int32 _running;
			private Int32 _maxConcurrency;

			public StubCompiler(Func<String, Boolean> succeeds)
			{
				this._succeeds = succeeds;
			}

			public TimeSpan Delay { get; set; }

			public Boolean ThrowIo { get; set; }

			public Action? OnCompiled { get; set; }

			public Int32 MaxConcurrency => this._maxConcurrency;

			public IReadOnlyList<CompilationRequest> Requests
			{
				get
				{
					lock (this._requests)
					{
						return this._requests.ToArray();
					}
				}
			}

			public async Task<CompilationResult> CompileAsync(CompilationRequest request, CancellationToken cancellationToken = default)
			{
				cancellationToken.ThrowIfCancellationRequested();
				lock (this._requests)
				{
					this._requests.Add(request);
				}

				Int32 running = Interlocked.Increment(ref this._running);
				Int32 observed;
				do
				{
					observed = this._maxConcurrency;
				}
				while (running > observed && Interlocked.CompareExchange(ref this._maxConcurrency, running, observed) != observed);

				try
				{
					if (this.Delay > TimeSpan.Zero)
					{
						await Task.Delay(this.Delay, cancellationToken);
					}

					if (this.ThrowIo)
					{
						throw new IOException("disk is full");
					}

					this.OnCompiled?.Invoke();
					Boolean success = this._succeeds(request.ScriptPath);
					return new CompilationResult { Success = success, Output = success ? "ok" : "failed" };
				}
				finally
				{
					Interlocked.Decrement(ref this._running);
				}
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
				throw new NotSupportedException();
			}
		}
	}
}
