namespace FalloutScriptStudio.Core.Compilation
{
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>
	/// Compiles many scripts in one run.
	/// </summary>
	public interface IBatchCompileService
	{
		/// <summary>
		/// Finds all <c>.ssl</c> scripts below a folder.
		/// </summary>
		/// <param name="directory">The folder to search recursively.</param>
		/// <returns>The full paths of the scripts.</returns>
		IReadOnlyList<String> FindScripts(String directory);

		/// <summary>
		/// Compiles the given scripts.
		/// </summary>
		/// <param name="scripts">The scripts to compile.</param>
		/// <param name="compilerOptions">The compiler options.</param>
		/// <param name="batchOptions">Parallelism options; defaults are used when <see langword="null"/>.</param>
		/// <param name="progress">Receives a notification after every script.</param>
		/// <param name="cancellationToken">Cancels the run; the result then has <see cref="BatchCompileResult.Cancelled"/> set.</param>
		/// <returns>The summary of the run.</returns>
		Task<BatchCompileResult> CompileAsync(
			IReadOnlyList<String> scripts,
			CompilerOptions compilerOptions,
			BatchCompileOptions? batchOptions = null,
			IProgress<BatchCompileProgress>? progress = null,
			CancellationToken cancellationToken = default);
	}
}
