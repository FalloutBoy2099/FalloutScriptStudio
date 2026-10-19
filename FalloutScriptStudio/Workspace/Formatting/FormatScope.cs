namespace FalloutScriptStudio.Workspace.Formatting
{
	/// <summary>The files "Format Project" looks at.</summary>
	public enum FormatScope
	{
		/// <summary>The scripts and headers that are open in the editor.</summary>
		OpenDocuments = 0,

		/// <summary>The files of the project folder that match the masks.</summary>
		ProjectFolder = 1,
	}
}