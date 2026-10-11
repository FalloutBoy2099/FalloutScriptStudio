namespace FalloutScriptStudio.Editor.Rendering
{
	using System;
	using System.Collections.Generic;
	using System.Windows;
	using System.Windows.Media;
	using ICSharpCode.AvalonEdit.Document;
	using ICSharpCode.AvalonEdit.Rendering;

	/// <summary>
	/// Draws an arrow in every visible tab character. AvalonEdit has one color for all non-printable characters; the legacy editor
	/// drew tabs and spaces in different colors (<c>TabMarkers</c> / <c>SpaceMarkers</c>), so the tabs are drawn here.
	/// </summary>
	public sealed class TabMarkerRenderer : IBackgroundRenderer
	{
		private const Double ArrowHead = 3.5;
		private Brush _brush = Brushes.Gray;
		private Pen? _pen;

		/// <inheritdoc />
		public KnownLayer Layer => KnownLayer.Selection;

		/// <summary>Gets or sets a value indicating whether the arrows are drawn.</summary>
		public Boolean IsEnabled { get; set; }

		/// <summary>Gets the color of the arrows.</summary>
		public Brush Brush => this._brush;

		/// <summary>Sets the color of the arrows.</summary>
		/// <param name="brush">The brush.</param>
		public void SetBrush(Brush brush)
		{
			this._brush = brush;
			this._pen = null;
		}

		/// <summary>Finds the tab characters in a piece of text.</summary>
		/// <param name="text">The text.</param>
		/// <param name="offset">The offset of the first character of <paramref name="text"/> in the document.</param>
		/// <returns>The document offsets of the tabs.</returns>
		public static IReadOnlyList<Int32> FindTabs(String text, Int32 offset)
		{
			List<Int32> tabs = new List<Int32>();
			for (Int32 i = text.IndexOf('\t', StringComparison.Ordinal); i >= 0; i = text.IndexOf('\t', i + 1))
			{
				tabs.Add(offset + i);
			}

			return tabs;
		}

		/// <inheritdoc />
		public void Draw(TextView textView, DrawingContext drawingContext)
		{
			if (!this.IsEnabled || !textView.VisualLinesValid)
			{
				return;
			}

			Pen pen = this._pen ??= CreatePen(this._brush);
			TextDocument document = textView.Document;
			foreach (VisualLine visualLine in textView.VisualLines)
			{
				for (DocumentLine? line = visualLine.FirstDocumentLine; line is not null; line = line.NextLine)
				{
					foreach (Int32 offset in FindTabs(document.GetText(line.Offset, line.Length), line.Offset))
					{
						foreach (Rect rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, new TextSegment { StartOffset = offset, Length = 1 }))
						{
							DrawArrow(drawingContext, pen, rect);
						}
					}

					if (line == visualLine.LastDocumentLine)
					{
						break;
					}
				}
			}
		}

		private static Pen CreatePen(Brush brush)
		{
			Pen pen = new Pen(brush, 1);
			pen.Freeze();
			return pen;
		}

		private static void DrawArrow(DrawingContext drawingContext, Pen pen, Rect rect)
		{
			if (rect.Width < 2 * ArrowHead)
			{
				return;
			}

			Double y = Math.Round(rect.Top + (rect.Height / 2)) + 0.5;
			Double left = rect.Left + 2;
			Double right = rect.Right - 2;
			StreamGeometry geometry = new StreamGeometry();
			using (StreamGeometryContext context = geometry.Open())
			{
				context.BeginFigure(new Point(left, y), false, false);
				context.LineTo(new Point(right, y), true, false);
				context.BeginFigure(new Point(right - ArrowHead, y - ArrowHead), false, false);
				context.LineTo(new Point(right, y), true, false);
				context.LineTo(new Point(right - ArrowHead, y + ArrowHead), true, false);
			}

			geometry.Freeze();
			drawingContext.DrawGeometry(null, pen, geometry);
		}
	}
}
