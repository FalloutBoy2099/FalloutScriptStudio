namespace FalloutScriptStudio.Services.ScriptTools.Search
{
	/// <summary>Where the advanced search looks.</summary>
	public enum SearchScope
	{
		/// <summary>The active script only.</summary>
		CurrentScript,

		/// <summary>All open scripts (their current text, including unsaved edits).</summary>
		OpenScripts,

		/// <summary>The files of a folder.</summary>
		Folder,
	}
}
