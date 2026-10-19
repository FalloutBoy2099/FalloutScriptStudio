namespace FalloutScriptStudio.Editor.Rendering
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Windows;
	using System.Windows.Media;
	using ICSharpCode.AvalonEdit.Document;
	using ICSharpCode.AvalonEdit.Rendering;

	/// <summary>Draws the wavy underlines of the parser diagnostics (replaces the legacy <c>TextMarker</c> wave lines).</summary>
	public sealed class DiagnosticRenderer : IBackgroundRenderer
	{
		private readonly TextSegmentCollection<DiagnosticMarker> _markers;
		private EditorPalette _palette;

		public DiagnosticRenderer(TextDocument document, EditorPalette palette)
		{
			this._markers = new(document);
			this._palette = palette;
		}

		/// <inheritdoc />
		public KnownLayer Layer => KnownLayer.Selection;

		/// <summary>Gets the number of marks.</summary>
		public Int32 Count => this._markers.Count;

		/// <summary>Replaces the colors of the marks.</summary>
		/// <param name="palette">The palette of the current theme.</param>
		public void SetPalette(EditorPalette palette)
		{
			this._palette = palette;
		}

		/// <summary>Adds a mark.</summary>
		/// <param name="marker">The mark.</param>
		public void Add(DiagnosticMarker marker)
		{
			this._markers.Add(marker);
		}

		/// <summary>Removes all marks.</summary>
		public void Clear()
		{
			this._markers.Clear();
		}

		/// <summary>Determines whether any mark touches a line.</summary>
		/// <param name="line">The line.</param>
		/// <returns><see langword="true"/> when a mark overlaps the line.</returns>
		public Boolean HasMarkerOnLine(DocumentLine line)
		{
			return this._markers.FindOverlappingSegments(line.Offset, line.Length + 1).Count > 0;
		}

		/// <summary>Gets the marks that cover a character.</summary>
		/// <param name="offset">The offset of the character.</param>
		/// <returns>The marks, the most severe first.</returns>
		public IReadOnlyList<DiagnosticMarker> GetMarkersAt(Int32 offset)
		{
			return this._markers.FindSegmentsContaining(offset).OrderBy(m => m.Severity).ToList();
		}

		/// <inheritdoc />
		public void Draw(TextView textView, DrawingContext drawingContext)
		{
			if (this._markers.Count == 0 || !textView.VisualLinesValid)
			{
				return;
			}

			IList<VisualLine> visualLines = textView.VisualLines;
			if (visualLines.Count == 0)
			{
				return;
			}

			Int32 viewStart = visualLines[0].FirstDocumentLine.Offset;
			Int32 viewEnd = visualLines[visualLines.Count - 1].LastDocumentLine.EndOffset;
			foreach (DiagnosticMarker marker in this._markers.FindOverlappingSegments(viewStart, viewEnd - viewStart))
			{
				DocumentLine markerLine = textView.Document.GetLineByOffset(marker.StartOffset);
				VisualLine? visualLine = textView.GetVisualLine(markerLine.LineNumber);
				if (visualLine is null || visualLine.FirstDocumentLine != markerLine)
				{
					continue;
				}

				Brush brush = marker.Severity switch
				{
					MarkerSeverity.Error => this._palette.ErrorUnderline,
					MarkerSeverity.Warning => this._palette.WarningUnderline,
					MarkerSeverity.IncludeError => this._palette.IncludeErrorUnderline,
					MarkerSeverity.Unused => this._palette.UnusedUnderline,
					_ => this._palette.MessageUnderline,
				};
				Pen pen = new(brush, 1);
				if (marker.Severity == MarkerSeverity.Unused)
				{
					pen.DashStyle = new DashStyle(new Double[] { 1, 2 }, 0);
				}

				pen.Freeze();

				foreach (Rect rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, marker))
				{
					if (marker.Severity == MarkerSeverity.Unused)
					{
						drawingContext.DrawLine(pen, new Point(rect.Left, rect.Bottom - 1), new Point(rect.Right, rect.Bottom - 1));
					}
					else
					{
						DrawWave(drawingContext, pen, rect);
					}
				}
			}
		}

		private static void DrawWave(DrawingContext drawingContext, Pen pen, Rect rect)
		{
			const Double Step = 2.5;
			Point start = rect.BottomLeft;
			Point end = rect.BottomRight;
			Int32 count = Math.Max((Int32)((end.X - start.X) / Step) + 1, 4);

			StreamGeometry geometry = new();
			using (StreamGeometryContext context = geometry.Open())
			{
				context.BeginFigure(new Point(start.X, start.Y - 1), false, false);
				List<Point> points = new(count);
				for (Int32 i = 0; i < count; i++)
				{
					points.Add(new Point(start.X + (i * Step), start.Y - 1 - (((i + 1) % 2 == 0) ? Step : 0)));
				}

				context.PolyLineTo(points, true, false);
			}

			geometry.Freeze();
			drawingContext.DrawGeometry(null, pen, geometry);
		}
	}
}