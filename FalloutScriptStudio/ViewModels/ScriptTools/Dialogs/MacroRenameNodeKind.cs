namespace FalloutScriptStudio.ViewModels.ScriptTools.Dialogs
{
	/// <summary>
	/// The level of a node of the macro rename preview tree.
	/// </summary>
	public enum MacroRenameNodeKind
	{
		/// <summary>A group of files: the open scripts or the files of the project folder.</summary>
		Section,

		/// <summary>A file with at least one match.</summary>
		File,

		/// <summary>One occurrence of the macro name.</summary>
		Match,
	}
}
