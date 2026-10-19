namespace FalloutScriptStudio.Controls.Diagram.Internal
{
	using System;
	using System.Collections.Generic;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Input;
	using Model;

	/// <summary>
	/// The pointer state machine of the canvas: selection, item dragging, resizing, rubber band, panning, hover and clicks.
	/// </summary>
	internal sealed class DiagramInputHandler
	{
		private const Double DragThreshold = 4.0;

		private readonly DiagramCanvas _canvas;
		private readonly DiagramScene _scene;
		private readonly DiagramSurface _surface;
		private readonly ScrollViewer _scroll;
		private readonly Dictionary<DiagramItem, Point> _dragStarts = [];
		private readonly HashSet<DiagramItem> _bandBase = [];
		private PointerMode _mode;
		private Point _downWorld;
		private Point _downSurface;
		private Point _panStart;
		private Double _panHorizontal;
		private Double _panVertical;
		private Boolean _moved;
		private DiagramHitTestResult _downHit = DiagramHitTestResult.Nothing;
		private Boolean _downWasSelected;
		private DiagramItem? _resizeItem;
		private DiagramHitPart _resizePart;
		private Rect _resizeStart;
		private DiagramItem? _lastHoverItem;
		private DiagramRow? _lastHoverRow;

		public DiagramInputHandler(DiagramCanvas canvas, DiagramScene scene, DiagramSurface surface, ScrollViewer scroll)
		{
			this._canvas = canvas;
			this._scene = scene;
			this._surface = surface;
			this._scroll = scroll;
		}

		private enum PointerMode
		{
			None,
			Dragging,
			Resizing,
			RubberBand,
			Panning
		}

		public void Attach()
		{
			this._surface.MouseDown += this.OnMouseDown;
			this._surface.MouseMove += this.OnMouseMove;
			this._surface.MouseUp += this.OnMouseUp;
			this._surface.MouseLeave += this.OnMouseLeave;
			this._surface.LostMouseCapture += this.OnLostMouseCapture;
		}

		private void OnMouseDown(Object sender, MouseButtonEventArgs e)
		{
			Point surfacePoint = e.GetPosition(this._surface);
			Point world = this._surface.SurfaceToWorld(surfacePoint);
			this._canvas.SetLastClickPosition(world);
			this._canvas.Focus();

			switch (e.ChangedButton)
			{
				case MouseButton.Middle:
					this.BeginPan(e);
					e.Handled = true;
					break;
				case MouseButton.Right:
					this.SelectUnderPointer(world);
					break;
				case MouseButton.Left:
					this.OnLeftDown(e, surfacePoint, world);
					e.Handled = true;
					break;
				default:
					break;
			}
		}

		private void SelectUnderPointer(Point world)
		{
			DiagramHitTestResult hit = this._scene.HitTest(world, false);
			if (hit.Item != null && !hit.Item.IsSelected)
			{
				this._canvas.Select(hit.Item, false);
			}
		}

		private void OnLeftDown(MouseButtonEventArgs e, Point surfacePoint, Point world)
		{
			this._downSurface = surfacePoint;
			this._downWorld = world;
			this._moved = false;
			DiagramHitTestResult hit = this._scene.HitTest(world, true);
			Boolean ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;

			if (e.ClickCount >= 2 && IsDoubleClickTarget(hit))
			{
				if (hit.Item is DiagramNote note)
				{
					this._canvas.Select(note, false);
					this._canvas.BeginEditNote(note);
				}
				else if (hit.Item != null)
				{
					this._canvas.RaiseItemDoubleClicked(hit.Item);
				}

				return;
			}

			this._surface.CaptureMouse();

			if (hit.Item != null && hit.Part >= DiagramHitPart.ResizeWest)
			{
				this._mode = PointerMode.Resizing;
				this._resizeItem = hit.Item;
				this._resizePart = hit.Part;
				this._resizeStart = this._scene.GetItemBounds(hit.Item);
				return;
			}

			if (hit.Item != null)
			{
				this._downHit = hit;
				this._downWasSelected = hit.Item.IsSelected;
				if (!this._downWasSelected)
				{
					this._canvas.Select(hit.Item, ctrl);
				}

				this._mode = PointerMode.Dragging;
				this._dragStarts.Clear();
				foreach (DiagramItem item in this._scene.Selection)
				{
					this._dragStarts[item] = new Point(item.X, item.Y);
				}

				return;
			}

			this._downHit = DiagramHitTestResult.Nothing;
			this._bandBase.Clear();
			if (ctrl)
			{
				foreach (DiagramItem item in this._scene.Selection)
				{
					this._bandBase.Add(item);
				}
			}
			else
			{
				this._canvas.ClearSelection();
			}

			this._mode = PointerMode.RubberBand;
		}

		private void BeginPan(MouseButtonEventArgs e)
		{
			if (this._mode != PointerMode.None)
			{
				return;
			}

			this._mode = PointerMode.Panning;
			this._panStart = e.GetPosition(this._scroll);
			this._panHorizontal = this._scroll.HorizontalOffset;
			this._panVertical = this._scroll.VerticalOffset;
			this._surface.CaptureMouse();
			this._surface.Cursor = Cursors.SizeAll;
		}

		private void OnMouseMove(Object sender, MouseEventArgs e)
		{
			Point surfacePoint = e.GetPosition(this._surface);
			Point world = this._surface.SurfaceToWorld(surfacePoint);

			if (this._mode == PointerMode.Panning)
			{
				Point p = e.GetPosition(this._scroll);
				this._scroll.ScrollToHorizontalOffset(Math.Max(0.0, this._panHorizontal - (p.X - this._panStart.X)));
				this._scroll.ScrollToVerticalOffset(Math.Max(0.0, this._panVertical - (p.Y - this._panStart.Y)));
				return;
			}

			if (this._mode != PointerMode.None && e.LeftButton == MouseButtonState.Pressed)
			{
				if (!this._moved && (surfacePoint - this._downSurface).Length < DragThreshold)
				{
					return;
				}

				this._moved = true;
				switch (this._mode)
				{
					case PointerMode.Dragging:
						this.Drag(world);
						break;
					case PointerMode.Resizing:
						this.Resize(world);
						break;
					case PointerMode.RubberBand:
						this.UpdateRubberBand(world);
						break;
					default:
						break;
				}

				this.AutoScroll(e.GetPosition(this._scroll));
				return;
			}

			this.UpdateHover(world);
		}

		// Scrolls while an item or the rubber band is dragged close to the edge of the view.
		private void AutoScroll(Point hostPoint)
		{
			const Double edge = 24.0;
			Size view = this._canvas.ViewportSize;
			Double dx = 0.0;
			Double dy = 0.0;
			if (hostPoint.X < edge)
			{
				dx = hostPoint.X - edge;
			}
			else if (hostPoint.X > view.Width - edge)
			{
				dx = hostPoint.X - (view.Width - edge);
			}

			if (hostPoint.Y < edge)
			{
				dy = hostPoint.Y - edge;
			}
			else if (hostPoint.Y > view.Height - edge)
			{
				dy = hostPoint.Y - (view.Height - edge);
			}

			if (dx != 0.0)
			{
				this._scroll.ScrollToHorizontalOffset(Math.Max(0.0, this._scroll.HorizontalOffset + dx));
			}

			if (dy != 0.0)
			{
				this._scroll.ScrollToVerticalOffset(Math.Max(0.0, this._scroll.VerticalOffset + dy));
			}
		}

		private void OnMouseUp(Object sender, MouseButtonEventArgs e)
		{
			if (e.ChangedButton == MouseButton.Middle && this._mode == PointerMode.Panning)
			{
				this.EndMode();
				this._surface.ReleaseMouseCapture();
				return;
			}

			if (e.ChangedButton != MouseButton.Left || this._mode == PointerMode.None)
			{
				return;
			}

			PointerMode mode = this._mode;
			Boolean moved = this._moved;
			this.EndMode();
			this._surface.ReleaseMouseCapture();
			if (mode == PointerMode.Dragging && !moved)
			{
				this.HandleClick(e);
			}

			this.UpdateHover(this._surface.SurfaceToWorld(e.GetPosition(this._surface)));
		}

		private void OnMouseLeave(Object sender, MouseEventArgs e)
		{
			if (this._mode == PointerMode.None)
			{
				this.SetHover(DiagramHitTestResult.Nothing);
				this._surface.Cursor = null;
			}
		}

		private void OnLostMouseCapture(Object sender, MouseEventArgs e)
		{
			if (this._mode != PointerMode.None)
			{
				this.EndMode();
			}
		}

		private void EndMode()
		{
			this._mode = PointerMode.None;
			this._moved = false;
			this._resizeItem = null;
			this._dragStarts.Clear();
			this._surface.RubberBand = Rect.Empty;
			this._surface.Cursor = null;
		}

		private void Drag(Point world)
		{
			if (this._dragStarts.Count == 0)
			{
				return;
			}

			Double minX = Double.MaxValue;
			Double minY = Double.MaxValue;
			foreach (Point start in this._dragStarts.Values)
			{
				minX = Math.Min(minX, start.X);
				minY = Math.Min(minY, start.Y);
			}

			Double dx = Math.Max(world.X - this._downWorld.X, -minX);
			Double dy = Math.Max(world.Y - this._downWorld.Y, -minY);

			// Snap the delta so that the grabbed item lands on the grid and the others keep their relative offsets.
			DiagramItem? primary = this._downHit.Item;
			if (primary != null && this._dragStarts.TryGetValue(primary, out Point primaryStart))
			{
				dx = this._canvas.Snap(primaryStart.X + dx) - primaryStart.X;
				dy = this._canvas.Snap(primaryStart.Y + dy) - primaryStart.Y;
				dx = Math.Max(dx, -minX);
				dy = Math.Max(dy, -minY);
			}

			foreach (KeyValuePair<DiagramItem, Point> pair in this._dragStarts)
			{
				pair.Key.X = pair.Value.X + dx;
				pair.Key.Y = pair.Value.Y + dy;
			}
		}

		private void Resize(Point world)
		{
			DiagramItem? item = this._resizeItem;
			if (item == null)
			{
				return;
			}

			Rect start = this._resizeStart;
			Double dx = world.X - this._downWorld.X;
			Double dy = world.Y - this._downWorld.Y;
			switch (this._resizePart)
			{
				case DiagramHitPart.ResizeEast:
					item.Width = this._canvas.Snap(start.Right + dx) - start.X;
					break;
				case DiagramHitPart.ResizeWest:
					{
						Double left = Math.Min(this._canvas.Snap(start.X + dx), start.Right - item.MinimumWidth);
						item.Width = start.Right - left;
						item.X = start.Right - item.Width;
						break;
					}

				case DiagramHitPart.ResizeSouth:
					item.Height = this._canvas.Snap(start.Bottom + dy) - start.Y;
					break;
				case DiagramHitPart.ResizeNorth:
					{
						Double top = Math.Min(this._canvas.Snap(start.Y + dy), start.Bottom - item.MinimumHeight);
						item.Height = start.Bottom - top;
						item.Y = start.Bottom - item.Height;
						break;
					}

				default:
					break;
			}
		}

		private void UpdateRubberBand(Point world)
		{
			Rect band = new(this._downWorld, world);
			this._surface.RubberBand = band;
			this._canvas.BeginUpdate();
			try
			{
				foreach (DiagramItem item in new List<DiagramItem>(this._scene.Items))
				{
					Boolean selected = this._bandBase.Contains(item) || (!item.IsHidden && this._scene.GetItemBounds(item).IntersectsWith(band));
					this._scene.SetSelected(item, selected);
				}
			}
			finally
			{
				this._canvas.EndUpdate();
			}
		}

		private void HandleClick(MouseButtonEventArgs e)
		{
			DiagramItem? item = this._downHit.Item;
			if (item == null)
			{
				return;
			}

			ModifierKeys modifiers = Keyboard.Modifiers;
			if ((modifiers & ModifierKeys.Control) != 0)
			{
				if (this._downWasSelected)
				{
					this._canvas.Deselect(item);
				}

				return;
			}

			DiagramHitPart part = this._downHit.Part;
			DiagramNode? node = item as DiagramNode;
			Boolean isButton = part == DiagramHitPart.CollapseButton || part == DiagramHitPart.CodeButton;
			if (!isButton && this._scene.Selection.Count > 1 && item.IsSelected)
			{
				this._canvas.Select(item, false);
			}

			switch (part)
			{
				case DiagramHitPart.CollapseButton when node != null:
					node.IsCollapsed = !node.IsCollapsed;
					break;
				case DiagramHitPart.CodeButton when node != null:
					node.ShowCodeRows = !node.ShowCodeRows;
					node.HasUserShowCodeSetting = true;
					this._canvas.RaiseNodeCodeToggled(node);
					break;
				case DiagramHitPart.RowToggle when node != null && this._downHit.Row != null:
					this._downHit.Row.IsExpanded = !this._downHit.Row.IsExpanded;
					this.RaiseRowClicked(e, node, this._downHit.Row, part);
					break;
				case DiagramHitPart.Row when node != null && this._downHit.Row != null:
				case DiagramHitPart.RowDetail when node != null && this._downHit.Row != null:
					this.RaiseRowClicked(e, node, this._downHit.Row, part);
					break;
				case DiagramHitPart.Body when item is DiagramNote note && this._downWasSelected && this._scene.Selection.Count == 1:
					this._canvas.BeginEditNote(note);
					break;
				default:
					break;
			}
		}

		private void RaiseRowClicked(MouseButtonEventArgs e, DiagramNode node, DiagramRow row, DiagramHitPart part)
		{
			Point world = this._surface.SurfaceToWorld(e.GetPosition(this._surface));
			Rect bounds = this._canvas.GetRowViewportBounds(row);
			this._canvas.RaiseRowClicked(new DiagramRowEventArgs(node, row, part, Keyboard.Modifiers, e.ChangedButton, world, bounds, this._canvas.IsRowTextTrimmed(row)));
		}

		private void UpdateHover(Point world)
		{
			this.SetHover(this._scene.HitTest(world, true));
		}

		private void SetHover(DiagramHitTestResult hit)
		{
			if (this._scene.SetHover(hit))
			{
				this._surface.Cursor = GetCursor(hit);
				if (!ReferenceEquals(hit.Item, this._lastHoverItem) || !ReferenceEquals(hit.Row, this._lastHoverRow))
				{
					this._lastHoverItem = hit.Item;
					this._lastHoverRow = hit.Row;
					this._canvas.RaiseItemHover(hit);
				}
			}
		}

		// Double clicks on buttons, chevrons, rows and grips are treated as two quick single clicks.
		private static Boolean IsDoubleClickTarget(DiagramHitTestResult hit)
		{
			return hit.Item is DiagramNote
				? hit.Part == DiagramHitPart.Body
				: hit.Item != null && (hit.Part == DiagramHitPart.Title || hit.Part == DiagramHitPart.Body);
		}

		private static Cursor? GetCursor(DiagramHitTestResult hit)
		{
			return hit.Part switch
			{
				DiagramHitPart.ResizeEast or DiagramHitPart.ResizeWest => Cursors.SizeWE,
				DiagramHitPart.ResizeNorth or DiagramHitPart.ResizeSouth => Cursors.SizeNS,
				DiagramHitPart.CollapseButton or DiagramHitPart.CodeButton or DiagramHitPart.RowToggle => Cursors.Hand,
				_ => null
			};
		}
	}
}