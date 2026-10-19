namespace FalloutScriptStudio.Core.Compilation
{
	using System;

	/// <summary>
	/// Progress notification of a mass compilation, raised after every finished script.
	/// </summary>
	/// <param name="ScriptPath">The script that was just finished.</param>
	/// <param name="Succeeded">Whether that script compiled successfully.</param>
	/// <param name="Completed">The number of scripts finished so far.</param>
	/// <param name="Failed">The number of scripts that failed so far.</param>
	/// <param name="Total">The number of scripts to compile (the ones that are up to date are not counted).</param>
	public sealed record BatchCompileProgress(String ScriptPath, Boolean Succeeded, Int32 Completed, Int32 Failed, Int32 Total)
	{
		/// <summary>Gets the number of scripts that were skipped because they are up to date.</summary>
		public Int32 UpToDate { get; init; }
	}
}