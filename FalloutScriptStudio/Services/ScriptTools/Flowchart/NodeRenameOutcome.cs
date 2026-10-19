namespace FalloutScriptStudio.Services.ScriptTools.Flowchart
{
	/// <summary>
	/// How the renaming of a dialogue node ended.
	/// </summary>
	public enum NodeRenameOutcome
	{
		/// <summary>The node does not exist in the script.</summary>
		NotFound = 0,

		/// <summary>The user cancelled or nothing changed.</summary>
		Cancelled,

		/// <summary>The node was renamed.</summary>
		Renamed,

		/// <summary>The new name does not contain the word <c>Node</c>; the rename was undone.</summary>
		Rejected,
	}
}