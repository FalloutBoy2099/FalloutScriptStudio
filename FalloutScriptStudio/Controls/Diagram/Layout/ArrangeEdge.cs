namespace FalloutScriptStudio.Controls.Diagram.Layout
{
	using System;

	/// <summary>
	/// A directed edge between two boxes of a <see cref="LayeredGraphLayout"/> input, identified by their indexes.
	/// </summary>
	/// <param name="From">Index of the box the edge starts at.</param>
	/// <param name="To">Index of the box the edge points to.</param>
	public readonly record struct ArrangeEdge(Int32 From, Int32 To);
}
