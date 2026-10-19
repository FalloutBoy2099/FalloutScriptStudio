namespace FalloutScriptStudio.Services.ScriptTools.Replace
{
	/// <summary>How a "Replace in Files" run ended.</summary>
	public enum ReplaceOutcomeKind
	{
		/// <summary>Nothing matched (or every match is in a file that is left out).</summary>
		NothingFound,

		/// <summary>The user closed the preview without replacing.</summary>
		Cancelled,

		/// <summary>The replacements were applied (some files may have been left unchanged: see the result).</summary>
		Applied,
	}
}