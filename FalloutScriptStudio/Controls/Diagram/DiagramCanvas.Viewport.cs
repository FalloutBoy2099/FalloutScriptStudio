namespace FalloutScriptStudio.Controls.Diagram
{
	using System;
	using System.Windows;
	using System.Windows.Input;
	using FalloutScriptStudio.Controls.Diagram.Model;

	// Zoom and scroll.
	public sealed partial class DiagramCanvas
	{
		private const Double WheelZoomStep = 1.1;

		private Point? _zoomAnchor;

		/// <summary>
		/// Sets the zoom, keeping the world point under <paramref name="viewportAnchor"/> fixed.
		/// </summary>
		/// <param name="viewportAnchor">Anchor relative to the control.</param>
		/// <param name="zoom">The new zoom factor.</param>
		public void ZoomAt(Point viewportAnchor, Double zoom)
		{
			this._zoomAnchor = viewportAnchor;
			try
			{
				this.Zoom = zoom;
			}
			finally
			{
				this._zoomAnchor = null;
			}
		}

		/// <summary>Zooms in by one wheel step around the center of the view.</summary>
		public void ZoomIn()
		{
			this.Zoom *= WheelZoomStep;
		}

		/// <summary>Zooms out by one wheel step around the center of the view.</summary>
		public void ZoomOut()
		{
			this.Zoom /= WheelZoomStep;
		}

		/// <summary>Sets the zoom to 100 %.</summary>
		public void ResetZoom()
		{
			this.Zoom = 1.0;
		}

		/// <summary>
		/// Chooses the zoom (never above 100 %) at which the whole diagram fits into the view and scrolls to it.
		/// </summary>
		/// <param name="margin">Free space kept around the diagram, in screen units.</param>
		public void ZoomToFit(Double margin = 20.0)
		{
			Rect bounds = this._scene.GetContentBounds();
			if (bounds.IsEmpty || bounds.Width <= 0.0 || bounds.Height <= 0.0)
			{
				return;
			}

			Size view = this.ViewportSize;
			Double fit = Math.Min((view.Width - (2.0 * margin)) / bounds.Width, (view.Height - (2.0 * margin)) / bounds.Height);
			if (Double.IsNaN(fit) || fit <= 0.0)
			{
				return;
			}

			this.Zoom = Math.Min(1.0, fit);
			this.ScrollToWorldPoint(new Point(bounds.X + (bounds.Width / 2.0), bounds.Y + (bounds.Height / 2.0)), true);
		}

		/// <summary>
		/// Sets the scroll offsets (screen units at the current zoom). Use it with <see cref="HorizontalOffset"/> /
		/// <see cref="VerticalOffset"/> to restore a saved view after setting <see cref="Zoom"/>.
		/// </summary>
		/// <param name="horizontal">Horizontal offset.</param>
		/// <param name="vertical">Vertical offset.</param>
		public void ScrollToOffset(Double horizontal, Double vertical)
		{
			this._scrollViewer.UpdateLayout();
			this._scrollViewer.ScrollToHorizontalOffset(Math.Max(0.0, horizontal));
			this._scrollViewer.ScrollToVerticalOffset(Math.Max(0.0, vertical));
			this._scrollViewer.UpdateLayout();
		}

		/// <summary>
		/// Scrolls so that a world point is at the center (or the top-left corner) of the view.
		/// </summary>
		/// <param name="worldPoint">The world point.</param>
		/// <param name="center"><c>true</c> to center the point.</param>
		public void ScrollToWorldPoint(Point worldPoint, Boolean center = true)
		{
			this._scene.EnsureLayouts();
			this._scrollViewer.UpdateLayout();
			Point s = this._surface.WorldToSurface(worldPoint);
			Size view = this.ViewportSize;
			Double x = center ? s.X - (view.Width / 2.0) : s.X;
			Double y = center ? s.Y - (view.Height / 2.0) : s.Y;
			this.ScrollToOffset(x, y);
		}

		/// <summary>
		/// Scrolls an item into view, centering it (the legacy "jump to node").
		/// </summary>
		/// <param name="item">The item.</param>
		/// <param name="center"><c>true</c> to center the item; <c>false</c> to put its top-left corner at the top-left of the view.</param>
		public void ScrollToItem(DiagramItem item, Boolean center = true)
		{
			ArgumentNullException.ThrowIfNull(item);
			Rect bounds = this._scene.GetItemBounds(item);
			Point target = center ? new Point(bounds.X + (bounds.Width / 2.0), bounds.Y + (bounds.Height / 2.0)) : bounds.TopLeft;
			this.ScrollToWorldPoint(target, center);
		}

		/// <summary>
		/// Brings node layouts, scroll extents and the drawing up to date immediately instead of at the next render pass.
		/// </summary>
		public void Refresh()
		{
			this._surface.FlushRendering();
			this._scrollViewer.UpdateLayout();
			this._surface.FlushRendering();
		}

		internal void ZoomByWheel(Double delta, Point viewportAnchor)
		{
			this.ZoomAt(viewportAnchor, this.Zoom * Math.Pow(WheelZoomStep, delta / 120.0));
		}

		/// <inheritdoc />
		protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
		{
			base.OnPreviewMouseWheel(e);
			if (e.Handled || (this._noteEditor != null && e.OriginalSource is DependencyObject source && this._noteEditor.IsAncestorOf(source)))
			{
				return;
			}

			ModifierKeys modifiers = Keyboard.Modifiers;
			Point anchor = e.GetPosition(this._scrollViewer);
			if ((modifiers & ModifierKeys.Control) != 0 || (this.ZoomsOnMouseWheel && (modifiers & ModifierKeys.Shift) == 0))
			{
				this.ZoomByWheel(e.Delta, anchor);
				e.Handled = true;
			}
			else if ((modifiers & ModifierKeys.Shift) != 0)
			{
				this._scrollViewer.ScrollToHorizontalOffset(this._scrollViewer.HorizontalOffset - e.Delta);
				e.Handled = true;
			}
		}

		private void ApplyZoom(Double newZoom)
		{
			Size view = this.ViewportSize;
			Point anchor = this._zoomAnchor ?? new Point(view.Width / 2.0, view.Height / 2.0);
			Point world = this.ViewportToWorld(anchor);
			this._surface.Zoom = newZoom;
			this._scrollViewer.UpdateLayout();
			Point s = this._surface.WorldToSurface(world);
			this._scrollViewer.ScrollToHorizontalOffset(Math.Max(0.0, s.X - anchor.X));
			this._scrollViewer.ScrollToVerticalOffset(Math.Max(0.0, s.Y - anchor.Y));
			this._scrollViewer.UpdateLayout();
			this.PositionNoteEditor();
			this._layoutChangedPending = true;
			this.ZoomChanged?.Invoke(this, EventArgs.Empty);
		}
	}
}
