namespace FalloutScriptStudio.Editor.Rendering
{
	using System;
	using System.Collections.Generic;
	using ICSharpCode.AvalonEdit.Document;
	using ICSharpCode.AvalonEdit.Rendering;
	using Core.TextTools;

	/// <summary>
	/// Colors the occurrences of the highlighted word (legacy "Highlight" on middle click) and the search matches by
	/// replacing the background and foreground of the matching text.
	/// </summary>
	public sealed class HighlightColorizer : DocumentColorizingTransformer
	{
		private readonly TextSegmentCollection<TextSegment> _occurrences;
		private readonly TextSegmentCollection<TextSegment> _matches;
		private readonly TextSegmentCollection<TextSegment> _flash;
		private EditorPalette _palette;

		public HighlightColorizer(TextDocument document, EditorPalette palette)
		{
			this._occurrences = new(document);
			this._matches = new(document);
			this._flash = new(document);
			this._palette = palette;
		}

		/// <summary>Gets the number of highlighted occurrences.</summary>
		public Int32 OccurrenceCount => this._occurrences.Count;

		/// <summary>Gets the number of marked search matches.</summary>
		public Int32 MatchCount => this._matches.Count;

		/// <summary>Gets the number of ranges marked as just inserted.</summary>
		public Int32 FlashCount => this._flash.Count;

		/// <summary>Replaces the colors.</summary>
		/// <param name="palette">The palette of the current theme.</param>
		public void SetPalette(EditorPalette palette)
		{
			this._palette = palette;
		}

		/// <summary>Replaces the highlighted occurrences.</summary>
		/// <param name="spans">The ranges to highlight.</param>
		public void SetOccurrences(IEnumerable<TextSpan> spans)
		{
			Replace(this._occurrences, spans);
		}

		/// <summary>Replaces the marked search matches.</summary>
		/// <param name="spans">The ranges to mark.</param>
		public void SetMatches(IEnumerable<TextSpan> spans)
		{
			Replace(this._matches, spans);
		}

		/// <summary>Replaces the ranges that are marked as just inserted (legacy <c>IncludeHighlight</c> block).</summary>
		/// <param name="spans">The ranges to mark.</param>
		public void SetFlash(IEnumerable<TextSpan> spans)
		{
			Replace(this._flash, spans);
		}

		/// <inheritdoc />
		protected override void ColorizeLine(DocumentLine line)
		{
			if (this._flash.Count > 0)
			{
				foreach (TextSegment segment in this._flash.FindOverlappingSegments(line))
				{
					Int32 start = Math.Max(segment.StartOffset, line.Offset);
					Int32 end = Math.Min(segment.EndOffset, line.EndOffset);
					if (start < end)
					{
						this.ChangeLinePart(start, end, element => element.TextRunProperties.SetBackgroundBrush(this._palette.IncludeHighlight));
					}
				}
			}

			if (this._occurrences.Count > 0)
			{
				this.ColorizeSegments(line, this._occurrences, this._palette.SearchBackground);
			}

			if (this._matches.Count > 0)
			{
				this.ColorizeSegments(line, this._matches, this._palette.SearchMatchBackground);
			}
		}

		private static void Replace(TextSegmentCollection<TextSegment> collection, IEnumerable<TextSpan> spans)
		{
			collection.Clear();
			foreach (TextSpan span in spans)
			{
				if (span.Length > 0)
				{
					collection.Add(new TextSegment { StartOffset = span.Offset, Length = span.Length });
				}
			}
		}

		private void ColorizeSegments(DocumentLine line, TextSegmentCollection<TextSegment> segments, System.Windows.Media.Brush background)
		{
			System.Windows.Media.Brush foreground = this._palette.SearchForeground;
			foreach (TextSegment segment in segments.FindOverlappingSegments(line))
			{
				Int32 start = Math.Max(segment.StartOffset, line.Offset);
				Int32 end = Math.Min(segment.EndOffset, line.EndOffset);
				if (start < end)
				{
					this.ChangeLinePart(start, end, element =>
					{
						element.TextRunProperties.SetBackgroundBrush(background);
						element.TextRunProperties.SetForegroundBrush(foreground);
					});
				}
			}
		}
	}
}