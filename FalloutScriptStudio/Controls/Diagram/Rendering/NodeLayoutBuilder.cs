namespace FalloutScriptStudio.Controls.Diagram.Rendering
{
	using System;
	using System.Collections.Generic;
	using System.Windows;
	using System.Windows.Media;
	using FalloutScriptStudio.Controls.Diagram.Model;

	/// <summary>
	/// Lays out the title bar, header lines and rows of a node at a given width.
	/// </summary>
	internal static class NodeLayoutBuilder
	{
		private const Double Unconstrained = 1.0e6;
		private const Double ContentSlack = 4.0;

		/// <summary>
		/// Builds the layout of a node.
		/// </summary>
		/// <param name="node">The node.</param>
		/// <param name="width">Outer width; zero or less selects the automatic width (content width limited to <see cref="DiagramMetrics.AutoWidthLimit"/>).</param>
		/// <param name="context">Palette and fonts.</param>
		public static NodeLayout Build(DiagramNode node, Double width, DiagramRenderContext context)
		{
			NaturalTextCache cache = new NaturalTextCache(context.Styles);
			if (width > 0.0)
			{
				return BuildAtWidth(node, Math.Max(width, node.MinimumWidth), context, cache);
			}

			NodeLayout natural = BuildAtWidth(node, Unconstrained, context, cache);
			Double autoWidth = Math.Max(node.MinimumWidth, Math.Min(natural.ContentWidth, DiagramMetrics.AutoWidthLimit));
			return BuildAtWidth(node, autoWidth, context, cache);
		}

		private static NodeLayout BuildAtWidth(DiagramNode node, Double w, DiagramRenderContext context, NaturalTextCache cache)
		{
			DiagramTextStyles styles = context.Styles;
			DiagramPalette palette = context.Palette;
			Double pad = DiagramMetrics.NodePadding;
			Double contentWidth = 0.0;

			// Title bar.
			Double buttonSize = DiagramMetrics.TitleButtonSize;
			Double buttonTop = (DiagramMetrics.TitleBarHeight - buttonSize) / 2.0;
			Double reserved = pad + buttonSize + DiagramMetrics.TitleButtonGap;
			Rect codeButton = node.ShowCodeButton ? new Rect(pad, buttonTop, buttonSize, buttonSize) : Rect.Empty;
			Rect collapseButton = new Rect(w - pad - buttonSize, buttonTop, buttonSize, buttonSize);
			Double titleAvail = Math.Max(10.0, w - (2.0 * reserved));
			FormattedText title = FitSingleLine(cache, styles, DiagramTextStyles.Normalize(node.Name), styles.Title, DiagramMetrics.TitleFontSize, palette.TextPrimary, titleAvail, out Double titleNatural);
			Point titleOrigin = new Point(reserved + ((titleAvail - title.Width) / 2.0), (DiagramMetrics.TitleBarHeight - title.Height) / 2.0);
			contentWidth = Math.Max(contentWidth, titleNatural + (2.0 * reserved));

			// Header lines.
			Double y = DiagramMetrics.TitleBarHeight;
			List<(FormattedText Text, Point Origin)> headerLines = new List<(FormattedText, Point)>();
			if (!context.LowDetail && node.HeaderLines.Count > 0)
			{
				y += 2.0;
				Double lineAvail = Math.Max(10.0, w - (2.0 * pad));
				foreach (String line in node.HeaderLines)
				{
					FormattedText ft = FitSingleLine(cache, styles, DiagramTextStyles.Normalize(line), styles.Body, DiagramMetrics.HeaderLineFontSize, palette.TextSecondary, lineAvail, out Double natural);
					headerLines.Add((ft, new Point(pad + ((lineAvail - ft.Width) / 2.0), y)));
					y += ft.Height + DiagramMetrics.HeaderLineGap;
					contentWidth = Math.Max(contentWidth, natural + (2.0 * pad));
				}

				y += DiagramMetrics.HeaderBottomPadding;
			}
			else
			{
				y += 4.0;
			}

			Double headerBottom = y;
			List<RowLayout> rows = new List<RowLayout>();
			Double height;
			if (node.IsCollapsed)
			{
				// Still measure the rows so that the automatic width does not change when the node is collapsed.
				LayoutRows(node, w, headerBottom, context, cache, rows, ref contentWidth, measureOnly: true);
				rows.Clear();
				height = headerBottom;
			}
			else
			{
				height = LayoutRows(node, w, headerBottom, context, cache, rows, ref contentWidth, measureOnly: false);
			}

			return new NodeLayout
			{
				Width = w,
				Height = height,
				ContentWidth = contentWidth + ContentSlack,
				HeaderHeight = headerBottom,
				TitleBar = new Rect(0, 0, w, DiagramMetrics.TitleBarHeight),
				CodeButton = codeButton,
				CollapseButton = collapseButton,
				Title = title,
				TitleOrigin = titleOrigin,
				HeaderLines = headerLines,
				Rows = rows,
				TargetAnchorOffset = node.IsCollapsed ? height / 2.0 : DiagramMetrics.TitleBarHeight / 2.0,
				DefaultSourceAnchorOffset = node.IsCollapsed ? height / 2.0 : DiagramMetrics.TitleBarHeight / 2.0
			};
		}

		private static Double LayoutRows(DiagramNode node, Double w, Double top, DiagramRenderContext context, NaturalTextCache cache, List<RowLayout> rows, ref Double contentWidth, Boolean measureOnly)
		{
			DiagramTextStyles styles = context.Styles;
			DiagramPalette palette = context.Palette;
			Double pad = DiagramMetrics.NodePadding;
			Double rowX = pad;
			Double rowW = Math.Max(10.0, w - (2.0 * pad));
			Double y = top + DiagramMetrics.RowsTopPadding;
			Boolean any = false;

			foreach (DiagramRow row in node.Rows)
			{
				if (row.Kind == DiagramRowKind.Code && !node.ShowCodeRows)
				{
					continue;
				}

				any = true;
				Boolean isCode = row.Kind == DiagramRowKind.Code;
				Boolean isMono = isCode || row.Kind == DiagramRowKind.Call;
				Double padX = isCode ? 2.0 : DiagramMetrics.RowPaddingX;
				Double padY = isCode ? 1.0 : DiagramMetrics.RowPaddingY;
				Boolean expandable = row.IsExpandable;
				Boolean hasIcon = !context.LowDetail && !isCode && DiagramIcons.Get(row.Icon) != null;

				Typeface face = isMono ? styles.Mono : styles.Body;
				Double fontSize = isMono ? DiagramMetrics.CodeFontSize : DiagramMetrics.MessageFontSize;
				Brush brush = row.Kind switch
				{
					DiagramRowKind.Reply => palette.ReplyText,
					DiagramRowKind.Option => palette.OptionText,
					DiagramRowKind.Code => palette.TextSecondary,
					_ => palette.TextPrimary
				};

				String text = DiagramTextStyles.Normalize(row.Text);
				if (context.LowDetail && (row.Kind == DiagramRowKind.Reply || row.Kind == DiagramRowKind.Option))
				{
					text = "● " + text;
				}

				Double decorations = (2.0 * padX) + (expandable ? DiagramMetrics.ToggleZoneWidth : 0.0) + (hasIcon ? DiagramMetrics.IconSize + DiagramMetrics.IconGap : 0.0);
				Double textAvail = Math.Max(10.0, rowW - decorations);
				Int32 maxLines = isMono ? 1 : DiagramMetrics.MessageMaxLines;
				FormattedText natural = cache.Get(text, face, fontSize, brush);
				contentWidth = Math.Max(contentWidth, natural.Width + decorations + (2.0 * pad));

				Boolean showDetail = expandable && row.IsExpanded;
				FormattedText? detailNatural = null;
				Double detailDecorations = DiagramMetrics.DetailIndent + (2.0 * DiagramMetrics.RowPaddingX) + DiagramMetrics.IconSize + DiagramMetrics.IconGap;
				if (showDetail)
				{
					detailNatural = cache.Get(DiagramTextStyles.Normalize(row.Detail), styles.Mono, DiagramMetrics.CodeFontSize, palette.TextPrimary);
					contentWidth = Math.Max(contentWidth, detailNatural.Width + detailDecorations + (2.0 * pad));
				}

				if (measureOnly)
				{
					continue;
				}

				Boolean textFits = natural.Width <= textAvail;
				FormattedText display = textFits ? natural : styles.Create(text, face, fontSize, brush, textAvail, maxLines);
				Boolean trimmed = !textFits && (maxLines == 1 || text.IndexOf(' ', StringComparison.Ordinal) < 0 || natural.Width > textAvail * maxLines
					|| styles.Create(text, face, fontSize, brush, textAvail, 1000).Height > display.Height + 0.5);
				Double headerHeight = display.Height + (2.0 * padY);
				Rect header = new Rect(rowX, y, rowW, headerHeight);
				Double contentLeft = rowX + padX;
				Rect toggle = Rect.Empty;
				if (expandable)
				{
					toggle = new Rect(rowX, y, padX + DiagramMetrics.ToggleZoneWidth, headerHeight);
					contentLeft += DiagramMetrics.ToggleZoneWidth;
				}

				Rect icon = Rect.Empty;
				if (hasIcon)
				{
					icon = new Rect(contentLeft, y + padY + ((natural.Height - DiagramMetrics.IconSize) / 2.0), DiagramMetrics.IconSize, DiagramMetrics.IconSize);
					contentLeft += DiagramMetrics.IconSize + DiagramMetrics.IconGap;
				}

				RowLayout layout = new RowLayout(row)
				{
					Header = header,
					Toggle = toggle,
					Icon = icon,
					Text = display,
					TextOrigin = new Point(contentLeft, y + padY),
					AnchorY = y + (headerHeight / 2.0),
					IsTextTrimmed = trimmed
				};

				Double rowBottom = y + headerHeight;
				if (showDetail && detailNatural != null)
				{
					Double detailAvail = Math.Max(10.0, rowW - detailDecorations);
					FormattedText detailDisplay = detailNatural.Width <= detailAvail
						? detailNatural
						: styles.Create(DiagramTextStyles.Normalize(row.Detail), styles.Mono, DiagramMetrics.CodeFontSize, palette.TextPrimary, detailAvail);
					Double detailHeight = detailDisplay.Height + (2.0 * DiagramMetrics.RowPaddingY);
					Double detailTop = rowBottom + DiagramMetrics.DetailGap;
					Rect detail = new Rect(rowX + DiagramMetrics.DetailIndent, detailTop, rowW - DiagramMetrics.DetailIndent, detailHeight);
					layout.Detail = detail;
					layout.DetailIcon = new Rect(detail.X + DiagramMetrics.RowPaddingX, detailTop + ((detailHeight - DiagramMetrics.IconSize) / 2.0), DiagramMetrics.IconSize, DiagramMetrics.IconSize);
					layout.DetailText = detailDisplay;
					layout.DetailOrigin = new Point(detail.X + DiagramMetrics.RowPaddingX + DiagramMetrics.IconSize + DiagramMetrics.IconGap, detailTop + DiagramMetrics.RowPaddingY);
					rowBottom = detail.Bottom;
				}

				rows.Add(layout);
				y = rowBottom + DiagramMetrics.RowSpacing;
			}

			if (measureOnly)
			{
				return top;
			}

			if (any)
			{
				y -= DiagramMetrics.RowSpacing;
			}
			else
			{
				y = top;
			}

			return y + (any ? DiagramMetrics.RowsBottomPadding : DiagramMetrics.RowsBottomPadding / 2.0);
		}

		private static FormattedText FitSingleLine(NaturalTextCache cache, DiagramTextStyles styles, String text, Typeface face, Double size, Brush brush, Double avail, out Double naturalWidth)
		{
			FormattedText natural = cache.Get(text, face, size, brush);
			naturalWidth = natural.Width;
			return natural.Width <= avail ? natural : styles.Create(text, face, size, brush, avail);
		}

		// Shares the unconstrained (single line) text measurements between the passes of one layout.
		private sealed class NaturalTextCache
		{
			private readonly DiagramTextStyles _styles;
			private readonly Dictionary<(String Text, Typeface Face, Double Size, Brush Brush), FormattedText> _items = new Dictionary<(String, Typeface, Double, Brush), FormattedText>();

			public NaturalTextCache(DiagramTextStyles styles)
			{
				this._styles = styles;
			}

			public FormattedText Get(String text, Typeface face, Double size, Brush brush)
			{
				(String, Typeface, Double, Brush) key = (text, face, size, brush);
				if (!this._items.TryGetValue(key, out FormattedText? formatted))
				{
					formatted = this._styles.Create(text, face, size, brush);
					this._items[key] = formatted;
				}

				return formatted;
			}
		}
	}
}
