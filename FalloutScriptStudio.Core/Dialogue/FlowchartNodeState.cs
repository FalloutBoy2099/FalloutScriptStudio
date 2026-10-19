namespace FalloutScriptStudio.Core.Dialogue
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// The saved position and state of a node in a flowchart (the <c>&lt;Node&gt;</c> element of a <c>.fcd</c> file).
	/// </summary>
	/// <param name="Name">The name of the node procedure.</param>
	public sealed record FlowchartNodeState(String Name)
	{
		/// <summary>Gets the horizontal position of the node on the canvas (canvas units, 100 per file unit).</summary>
		public Single X { get; init; }

		/// <summary>Gets the vertical position of the node on the canvas.</summary>
		public Single Y { get; init; }

		/// <summary>Gets the width of the node on the canvas.</summary>
		public Single Width { get; init; }

		/// <summary>Gets a value indicating whether the node is collapsed to its header.</summary>
		public Boolean Collapsed { get; init; }

		/// <summary>Gets a value indicating whether the node is hidden from the canvas.</summary>
		public Boolean Hidden { get; init; }

		/// <summary>
		/// Gets whether the code lines of the node are shown when the user set it for this node (the <c>ShowCode</c> attribute);
		/// <c>null</c> when the node follows the global setting.
		/// </summary>
		public Boolean? ShowCode { get; init; }

		/// <summary>Gets the collapsed state of the message blocks of the node.</summary>
		public IReadOnlyList<FlowchartContentState> Contents { get; init; } = Array.Empty<FlowchartContentState>();
	}
}