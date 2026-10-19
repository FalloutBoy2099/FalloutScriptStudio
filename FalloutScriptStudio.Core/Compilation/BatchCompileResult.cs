namespace FalloutScriptStudio.Core.Compilation
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// Summary of a mass compilation.
	/// </summary>
	/// <param name="Found">The number of scripts that were looked at (compiled, failed, skipped as up to date or not reached).</param>
	/// <param name="Compiled">The number of scripts that compiled successfully.</param>
	/// <param name="Failures">The scripts that failed.</param>
	/// <param name="Cancelled">Whether the run was cancelled before all scripts were processed.</param>
	/// <param name="Threads">The number of parallel compilations that were used.</param>
	public sealed record BatchCompileResult(Int32 Found, Int32 Compiled, IReadOnlyList<BatchCompileFailure> Failures, Boolean Cancelled, Int32 Threads)
	{
		/// <summary>Gets the number of scripts that failed to compile.</summary>
		public Int32 Failed => this.Failures.Count;

		/// <summary>Gets the number of scripts that were skipped because they were up to date (only when the run was asked to skip them).</summary>
		public Int32 UpToDate => this.UpToDateScripts.Count;

		/// <summary>Gets the scripts that were skipped because they were up to date.</summary>
		public IReadOnlyList<String> UpToDateScripts { get; init; } = Array.Empty<String>();

		/// <summary>Gets the number of scripts that were not processed (because the run was cancelled).</summary>
		public Int32 Skipped => this.Found - this.Compiled - this.Failed - this.UpToDate;
	}
}