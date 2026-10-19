namespace FalloutScriptStudio.Core.Compilation
{
	using System;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.IO;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using Building;

	/// <summary>
	/// <see cref="IBatchCompileService"/> implementation on top of <see cref="IScriptCompiler"/>.
	/// </summary>
	public sealed class BatchCompileService : IBatchCompileService
	{
		private static readonly TimeSpan _flushInterval = TimeSpan.FromSeconds(5);

		private readonly IScriptCompiler _compiler;
		private readonly IIncrementalBuildService? _incremental;

		/// <summary>
		/// Initializes a new instance of the <see cref="BatchCompileService"/> class.
		/// </summary>
		/// <param name="compiler">The compiler used for every script.</param>
		/// <param name="incremental">Tells which scripts are up to date and remembers the builds; without it every script is compiled and nothing is remembered.</param>
		public BatchCompileService(IScriptCompiler compiler, IIncrementalBuildService? incremental = null)
		{
			ArgumentNullException.ThrowIfNull(compiler);
			this._compiler = compiler;
			this._incremental = incremental;
		}

		/// <inheritdoc/>
		public IReadOnlyList<String> FindScripts(String directory)
		{
			ArgumentException.ThrowIfNullOrEmpty(directory);
			return Directory.GetFiles(directory, "*.ssl", SearchOption.AllDirectories);
		}

		/// <inheritdoc/>
		public async Task<BatchCompileResult> CompileAsync(
			IReadOnlyList<String> scripts,
			CompilerOptions compilerOptions,
			BatchCompileOptions? batchOptions = null,
			IProgress<BatchCompileProgress>? progress = null,
			CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(scripts);
			ArgumentNullException.ThrowIfNull(compilerOptions);

			BatchCompileOptions options = batchOptions ?? new BatchCompileOptions();
			Int32 found = scripts.Count;
			IIncrementalBuildService? incremental = options.BuildCacheFile is { Length: > 0 } ? this._incremental : null;
			String? cacheFile = incremental is null ? null : options.BuildCacheFile;

			ConcurrentQueue<BatchCompileFailure> failures = new ConcurrentQueue<BatchCompileFailure>();
			List<String> upToDate = [];
			Int32 compiled = 0;
			Int32 failed = 0;
			Int32 completed = 0;
			Boolean cancelled = false;
			Int32 threads = 1;

			try
			{
				IReadOnlyList<String> toCompile = scripts;
				if (incremental is not null && cacheFile is not null && options.OnlyChanged)
				{
					BuildPlan plan = await Task.Run(
						() => incremental.Plan(scripts.Select(script => new BuildCandidate(script)).ToList(), compilerOptions, cacheFile, false, cancellationToken),
						cancellationToken).ConfigureAwait(false);
					toCompile = plan.Stale.Select(decision => decision.ScriptPath).ToList();
					upToDate.AddRange(plan.UpToDate.Select(decision => decision.ScriptPath));
				}

				if (options.MultiThreaded)
				{
					threads = Math.Max(1, Math.Min(options.MaxDegreeOfParallelism ?? Environment.ProcessorCount, toCompile.Count));
				}

				Int32 total = toCompile.Count;
				Int32 skipped = upToDate.Count;
				if (skipped > 0)
				{
					// Tells the caller at once what is skipped (the run may take a while before the first script is finished).
					progress?.Report(new BatchCompileProgress(String.Empty, true, 0, 0, total) { UpToDate = skipped });
				}

				Stopwatch sinceFlush = Stopwatch.StartNew();
				Object flushLock = new Object();
				ParallelOptions parallelOptions = new ParallelOptions
				{
					MaxDegreeOfParallelism = threads,
					CancellationToken = cancellationToken,
				};

				try
				{
					await Parallel.ForEachAsync(toCompile, parallelOptions, async (script, token) =>
					{
						BuildTicket? ticket = cacheFile is null ? null : incremental?.Begin(script, compilerOptions, false, cacheFile);
						CompilationResult result;
						try
						{
							result = await this._compiler.CompileAsync(
								new CompilationRequest
								{
									ScriptPath = script,
									Options = compilerOptions,
									ShortCircuit = compilerOptions.ShortCircuit,
									Batch = true,
								},
								token).ConfigureAwait(false);
						}
						catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
						{
							result = new CompilationResult { Success = false, Output = ex.Message };
						}

						if (result.Success)
						{
							ticket?.Succeeded();
							Interlocked.Increment(ref compiled);
						}
						else
						{
							ticket?.Failed();
							Interlocked.Increment(ref failed);
							failures.Enqueue(new BatchCompileFailure(script, result.Output));
						}

						if (incremental is not null && cacheFile is not null && sinceFlush.Elapsed >= _flushInterval)
						{
							// A long run is saved from time to time: if the program stops, the finished scripts are not built again.
							lock (flushLock)
							{
								if (sinceFlush.Elapsed >= _flushInterval)
								{
									_ = incremental.Flush(cacheFile);
									sinceFlush.Restart();
								}
							}
						}

						Int32 done = Interlocked.Increment(ref completed);
						progress?.Report(new BatchCompileProgress(script, result.Success, done, Volatile.Read(ref failed), total) { UpToDate = skipped });
					}).ConfigureAwait(false);
				}
				finally
				{
					if (incremental is not null && cacheFile is not null)
					{
						_ = incremental.Flush(cacheFile);
					}
				}
			}
			catch (OperationCanceledException)
			{
				cancelled = true;
			}

			return new BatchCompileResult(found, compiled, failures.ToArray(), cancelled, threads) { UpToDateScripts = upToDate };
		}
	}
}