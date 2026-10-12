namespace FalloutScriptStudio.Controls.Diagram.Rendering
{
	using System;
	using System.Collections.Generic;
	using System.Windows;
	using System.Windows.Media;
	using FalloutScriptStudio.Controls.Diagram.Internal;
	using FalloutScriptStudio.Controls.Diagram.Model;
	using FalloutScriptStudio.Controls.Diagram.Routing;
	using FalloutScriptStudio.Localization;

	/// <summary>
	/// Draws the items, links and interaction overlay of a <see cref="DiagramScene"/> in world coordinates.
	/// </summary>
	internal sealed partial class DiagramRenderer
	{
		private readonly DiagramScene _scene;
		private readonly Dictionary<DiagramItem, CachedItemDrawing> _cache = new Dictionary<DiagramItem, CachedItemDrawing>();
		private readonly Dictionary<DiagramNode, Drawing> _contentCache = new Dictionary<DiagramNode, Drawing>();

		public DiagramRenderer(DiagramScene scene)
		{
			this._scene = scene;
			scene.Changed += this.OnSceneChanged;
		}

		/// <summary>
		/// Draws the items of one layer.
		/// </summary>
		/// <param name="dc">Target.</param>
		/// <param name="selectedLayer"><c>true</c> to draw the selected items, <c>false</c> for the others.</param>
		/// <param name="interactive"><c>false</c> to draw every item as unselected and not hovered (image export).</param>
		public void DrawItems(DrawingContext dc, Boolean selectedLayer, Boolean interactive)
		{
			this._scene.EnsureLayouts();
			DiagramHitTestResult hover = interactive ? this._scene.Hover : DiagramHitTestResult.Nothing;
			foreach (DiagramItem item in this._scene.Items)
			{
				if (item.IsHidden)
				{
					continue;
				}

				Boolean selected = interactive && item.IsSelected;
				if (selected != selectedLayer)
				{
					continue;
				}

				dc.PushTransform(FrozenTranslate(item.X, item.Y));
				dc.DrawDrawing(this.GetDrawing(item, selected, hover, interactive));
				dc.Pop();
			}
		}

		/// <summary>
		/// Draws what lives above the items: resize handles, start markers of highlighted links and the rubber band.
		/// </summary>
		public void DrawOverlay(DrawingContext dc, Double zoom, Rect rubberBand)
		{
			DiagramPalette palette = this._scene.Context.Palette;
			Double scale = Math.Clamp(1.0 / Math.Sqrt(zoom), 0.5, 2.0);

			foreach (RoutedLink routed in this._scene.Routes)
			{
				if (routed.Link.Source.IsSelected && routed.DrawStartMarker)
				{
					dc.DrawEllipse(palette.LinkOutgoing, null, routed.Route.Start, 3.0 * scale, 3.0 * scale);
				}
			}

			Pen gripPen = new Pen(palette.Accent, 1.25 / zoom);
			gripPen.Freeze();
			foreach (DiagramItem item in this._scene.Selection)
			{
				if (item.IsHidden)
				{
					continue;
				}

				Rect bounds = this._scene.GetItemBounds(item);
				for (DiagramHitPart part = DiagramHitPart.ResizeWest; part <= DiagramHitPart.ResizeSouth; part++)
				{
					if (!GripGeometry.Has(item, part))
					{
						continue;
					}

					Rect grip = GripGeometry.GetRect(bounds, part, zoom);
					Double radius = Math.Min(grip.Width, grip.Height) / 2.0;
					dc.DrawRoundedRectangle(palette.NodeFill, gripPen, grip, radius, radius);
				}
			}

			if (!rubberBand.IsEmpty)
			{
				Pen bandPen = new Pen(palette.Accent, 1.0 / zoom);
				bandPen.Freeze();
				dc.DrawRectangle(palette.AccentSoft, bandPen, rubberBand);
			}
		}

		private static TranslateTransform FrozenTranslate(Double x, Double y)
		{
			TranslateTransform transform = new TranslateTransform(x, y);
			transform.Freeze();
			return transform;
		}

		private static RectangleGeometry FrozenRoundedRect(Rect rect, Double radius)
		{
			RectangleGeometry geometry = new RectangleGeometry(rect, radius, radius);
			geometry.Freeze();
			return geometry;
		}

		private void OnSceneChanged(Object? sender, SceneChangedEventArgs e)
		{
			if ((e.Change & SceneChange.Context) != 0)
			{
				this._cache.Clear();
				this._contentCache.Clear();
				return;
			}

			if ((e.Change & (SceneChange.Layout | SceneChange.Appearance)) != 0)
			{
				if (e.Item == null)
				{
					this._cache.Clear();
					this._contentCache.Clear();
				}
				else
				{
					this._cache.Remove(e.Item);
					this.ForgetContent(e.Item);
				}
			}

			if ((e.Change & SceneChange.Structure) != 0 && e.Item != null && !this._scene.Contains(e.Item))
			{
				this._cache.Remove(e.Item);
				this.ForgetContent(e.Item);
			}
		}

		private void ForgetContent(DiagramItem item)
		{
			if (item is DiagramNode node)
			{
				this._contentCache.Remove(node);
			}
		}

		// Item visuals are recorded once into a frozen drawing (in item-local coordinates) and replayed whenever a layer is
		// re-recorded: glyph run creation is by far the most expensive part of drawing a node. The texts and the rows of a node
		// (its "content") do not depend on the selection, so they are recorded once more, apart from the frame, and a selection
		// change only draws the frame again and replays the content.
		private Drawing GetDrawing(DiagramItem item, Boolean selected, DiagramHitTestResult hover, Boolean cacheable)
		{
			Boolean hovered = ReferenceEquals(hover.Item, item);
			DiagramHitTestResult hoverState = hovered ? hover : DiagramHitTestResult.Nothing;
			if (cacheable
				&& this._cache.TryGetValue(item, out CachedItemDrawing? cached)
				&& cached.Selected == selected
				&& cached.Hover == hoverState)
			{
				return cached.Drawing;
			}

			DrawingGroup group = new DrawingGroup();
			using (DrawingContext dc = group.Open())
			{
				if (item is DiagramNode node)
				{
					this.DrawNode(dc, node, selected, hoverState, cacheable);
				}
				else if (item is DiagramNote note)
				{
					this.DrawNote(dc, note, selected, hovered);
				}
			}

			if (group.CanFreeze)
			{
				group.Freeze();
			}

			if (cacheable)
			{
				this._cache[item] = new CachedItemDrawing(group, selected, hoverState);
			}

			return group;
		}

		private sealed class CachedItemDrawing
		{
			public CachedItemDrawing(Drawing drawing, Boolean selected, DiagramHitTestResult hover)
			{
				this.Drawing = drawing;
				this.Selected = selected;
				this.Hover = hover;
			}

			public Drawing Drawing { get; }

			public Boolean Selected { get; }

			public DiagramHitTestResult Hover { get; }
		}
	}
}
