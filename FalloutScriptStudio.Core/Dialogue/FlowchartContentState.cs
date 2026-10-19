namespace FalloutScriptStudio.Core.Dialogue
{
	using System;

	/// <summary>
	/// The collapsed state of one message block of a node in a saved flowchart (the <c>&lt;Content&gt;</c> element of a <c>.fcd</c> file).
	/// </summary>
	/// <param name="Line">The identifier the legacy canvas gave the block: the index of the message block in the node, as text.</param>
	/// <param name="Collapsed">Whether the block is collapsed.</param>
	public sealed record FlowchartContentState(String Line, Boolean Collapsed);
}