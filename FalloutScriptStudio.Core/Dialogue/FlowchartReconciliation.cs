namespace FalloutScriptStudio.Core.Dialogue
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// Result of comparing a saved flowchart with the nodes that exist in the script now.
	/// </summary>
	/// <param name="Document">The saved flowchart without the nodes that no longer exist in the script.</param>
	/// <param name="AddedNodes">Nodes of the script that the saved flowchart has no position for (the legacy "added new node" message).</param>
	/// <param name="RemovedNodes">Nodes of the saved flowchart that no longer exist in the script (the legacy "deleted existing dialog node" message).</param>
	public sealed record FlowchartReconciliation(FlowchartDocument Document, IReadOnlyList<String> AddedNodes, IReadOnlyList<String> RemovedNodes);
}