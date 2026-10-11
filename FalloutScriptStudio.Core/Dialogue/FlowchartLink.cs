namespace FalloutScriptStudio.Core.Dialogue
{
	using System;

	/// <summary>
	/// A link from a line of one node to another node (legacy <c>LinkTo</c>): the arrow the flowchart draws.
	/// </summary>
	/// <param name="TargetNode">The name of the node the link goes to.</param>
	/// <param name="ContentLine">The 1-based index of the line in <see cref="FlowchartNodeData.Content"/> the arrow starts at.</param>
	public sealed record FlowchartLink(String TargetNode, Int32 ContentLine);
}
