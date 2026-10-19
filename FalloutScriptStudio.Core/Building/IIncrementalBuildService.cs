namespace FalloutScriptStudio.Core.Building
{
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using Compilation;

	/// <summary>
	/// Decides which scripts have to be compiled again ("only recompile what changed") and remembers successful builds in the build cache.
	/// A script is up to date only when its compiled file exists and is the one the last successful build produced, from the same source,
	/// the same included files, the same options and the same tools; any doubt means "build again".
	/// </summary>
	public interface IIncrementalBuildService
	{
		/// <summary>Checks which of the scripts are stale. Nothing is compiled.</summary>
		/// <param name="candidates">The scripts to examine.</param>
		/// <param name="options">The compiler options of the build.</param>
		/// <param name="cacheFile">The build cache file (see <see cref="BuildCache.GetFilePath"/>).</param>
		/// <param name="force">Treat every script as stale (Rebuild All).</param>
		/// <param name="cancellationToken">Cancels the check.</param>
		/// <returns>One decision per script.</returns>
		BuildPlan Plan(IReadOnlyList<BuildCandidate> candidates, CompilerOptions options, String cacheFile, Boolean force = false, CancellationToken cancellationToken = default);

		/// <summary>Takes the state of a script and of its dependencies before its compilation starts.</summary>
		/// <param name="scriptPath">The script.</param>
		/// <param name="options">The compiler options of the compilation.</param>
		/// <param name="shortCircuit">Whether the script asks for short-circuit evaluation itself.</param>
		/// <param name="cacheFile">The build cache file.</param>
		/// <returns>The ticket to report the outcome with.</returns>
		BuildTicket Begin(String scriptPath, CompilerOptions options, Boolean shortCircuit, String cacheFile);

		/// <summary>Writes the build cache when it has unsaved changes. Failures are logged, never thrown.</summary>
		/// <param name="cacheFile">The build cache file.</param>
		/// <returns><see langword="true"/> when the file is up to date afterwards.</returns>
		Boolean Flush(String cacheFile);
	}
}