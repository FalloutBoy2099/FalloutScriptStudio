namespace FalloutScriptStudio.Core.Compilation
{
	using System;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>
	/// <see cref="IBatchCompileService"/> implementation on top of <see cref="IScriptCompiler"/>.
	/// </summary>
	public sealed class BatchCompileService : IBatchCompileService
	{
		private readonly IScriptCompiler _compiler;

		/// <summary>
		/// Initializes a new instance of the <see cref="BatchCompileService"/> class.
		/// </summary>
		/// <param name="compiler">The compiler used for every script.</param>
		public BatchCompileService(IScriptCompiler compiler)
		{
			ArgumentNullException.ThrowIfNull(compiler);
			this._compiler = compiler;
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
			Int32 threads = 1;
			if (options.MultiThreaded)
			{
				threads = Math.Max(1, Math.Min(options.MaxDegreeOfParallelism ?? Environment.ProcessorCount, found));
			}

			ConcurrentQueue<BatchCompileFailure> failures = new ConcurrentQueue<BatchCompileFailure>();
			Int32 compiled = 0;
			Int32 failed = 0;
			Int32 completed = 0;
			Boolean cancelled = false;

			ParallelOptions parallelOptions = new ParallelOptions
			{
				MaxDegreeOfParallelism = threads,
				CancellationToken = cancellationToken,
			};

			try
			{
				await Parallel.ForEachAsync(scripts, parallelOptions, async (script, token) =>
				{
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
						Interlocked.Increment(ref compiled);
					}
					else
					{
						Interlocked.Increment(ref failed);
						failures.Enqueue(new BatchCompileFailure(script, result.Output));
					}

					Int32 done = Interlocked.Increment(ref completed);
					progress?.Report(new BatchCompileProgress(script, result.Success, done, Volatile.Read(ref failed), found));
				}).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				cancelled = true;
			}

			return new BatchCompileResult(found, compiled, failures.ToArray(), cancelled, threads);
		}
	}
}
