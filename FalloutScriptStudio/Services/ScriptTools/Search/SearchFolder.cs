namespace FalloutScriptStudio.Services.ScriptTools.Search
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// A folder to search.
	/// </summary>
	/// <param name="Directory">The folder.</param>
	/// <param name="FileMasks">The file masks (<c>*.ssl</c>); see <see cref="SearchFileMasks.Parse"/>.</param>
	/// <param name="IncludeSubfolders">Whether sub folders are searched too.</param>
	/// <param name="OpenTexts">The current texts of open documents keyed by full path; they are searched instead of the (possibly outdated) file on disk.</param>
	public sealed record SearchFolder(String Directory, IReadOnlyList<String> FileMasks, Boolean IncludeSubfolders, IReadOnlyDictionary<String, String> OpenTexts);
}