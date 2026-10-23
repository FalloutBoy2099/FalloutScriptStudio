namespace FalloutScriptStudio.Core.Dialogue
{
	using System;

	/// <summary>An edge from a node to another node.</summary>
	/// <param name="Target">The name of the target node as defined.</param>
	/// <param name="Line">The 1-based line of the script.</param>
	/// <param name="Kind">How the edge was found.</param>
	internal sealed record DialogueEdge(String Target, Int32 Line, DialogueEdgeKind Kind);
}