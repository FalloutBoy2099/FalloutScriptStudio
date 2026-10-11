namespace FalloutScriptStudio.Controls.Diagram.Layout
{
	using System;

	/// <summary>
	/// Spacing and origin used by <see cref="LayeredGraphLayout"/> (and therefore by <c>DiagramCanvas.AutoArrange</c>).
	/// </summary>
	public sealed record DiagramArrangeOptions
	{
		/// <summary>Gets the horizontal gap between two layers; it must leave room for the connector lines.</summary>
		public Double HorizontalSpacing { get; init; } = 90.0;

		/// <summary>Gets the vertical gap between two nodes of the same layer.</summary>
		public Double VerticalSpacing { get; init; } = 30.0;

		/// <summary>Gets the left edge of the first layer.</summary>
		public Double OriginX { get; init; } = 40.0;

		/// <summary>Gets the top edge of the arrangement.</summary>
		public Double OriginY { get; init; } = 40.0;

		/// <summary>Gets the width after which nodes without any link are wrapped to a new row below the graph.</summary>
		public Double UnlinkedRowWidth { get; init; } = 1600.0;
	}
}
