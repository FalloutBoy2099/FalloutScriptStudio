namespace FalloutScriptStudio.Controls.Diagram
{
	using System;
	using System.Windows;
	using Model;
	using Rendering;

	// Hit testing, item bounds and conversions between viewport and world coordinates.
	public sealed partial class DiagramCanvas
	{
		/// <summary>
		/// Gets the union of the bounds of all visible items in world coordinates, or <see cref="Rect.Empty"/>.
		/// </summary>
		public Rect GetContentBounds()
		{
			return this._scene.GetContentBounds();
		}

		/// <summary>
		/// Gets the bounds of an item in world coordinates (the height of a node is the laid out height).
		/// </summary>
		/// <param name="item">The item.</param>
		public Rect GetItemBounds(DiagramItem item)
		{
			ArgumentNullException.ThrowIfNull(item);
			return this._scene.GetItemBounds(item);
		}

		/// <summary>
		/// Finds the item (and row) at a point.
		/// </summary>
		/// <param name="viewportPoint">A point relative to the control.</param>
		/// <returns>What was hit; <see cref="DiagramHitTestResult.Nothing"/> for empty space.</returns>
		public DiagramHitTestResult HitTest(Point viewportPoint)
		{
			return this._scene.HitTest(this.ViewportToWorld(viewportPoint), false);
		}

		/// <summary>
		/// Finds the item (and row) at a world position.
		/// </summary>
		/// <param name="worldPoint">A point in world coordinates.</param>
		public DiagramHitTestResult HitTestWorld(Point worldPoint)
		{
			return this._scene.HitTest(worldPoint, false);
		}

		/// <summary>
		/// Converts a point relative to the control to world coordinates.
		/// </summary>
		/// <param name="viewportPoint">A point relative to the control.</param>
		public Point ViewportToWorld(Point viewportPoint)
		{
			return this._surface.SurfaceToWorld(new Point(viewportPoint.X + this.HorizontalOffset, viewportPoint.Y + this.VerticalOffset));
		}

		/// <summary>
		/// Converts a world position to a point relative to the control.
		/// </summary>
		/// <param name="worldPoint">A point in world coordinates.</param>
		public Point WorldToViewport(Point worldPoint)
		{
			Point s = this._surface.WorldToSurface(worldPoint);
			return new Point(s.X - this.HorizontalOffset, s.Y - this.VerticalOffset);
		}

		/// <summary>
		/// Gets the center of the visible area in world coordinates (where to place a newly created node).
		/// </summary>
		public Point GetViewportCenterInWorld()
		{
			Size size = this.ViewportSize;
			return this.ViewportToWorld(new Point(size.Width / 2.0, size.Height / 2.0));
		}

		/// <summary>
		/// Gets the header rectangle of a row relative to the control, for example to anchor a popup next to it.
		/// </summary>
		/// <param name="row">The row.</param>
		/// <returns>The rectangle, or <see cref="Rect.Empty"/> when the row is not currently visible.</returns>
		public Rect GetRowViewportBounds(DiagramRow row)
		{
			ArgumentNullException.ThrowIfNull(row);
			DiagramNode? node = row.Owner;
			if (node == null || node.IsHidden || node.IsCollapsed || !this._scene.Contains(node))
			{
				return Rect.Empty;
			}

			RowLayout? layout = this._scene.GetLayout(node).FindRow(row);
			if (layout == null)
			{
				return Rect.Empty;
			}

			Point topLeft = this.WorldToViewport(new Point(node.X + layout.Header.X, node.Y + layout.Header.Y));
			return new Rect(topLeft.X, topLeft.Y, layout.Header.Width * this.Zoom, layout.Header.Height * this.Zoom);
		}

		/// <summary>
		/// Gets a value indicating whether the text of a row is currently cut with an ellipsis (it does not fit the node width).
		/// </summary>
		/// <param name="row">The row.</param>
		/// <returns><c>false</c> also when the row is not visible.</returns>
		public Boolean IsRowTextTrimmed(DiagramRow row)
		{
			ArgumentNullException.ThrowIfNull(row);
			DiagramNode? node = row.Owner;
			if (node == null || node.IsHidden || node.IsCollapsed || !this._scene.Contains(node))
			{
				return false;
			}

			return this._scene.GetLayout(node).FindRow(row)?.IsTextTrimmed ?? false;
		}
	}
}