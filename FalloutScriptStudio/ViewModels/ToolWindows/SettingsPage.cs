namespace FalloutScriptStudio.ViewModels.ToolWindows
{
	/// <summary>
	/// The pages of the settings window.
	/// </summary>
	public enum SettingsPage
	{
		/// <summary>Theme and editor font.</summary>
		Appearance = 0,

		/// <summary>Typing behaviour, completion and file encoding.</summary>
		Editor,

		/// <summary>The background script parser.</summary>
		Parser,

		/// <summary>Compiler and preprocessor.</summary>
		Compiler,

		/// <summary>Folders and files.</summary>
		Paths,

		/// <summary>Message files.</summary>
		Messages,

		/// <summary>The dialog flowchart.</summary>
		Flowchart,
	}
}