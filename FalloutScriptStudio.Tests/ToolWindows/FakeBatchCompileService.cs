namespace FalloutScriptStudio.Tests.ToolWindows
{
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;

	internal sealed class FakeBatchCompileService : IBatchCompileService
	{
		private readonly TaskCompletionSource<Boolean> _gate = new TaskCompletionSource<Boolean>(TaskCreationOptions.RunContinuationsAsynchronously);

		public IReadOnlyList<BatchCompileFailure> Failures { get; set; } = Array.Empty<BatchCompileFailure>();

		public Boolean Started { get; private set; }

		public BatchCompileOptions? ReceivedOptions { get; private set; }

		public IReadOnlyList<String> FindScripts(String directory)
		{
			return Array.Empty<String>();
		}

		public void Release()
		{
			this._gate.TrySetResult(true);
		}

		public async Task<BatchCompileResult> CompileAsync(
			IReadOnlyList<String> scripts,
			CompilerOptions compilerOptions,
			BatchCompileOptions? batchOptions = null,
			IProgress<BatchCompileProgress>? progress = null,
			CancellationToken cancellationToken = default)
		{
			this.Started = true;
			this.ReceivedOptions = batchOptions;

			try
			{
				await this._gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				return new BatchCompileResult(scripts.Count, 0, Array.Empty<BatchCompileFailure>(), true, 1);
			}

			return new BatchCompileResult(scripts.Count, scripts.Count - this.Failures.Count, this.Failures, false, 1);
		}
	}
}
