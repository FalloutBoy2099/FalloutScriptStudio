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
	internal sealed class DiagramRenderer
	{
		private const Double LinkCornerRadius = 8.0;

		private readonly DiagramScene _scene;
		private readonly Dictionary<DiagramItem, CachedItemDrawing> _cache = new Dictionary<DiagramItem, CachedItemDrawing>();

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
		/// Draws the connector lines.
		/// </summary>
		public void DrawLinks(DrawingContext dc, Double zoom, Boolean onlyForSelection, Boolean interactive)
		{
			DiagramPalette palette = this._scene.Context.Palette;
			Boolean anySelectedNode = false;
			if (interactive)
			{
				foreach (DiagramItem item in this._scene.Selection)
				{
					if (item is DiagramNode)
					{
						anySelectedNode = true;
						break;
					}
				}
			}

			List<RoutedLink> normal = new List<RoutedLink>();
			List<RoutedLink> incoming = new List<RoutedLink>();
			List<RoutedLink> outgoing = new List<RoutedLink>();
			foreach (RoutedLink routed in this._scene.Routes)
			{
				if (interactive && routed.Link.Source.IsSelected)
				{
					outgoing.Add(routed);
				}
				else if (interactive && routed.Link.Target.IsSelected)
				{
					incoming.Add(routed);
				}
				else if (!(onlyForSelection && anySelectedNode))
				{
					normal.Add(routed);
				}
			}

			Double thickness = Math.Clamp(1.5 / zoom, 1.0, 4.0);
			Double scale = Math.Clamp(1.0 / Math.Sqrt(zoom), 0.5, 2.0);
			this.DrawLinkGroup(dc, normal, palette.LinkNormal, thickness, scale, drawStartMarkers: true);
			this.DrawLinkGroup(dc, incoming, palette.LinkIncoming, thickness + 0.5, scale, drawStartMarkers: true);
			this.DrawLinkGroup(dc, outgoing, palette.LinkOutgoing, thickness + 0.5, scale, drawStartMarkers: false);
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

		private static StreamGeometry BuildRoundedPolylines(IEnumerable<RoutedLink> links)
		{
			StreamGeometry geometry = new StreamGeometry();
			using (StreamGeometryContext ctx = geometry.Open())
			{
				foreach (RoutedLink routed in links)
				{
					IReadOnlyList<Point> pts = routed.Route.Points;
					ctx.BeginFigure(pts[0], false, false);
					for (Int32 i = 1; i < pts.Count - 1; i++)
					{
						Point prev = pts[i - 1];
						Point corner = pts[i];
						Point next = pts[i + 1];
						Double inLength = (corner - prev).Length;
						Double outLength = (next - corner).Length;
						Double r = Math.Min(LinkCornerRadius, Math.Min(inLength, outLength) / 2.0);
						Vector inDir = (corner - prev) / inLength;
						Vector outDir = (next - corner) / outLength;
						ctx.LineTo(corner - (inDir * r), true, false);
						ctx.QuadraticBezierTo(corner, corner + (outDir * r), true, false);
					}

					ctx.LineTo(pts[pts.Count - 1], true, false);
				}
			}

			geometry.Freeze();
			return geometry;
		}

		private static Vector DirectionVector(RouteDirection direction)
		{
			return direction switch
			{
				RouteDirection.Up => new Vector(0, -1),
				RouteDirection.Down => new Vector(0, 1),
				RouteDirection.Left => new Vector(-1, 0),
				_ => new Vector(1, 0)
			};
		}

		private void DrawLinkGroup(DrawingContext dc, List<RoutedLink> links, Brush brush, Double thickness, Double scale, Boolean drawStartMarkers)
		{
			if (links.Count == 0)
			{
				return;
			}

			DiagramPalette palette = this._scene.Context.Palette;
			Pen pen = DiagramPalette.MakeLinkPen(brush, thickness);
			dc.DrawGeometry(null, pen, BuildRoundedPolylines(links));

			StreamGeometry arrows = new StreamGeometry();
			Double length = 9.0 * scale;
			Double half = 4.5 * scale;
			using (StreamGeometryContext ctx = arrows.Open())
			{
				foreach (RoutedLink routed in links)
				{
					Point tip = routed.Route.End;
					Vector dir = DirectionVector(routed.Route.EndDirection);
					Vector perp = new Vector(-dir.Y, dir.X);
					Point back = tip - (dir * length);
					ctx.BeginFigure(tip, true, true);
					ctx.LineTo(back + (perp * half), true, false);
					ctx.LineTo(back - (perp * half), true, false);
				}
			}

			arrows.Freeze();
			dc.DrawGeometry(brush, null, arrows);

			if (drawStartMarkers)
			{
				foreach (RoutedLink routed in links)
				{
					if (routed.DrawStartMarker)
					{
						dc.DrawEllipse(brush, null, routed.Route.Start, 3.0 * scale, 3.0 * scale);
					}
				}
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
				return;
			}

			if ((e.Change & (SceneChange.Layout | SceneChange.Appearance)) != 0)
			{
				if (e.Item == null)
				{
					this._cache.Clear();
				}
				else
				{
					this._cache.Remove(e.Item);
				}
			}

			if ((e.Change & SceneChange.Structure) != 0 && e.Item != null && !this._scene.Contains(e.Item))
			{
				this._cache.Remove(e.Item);
			}
		}

		// Item visuals are recorded once into a frozen drawing (in item-local coordinates) and replayed whenever a layer is
		// re-recorded: glyph run creation is by far the most expensive part of drawing a node.
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
					this.DrawNode(dc, node, selected, hoverState);
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

		private void DrawNode(DrawingContext dc, DiagramNode node, Boolean selected, DiagramHitTestResult hover)
		{
			DiagramRenderContext context = this._scene.Context;
			DiagramPalette palette = context.Palette;
			NodeLayout layout = this._scene.GetLayout(node);
			Double w = layout.Width;
			Double h = layout.Height;
			Double r = DiagramMetrics.NodeCornerRadius;
			Boolean hovered = ReferenceEquals(hover.Item, node);

			if (context.HighQuality && !context.LowDetail)
			{
				dc.DrawRoundedRectangle(palette.ShadowFar, null, new Rect(-1, 1, w + 2, h + 4), r + 1, r + 1);
				dc.DrawRoundedRectangle(palette.ShadowNear, null, new Rect(0, 1, w, h + 1), r, r);
			}

			Pen border = selected ? palette.AccentPen : (hovered && node.Kind == DiagramNodeKind.Default ? palette.NodeStrokeHover : palette.GetKindPen(node.Kind));
			Double inset = border.Thickness / 2.0;
			Rect body = new Rect(inset, inset, Math.Max(0.0, w - (2.0 * inset)), Math.Max(0.0, h - (2.0 * inset)));
			dc.DrawRoundedRectangle(palette.NodeFill, null, body, r, r);

			dc.PushClip(FrozenRoundedRect(new Rect(0, 0, w, h), r));
			dc.DrawRectangle(palette.GetHeaderTint(node.Kind), null, new Rect(0, 0, w, layout.HeaderHeight));
			dc.Pop();

			if (selected)
			{
				dc.DrawRoundedRectangle(null, palette.AccentRingPen, body, r, r);
			}

			dc.DrawRoundedRectangle(null, border, body, r, r);

			this.DrawNodeHeader(dc, node, layout, hover, hovered);
			if (!node.IsCollapsed)
			{
				this.DrawRows(dc, node, layout, hover, hovered);
			}
		}

		private void DrawNodeHeader(DrawingContext dc, DiagramNode node, NodeLayout layout, DiagramHitTestResult hover, Boolean hovered)
		{
			DiagramPalette palette = this._scene.Context.Palette;
			if (layout.Title != null)
			{
				dc.DrawText(layout.Title, layout.TitleOrigin);
			}

			foreach ((FormattedText text, Point origin) in layout.HeaderLines)
			{
				dc.DrawText(text, origin);
			}

			if (!layout.CodeButton.IsEmpty)
			{
				Rect b = layout.CodeButton;
				if (hovered && hover.Part == DiagramHitPart.CodeButton)
				{
					dc.DrawRoundedRectangle(palette.HoverOverlay, null, b, 4, 4);
				}

				Pen pen = node.ShowCodeRows ? palette.GlyphPenStrong : palette.GlyphPenOff;
				Double cx = b.X + (b.Width / 2.0);
				Double cy = b.Y + (b.Height / 2.0);
				dc.DrawLine(pen, new Point(cx - 5, cy - 4), new Point(cx + 2, cy - 4));
				dc.DrawLine(pen, new Point(cx - 5, cy - 1.3), new Point(cx + 0, cy - 1.3));
				dc.DrawLine(pen, new Point(cx - 5, cy + 1.4), new Point(cx + 4, cy + 1.4));
				dc.DrawLine(pen, new Point(cx - 5, cy + 4.1), new Point(cx + 1, cy + 4.1));
				if (!node.ShowCodeRows)
				{
					dc.DrawLine(palette.GlyphWarningPen, new Point(cx - 5, cy + 6), new Point(cx + 5, cy - 6));
				}
			}

			Rect c = layout.CollapseButton;
			if (hovered && hover.Part == DiagramHitPart.CollapseButton)
			{
				dc.DrawRoundedRectangle(palette.HoverOverlay, null, c, 4, 4);
			}

			Double mx = c.X + (c.Width / 2.0);
			Double my = c.Y + (c.Height / 2.0);
			if (node.IsCollapsed)
			{
				dc.DrawLine(palette.GlyphPenStrong, new Point(mx - 4, my - 2), new Point(mx, my + 2));
				dc.DrawLine(palette.GlyphPenStrong, new Point(mx, my + 2), new Point(mx + 4, my - 2));
			}
			else
			{
				dc.DrawLine(palette.GlyphPenStrong, new Point(mx - 4, my + 2), new Point(mx, my - 2));
				dc.DrawLine(palette.GlyphPenStrong, new Point(mx, my - 2), new Point(mx + 4, my + 2));
			}

			if (node.IsEditing)
			{
				dc.DrawEllipse(palette.Accent, null, new Point(layout.Width - 5, 5), 3, 3);
			}
		}

		private void DrawRows(DrawingContext dc, DiagramNode node, NodeLayout layout, DiagramHitTestResult hover, Boolean hovered)
		{
			DiagramRenderContext context = this._scene.Context;
			DiagramPalette palette = context.Palette;
			if (layout.Rows.Count == 0)
			{
				return;
			}

			dc.DrawLine(palette.TitleSeparatorPen, new Point(0, layout.HeaderHeight), new Point(layout.Width, layout.HeaderHeight));

			foreach (RowLayout rowLayout in layout.Rows)
			{
				DiagramRow row = rowLayout.Row;
				Brush? fill = row.Kind switch
				{
					DiagramRowKind.Reply => palette.ReplyFill,
					DiagramRowKind.Option => palette.OptionFill,
					DiagramRowKind.Call => palette.CallFill,
					_ => null
				};

				if (fill != null)
				{
					dc.DrawRoundedRectangle(fill, null, rowLayout.Header, DiagramMetrics.RowCornerRadius, DiagramMetrics.RowCornerRadius);
				}

				if (hovered && ReferenceEquals(hover.Row, row) && hover.Part != DiagramHitPart.RowDetail)
				{
					dc.DrawRoundedRectangle(palette.HoverOverlay, null, rowLayout.Header, DiagramMetrics.RowCornerRadius, DiagramMetrics.RowCornerRadius);
				}

				if (row.IsExpandable && !context.LowDetail)
				{
					Double cx = rowLayout.Header.X + DiagramMetrics.RowPaddingX + 4.0;
					Double cy = rowLayout.Header.Y + (rowLayout.Header.Height / 2.0);
					if (row.IsExpanded)
					{
						dc.DrawLine(palette.GlyphPen, new Point(cx - 3.5, cy - 1.5), new Point(cx, cy + 2));
						dc.DrawLine(palette.GlyphPen, new Point(cx, cy + 2), new Point(cx + 3.5, cy - 1.5));
					}
					else
					{
						dc.DrawLine(palette.GlyphPen, new Point(cx - 1.5, cy - 3.5), new Point(cx + 2, cy));
						dc.DrawLine(palette.GlyphPen, new Point(cx + 2, cy), new Point(cx - 1.5, cy + 3.5));
					}
				}

				Geometry? icon = rowLayout.Icon.IsEmpty ? null : DiagramIcons.Get(row.Icon);
				if (icon != null)
				{
					Brush iconBrush = row.Kind switch
					{
						DiagramRowKind.Reply => palette.ReplyIcon,
						DiagramRowKind.Option => palette.OptionIcon,
						_ => palette.TextSecondary
					};

					dc.PushTransform(FrozenTranslate(rowLayout.Icon.X, rowLayout.Icon.Y));
					dc.DrawGeometry(iconBrush, null, icon);
					dc.Pop();
				}

				if (rowLayout.Text != null)
				{
					dc.DrawText(rowLayout.Text, rowLayout.TextOrigin);
				}

				if (!rowLayout.Detail.IsEmpty && rowLayout.DetailText != null)
				{
					dc.DrawRoundedRectangle(palette.DetailFill, null, rowLayout.Detail, DiagramMetrics.RowCornerRadius, DiagramMetrics.RowCornerRadius);
					Geometry? arrow = DiagramIcons.Get(DiagramRowIcon.Arrow);
					if (arrow != null && !context.LowDetail)
					{
						dc.PushTransform(FrozenTranslate(rowLayout.DetailIcon.X, rowLayout.DetailIcon.Y));
						dc.DrawGeometry(palette.TextSecondary, null, arrow);
						dc.Pop();
					}

					dc.DrawText(rowLayout.DetailText, rowLayout.DetailOrigin);
				}
			}
		}

		private void DrawNote(DrawingContext dc, DiagramNote note, Boolean selected, Boolean hovered)
		{
			DiagramRenderContext context = this._scene.Context;
			DiagramPalette palette = context.Palette;
			Double w = note.Width;
			Double h = note.Height;
			Double r = 6.0;

			if (context.HighQuality)
			{
				dc.DrawRoundedRectangle(palette.ShadowFar, null, new Rect(-1, 1, w + 2, h + 4), r + 1, r + 1);
				dc.DrawRoundedRectangle(palette.ShadowNear, null, new Rect(0, 1, w, h + 1), r, r);
			}

			Pen border = selected ? palette.AccentPen : palette.NoteStroke;
			Double inset = border.Thickness / 2.0;
			Rect body = new Rect(inset, inset, Math.Max(0.0, w - (2.0 * inset)), Math.Max(0.0, h - (2.0 * inset)));
			dc.DrawRoundedRectangle(palette.NoteFill, null, body, r, r);
			if (selected)
			{
				dc.DrawRoundedRectangle(null, palette.AccentRingPen, body, r, r);
			}

			dc.DrawRoundedRectangle(null, border, body, r, r);
			if (hovered && !selected)
			{
				dc.DrawRoundedRectangle(palette.HoverOverlay, null, body, r, r);
			}

			if (!note.IsEditing)
			{
				Boolean empty = String.IsNullOrEmpty(note.Text);
				String text = empty ? Loc.Get("Str.Flowchart.Note.Placeholder") : note.Text;
				Double pad = DiagramMetrics.NotePadding;
				FormattedText ft = context.Styles.Create(
					text,
					empty ? context.Styles.Italic : context.Styles.Body,
					DiagramMetrics.NoteFontSize,
					empty ? palette.NotePlaceholder : palette.NoteText,
					Math.Max(10.0, w - (2.0 * pad)),
					1000);
				ft.MaxTextHeight = Math.Max(10.0, h - (2.0 * pad));
				dc.DrawText(ft, new Point(pad, pad));
			}
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
