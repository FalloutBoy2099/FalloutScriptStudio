namespace FalloutScriptStudio.Services.ScriptTools.Search
{
	using System;

	/// <summary>
	/// The progress of a search run.
	/// </summary>
	/// <param name="FilesProcessed">The number of files searched so far.</param>
	/// <param name="FilesTotal">The total number of files; 0 while the files are still being listed.</param>
	/// <param name="MatchingLines">The number of matching lines found so far.</param>
	public sealed record SearchProgress(Int32 FilesProcessed, Int32 FilesTotal, Int32 MatchingLines);
}