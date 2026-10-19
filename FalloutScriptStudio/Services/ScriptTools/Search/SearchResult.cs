namespace FalloutScriptStudio.Services.ScriptTools.Search
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// The outcome of a search run.
	/// </summary>
	/// <param name="Hits">The matching lines in the order they were found.</param>
	/// <param name="FilesSearched">The number of texts and files that were searched.</param>
	/// <param name="FilesWithMatches">The number of files that contain a match.</param>
	/// <param name="FilesSkipped">The number of files that could not be read or look binary.</param>
	/// <param name="Truncated">Whether the search stopped early because the limit of reported lines was reached.</param>
	public sealed record SearchResult(IReadOnlyList<SearchHit> Hits, Int32 FilesSearched, Int32 FilesWithMatches, Int32 FilesSkipped, Boolean Truncated);
}