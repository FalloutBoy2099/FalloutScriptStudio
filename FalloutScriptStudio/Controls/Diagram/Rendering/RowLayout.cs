namespace FalloutScriptStudio.Controls.Diagram.Rendering
{
	using System;
	using System.Windows;
	using System.Windows.Media;
	using Model;

	/// <summary>
	/// Computed geometry and text of one visible <see cref="DiagramRow"/>. All rectangles are relative to the node's top-left corner.
	/// </summary>
	internal sealed class RowLayout
	{
		public RowLayout(DiagramRow row)
		{
			this.Row = row;
		}

		public DiagramRow Row { get; }

		/// <summary>Gets or sets the tinted header box; it is the clickable part of the row.</summary>
		public Rect Header { get; set; } = Rect.Empty;

		/// <summary>Gets or sets the zone of the header that toggles the detail line; empty for non expandable rows.</summary>
		public Rect Toggle { get; set; } = Rect.Empty;

		/// <summary>Gets or sets the rectangle of the icon; empty when the row has none.</summary>
		public Rect Icon { get; set; } = Rect.Empty;

		public FormattedText? Text { get; set; }

		public Point TextOrigin { get; set; }

		/// <summary>Gets or sets the detail box; empty unless the row is expanded.</summary>
		public Rect Detail { get; set; } = Rect.Empty;

		public Rect DetailIcon { get; set; } = Rect.Empty;

		public FormattedText? DetailText { get; set; }

		public Point DetailOrigin { get; set; }

		/// <summary>Gets or sets the vertical position, relative to the node top, where a link line leaves the node.</summary>
		public Double AnchorY { get; set; }

		/// <summary>Gets or sets a value indicating whether the row text is cut (ellipsis) and the full text is not visible.</summary>
		public Boolean IsTextTrimmed { get; set; }
	}
}