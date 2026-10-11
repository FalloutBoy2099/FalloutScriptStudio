namespace FalloutScriptStudio.Tests.Shell.Support
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;

	/// <summary>A batch compile service that lists the real files but compiles nothing.</summary>
	internal sealed class FakeBatchCompileService : IBatchCompileService
	{
		public IReadOnlyList<String> FindScripts(String directory)
		{
			return Directory.GetFiles(directory, "*.ssl", SearchOption.AllDirectories).ToList();
		}

		public Task<BatchCompileResult> CompileAsync(
			IReadOnlyList<String> scripts,
			CompilerOptions compilerOptions,
			BatchCompileOptions? batchOptions = null,
			IProgress<BatchCompileProgress>? progress = null,
			CancellationToken cancellationToken = default)
		{
			return Task.FromResult(new BatchCompileResult(scripts.Count, scripts.Count, Array.Empty<BatchCompileFailure>(), false, 1));
		}
	}
}
