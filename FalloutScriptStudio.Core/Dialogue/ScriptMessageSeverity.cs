namespace FalloutScriptStudio.Core.Dialogue
{
	/// <summary>How serious a <see cref="ScriptMessageFinding"/> is.</summary>
	public enum ScriptMessageSeverity
	{
		/// <summary>The game would show a missing text, or the file is broken.</summary>
		Error = 0,

		/// <summary>Probably a mistake.</summary>
		Warning,

		/// <summary>A note.</summary>
		Info,
	}
}