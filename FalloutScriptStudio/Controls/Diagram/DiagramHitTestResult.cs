namespace FalloutScriptStudio.Controls.Diagram
{
	using FalloutScriptStudio.Controls.Diagram.Model;

	/// <summary>
	/// Result of <see cref="DiagramCanvas.HitTest"/>.
	/// </summary>
	/// <param name="Item">The topmost visible item under the point, or <c>null</c>.</param>
	/// <param name="Row">The row under the point when <paramref name="Part"/> concerns a row, otherwise <c>null</c>.</param>
	/// <param name="Part">Which part of the item was hit.</param>
	public sealed record DiagramHitTestResult(DiagramItem? Item, DiagramRow? Row, DiagramHitPart Part)
	{
		/// <summary>Gets a result that represents "nothing hit".</summary>
		public static DiagramHitTestResult Nothing { get; } = new DiagramHitTestResult(null, null, DiagramHitPart.None);

		/// <summary>Gets the node under the point, if the hit item is a node.</summary>
		public DiagramNode? Node => this.Item as DiagramNode;
	}
}
