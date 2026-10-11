namespace FalloutScriptStudio.ViewModels.MessageEditor
{
	/// <summary>
	/// The kind of audible feedback the editor asks the platform for.
	/// </summary>
	public enum MessageEditorBeep
	{
		/// <summary>The requested action was not possible (for example the next message number is taken).</summary>
		Refused,

		/// <summary>Nothing was found.</summary>
		NotFound,
	}
}
