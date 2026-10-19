namespace FalloutScriptStudio.Core.Configuration
{
	/// <summary>
	/// What happened to the project file of the active project outside of the editor.
	/// </summary>
	public enum ProjectFileChangeKind
	{
		/// <summary>The file was edited and its new content is valid: it is applied.</summary>
		Modified,

		/// <summary>The file was edited and its content is not valid (after a few retries, so it is not a half-written file): the last valid settings stay in effect.</summary>
		Invalid,

		/// <summary>The file was deleted: the project is a folder without settings again.</summary>
		Deleted,
	}
}