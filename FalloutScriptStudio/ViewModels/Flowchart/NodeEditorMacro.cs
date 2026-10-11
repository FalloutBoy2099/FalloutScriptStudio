namespace FalloutScriptStudio.ViewModels.Flowchart
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// A statement the node code editor can paste (the "Quick Macros" menu of the legacy editor). The placeholders in braces are
	/// selected after the statement is pasted.
	/// </summary>
	/// <param name="Header">The text of the menu item.</param>
	/// <param name="Template">The statement, with <c>{msg_num}</c>, <c>{node}</c> and <c>{iq}</c> placeholders.</param>
	/// <param name="StartsGroup">Whether a separator is drawn in front of the item.</param>
	public sealed record NodeEditorMacro(String Header, String Template, Boolean StartsGroup = false)
	{
		/// <summary>Gets the macros in menu order.</summary>
		public static IReadOnlyList<NodeEditorMacro> All { get; } = new[]
		{
			new NodeEditorMacro("Reply", "Reply({msg_num});"),
			new NodeEditorMacro("Messages", "NMessage({msg_num});"),
			new NodeEditorMacro("Option", "NOption({msg_num}, {node}, {iq});", true),
			new NodeEditorMacro("LowOption", "NLowOption({msg_num},  {node});"),
			new NodeEditorMacro("mstr", "mstr({msg_num})", true),
			new NodeEditorMacro("g_mstr", "g_mstr({msg_num})"),
			new NodeEditorMacro("Call node", "call {node};", true),
		};
	}
}
