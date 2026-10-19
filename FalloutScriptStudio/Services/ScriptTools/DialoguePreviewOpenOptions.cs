namespace FalloutScriptStudio.Services.ScriptTools
{
	using System;

	/// <summary>How the dialogue preview window is opened.</summary>
	public sealed record DialoguePreviewOpenOptions
	{
		/// <summary>
		/// Gets the name of the node to start the dialogue at ("Preview Dialogue From This Node"), instead of the node at the caret or
		/// <c>talk_p_proc</c>. A window that is already open for the script is moved to that node (Back returns to where it was).
		/// </summary>
		public String? StartAtNode { get; init; }
	}
}