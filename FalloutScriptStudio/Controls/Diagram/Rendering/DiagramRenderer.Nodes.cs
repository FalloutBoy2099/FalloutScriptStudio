namespace FalloutScriptStudio.Controls.Diagram.Rendering
{
	using System;
	using System.Windows;
	using System.Windows.Media;
	using Model;
	using Localization;

	/// <summary>Drawing of nodes (header, rows, links grips) and notes.</summary>
	internal sealed partial class DiagramRenderer
	{
		private const Double DimmedOpacity = 0.45;
		private const Double BadgeRadius = 7.0;

		private void DrawNode(DrawingContext dc, DiagramNode node, Boolean selected, DiagramHitTestResult hover, Boolean cacheContent)
		{
			if (node.IsDimmed)
			{
				dc.PushOpacity(DimmedOpacity);
				this.DrawNodeBody(dc, node, selected, hover, cacheContent);
				dc.Pop();
			}
			else
			{
				this.DrawNodeBody(dc, node, selected, hover, cacheContent);
			}

			if (node.IssueSeverity != DiagramIssueSeverity.None)
			{
				this.DrawIssueMark(dc, node);
			}
		}

		/// <summary>Draws the ring and the badge of a node that has a problem: the shape of the badge (cross, exclamation mark, "i") tells the severity as well as its colour.</summary>
		private void DrawIssueMark(DrawingContext dc, DiagramNode node)
		{
			DiagramPalette palette = this._scene.Context.Palette;
			NodeLayout layout = this._scene.GetLayout(node);
			Double r = DiagramMetrics.NodeCornerRadius;
			dc.DrawRoundedRectangle(null, palette.GetIssueRingPen(node.IssueSeverity), new Rect(-1.5, -1.5, layout.Width + 3.0, layout.Height + 3.0), r + 1.5, r + 1.5);

			Point center = new(3.0, 3.0);
			dc.DrawEllipse(palette.GetIssueBrush(node.IssueSeverity), null, center, BadgeRadius, BadgeRadius);
			switch (node.IssueSeverity)
			{
				case DiagramIssueSeverity.Error:
					dc.DrawLine(palette.BadgeGlyphPen, new Point(center.X - 2.8, center.Y - 2.8), new Point(center.X + 2.8, center.Y + 2.8));
					dc.DrawLine(palette.BadgeGlyphPen, new Point(center.X - 2.8, center.Y + 2.8), new Point(center.X + 2.8, center.Y - 2.8));
					break;
				case DiagramIssueSeverity.Warning:
					dc.DrawLine(palette.BadgeGlyphPen, new Point(center.X, center.Y - 3.6), new Point(center.X, center.Y + 0.8));
					dc.DrawEllipse(palette.BadgeGlyphBrush, null, new Point(center.X, center.Y + 3.2), 0.9, 0.9);
					break;
				default:
					dc.DrawEllipse(palette.BadgeGlyphBrush, null, new Point(center.X, center.Y - 3.2), 0.9, 0.9);
					dc.DrawLine(palette.BadgeGlyphPen, new Point(center.X, center.Y - 1.0), new Point(center.X, center.Y + 3.6));
					break;
			}
		}

		private void DrawNodeBody(DrawingContext dc, DiagramNode node, Boolean selected, DiagramHitTestResult hover, Boolean cacheContent)
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
			Rect body = new(inset, inset, Math.Max(0.0, w - (2.0 * inset)), Math.Max(0.0, h - (2.0 * inset)));
			dc.DrawRoundedRectangle(palette.NodeFill, null, body, r, r);

			dc.PushClip(FrozenRoundedRect(new Rect(0, 0, w, h), r));
			dc.DrawRectangle(palette.GetHeaderTint(node.Kind), null, new Rect(0, 0, w, layout.HeaderHeight));
			dc.Pop();

			if (selected)
			{
				dc.DrawRoundedRectangle(null, palette.AccentRingPen, body, r, r);
			}

			dc.DrawRoundedRectangle(null, border, body, r, r);

			// A node under the pointer is drawn with its hover marks and is not cached; every other node replays its recorded content.
			if (cacheContent && hover.Item is null)
			{
				if (!this._contentCache.TryGetValue(node, out Drawing? content))
				{
					DrawingGroup group = new();
					using (DrawingContext contentContext = group.Open())
					{
						this.DrawNodeContent(contentContext, node, layout, DiagramHitTestResult.Nothing, false);
					}

					if (group.CanFreeze)
					{
						group.Freeze();
					}

					this._contentCache[node] = group;
					content = group;
				}

				dc.DrawDrawing(content);
				return;
			}

			this.DrawNodeContent(dc, node, layout, hover, hovered);
		}

		private void DrawNodeContent(DrawingContext dc, DiagramNode node, NodeLayout layout, DiagramHitTestResult hover, Boolean hovered)
		{
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
			Rect body = new(inset, inset, Math.Max(0.0, w - (2.0 * inset)), Math.Max(0.0, h - (2.0 * inset)));
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
	}
}