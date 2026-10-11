namespace FalloutScriptStudio.ViewModels.MessageEditor
{
	/// <summary>
	/// How serious a structure problem of a message row is. The values are ordered by severity.
	/// </summary>
	public enum MessageIssueSeverity
	{
		/// <summary>No problem.</summary>
		None = 0,

		/// <summary>A style hint or a probably missing comment mark.</summary>
		Hint,

		/// <summary>A warning, for example a duplicate message number.</summary>
		Warning,

		/// <summary>An error that breaks the message file.</summary>
		Error,
	}
}
