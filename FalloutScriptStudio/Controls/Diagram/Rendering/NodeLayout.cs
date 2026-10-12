namespace FalloutScriptStudio.Controls.Diagram.Rendering
{
	using System;
	using System.Collections.Generic;
	using System.Windows;
	using System.Windows.Media;
	using FalloutScriptStudio.Controls.Diagram.Model;

	/// <summary>
	/// Computed geometry and text of a <see cref="DiagramNode"/> at a given width. All rectangles are relative to the node's
	/// top-left corner so moving the node never invalidates the layout.
	/// </summary>
	internal sealed class NodeLayout
	{
		/// <summary>Gets the render context the layout was made with (its texts are only valid for it).</summary>
		public DiagramRenderContext? Context { get; set; }

		/// <summary>Gets the text measurements of the layout, which the next layout of the node reuses.</summary>
		public NaturalTextCache? Natural { get; set; }

		public Double Width { get; init; }

		public Double Height { get; init; }

		/// <summary>Gets the width the content would like to have (used for "match widths" and automatic widths).</summary>
		public Double ContentWidth { get; init; }

		/// <summary>Gets the height of the title bar plus the header lines.</summary>
		public Double HeaderHeight { get; init; }

		public Rect TitleBar { get; init; }

		public Rect CodeButton { get; init; } = Rect.Empty;

		public Rect CollapseButton { get; init; }

		public FormattedText? Title { get; init; }

		public Point TitleOrigin { get; init; }

		public IReadOnlyList<(FormattedText Text, Point Origin)> HeaderLines { get; init; } = Array.Empty<(FormattedText, Point)>();

		public IReadOnlyList<RowLayout> Rows { get; init; } = Array.Empty<RowLayout>();

		/// <summary>Gets the vertical offset, from the node top, where a link line enters the east or west side of the node.</summary>
		public Double TargetAnchorOffset { get; init; }

		/// <summary>Gets the vertical offset, from the node top, where link lines leave a collapsed node or a node whose linking row is not visible.</summary>
		public Double DefaultSourceAnchorOffset { get; init; }

		public RowLayout? FindRow(DiagramRow row)
		{
			foreach (RowLayout layout in this.Rows)
			{
				if (ReferenceEquals(layout.Row, row))
				{
					return layout;
				}
			}

			return null;
		}

		/// <summary>
		/// Finds which part of the node is at <paramref name="local"/> (node relative coordinates).
		/// </summary>
		public (DiagramHitPart Part, DiagramRow? Row) HitTest(Point local)
		{
			if (this.CodeButton.Contains(local))
			{
				return (DiagramHitPart.CodeButton, null);
			}

			if (this.CollapseButton.Contains(local))
			{
				return (DiagramHitPart.CollapseButton, null);
			}

			if (this.TitleBar.Contains(local))
			{
				return (DiagramHitPart.Title, null);
			}

			foreach (RowLayout row in this.Rows)
			{
				if (row.Toggle.Contains(local))
				{
					return (DiagramHitPart.RowToggle, row.Row);
				}

				if (row.Header.Contains(local))
				{
					return (DiagramHitPart.Row, row.Row);
				}

				if (row.Detail.Contains(local))
				{
					return (DiagramHitPart.RowDetail, row.Row);
				}
			}

			return (DiagramHitPart.Body, null);
		}
	}
}
