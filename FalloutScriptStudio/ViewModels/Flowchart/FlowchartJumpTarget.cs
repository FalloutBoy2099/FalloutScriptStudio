namespace FalloutScriptStudio.ViewModels.Flowchart
{
	using System;

	/// <summary>
	/// An entry of the "jump to node" menu of the flowchart.
	/// </summary>
	/// <param name="Name">The node name.</param>
	/// <param name="IsHidden">Whether the node is hidden on the canvas (it is shown again when the user jumps to it).</param>
	public sealed record FlowchartJumpTarget(String Name, Boolean IsHidden);
}
