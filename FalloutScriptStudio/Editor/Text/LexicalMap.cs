namespace FalloutScriptStudio.Editor.Text
{
	using System;
	using System.Collections.Generic;
	using ICSharpCode.AvalonEdit.Document;

	/// <summary>
	/// Immutable map of the comments, strings and brace blocks of an SSL text. It answers "is this position real code?"
	/// without a visual tree (legacy <c>ColorTheme.CheckColorPosition</c> inspected the highlight colors for that).
	/// </summary>
	public sealed class LexicalMap
	{
		/// <summary>The number of characters read around an edit at first; bigger windows are tried when the scan has not found its way back by then.</summary>
		private const Int32 FirstWindow = 4096;

		/// <summary>The biggest window; it stays below the size from which .NET allocates strings on the large object heap.</summary>
		private const Int32 LastWindow = 4096 * 8;

		private static readonly LexicalMap _empty = new LexicalMap(Array.Empty<LexicalSpan>());

		private readonly LexicalSpan[] _spans;

		private LexicalMap(LexicalSpan[] spans)
		{
			this._spans = spans;
		}

		/// <summary>Gets a map of an empty text.</summary>
		public static LexicalMap Empty => _empty;

		/// <summary>Gets the non-code regions ordered by offset.</summary>
		public IReadOnlyList<LexicalSpan> Spans => this._spans;

		/// <summary>Scans a text.</summary>
		/// <param name="text">The whole text of the document.</param>
		/// <returns>The map.</returns>
		public static LexicalMap Build(String text)
		{
			if (text.Length == 0)
			{
				return _empty;
			}

			List<LexicalSpan> spans = new List<LexicalSpan>();
			Scan(text, 0, spans, null);
			return new LexicalMap(spans.ToArray());
		}

		/// <summary>
		/// Brings a map up to date after one edit of the text without scanning the whole text again: only the text around the edit is read,
		/// up to the place where the scan is in the same state as the scan of the old text was (an edit that opens a block comment or a string
		/// changes what follows, so the scan goes on until it is back in step).
		/// </summary>
		/// <param name="old">The map of the text before the edit.</param>
		/// <param name="text">The text after the edit.</param>
		/// <param name="offset">Where the edit starts.</param>
		/// <param name="removed">The number of characters the edit removed.</param>
		/// <param name="inserted">The number of characters the edit inserted.</param>
		/// <returns>The map of the new text, or <see langword="null"/> when the edit cannot be followed (scan the whole text then).</returns>
		public static LexicalMap? TryUpdate(LexicalMap old, ITextSource text, Int32 offset, Int32 removed, Int32 inserted)
		{
			LexicalSpan[] spans = old._spans;
			Int32 newLength = text.TextLength;
			if (offset < 0 || removed < 0 || inserted < 0 || offset + inserted > newLength || newLength == 0)
			{
				return null;
			}

			Int32 delta = inserted - removed;
			Int32 editEnd = offset + inserted;

			// The scan restarts where the scanner of the old text certainly was in plain code: just before the edit (a typed character can
			// turn the one before it into the start of a comment), or at the start of the first span that the edit touches or extends.
			// A line ends before its carriage return, so an edit between the carriage return and the line feed changes the span before it.
			Int32 touched = FirstSpanEndingAtOrAfter(spans, Math.Max(0, offset - 1));
			Int32 scanFrom = Math.Max(0, offset - 1);
			if (touched < spans.Length && spans[touched].Start < scanFrom)
			{
				scanFrom = spans[touched].Start;
			}

			for (Int32 size = FirstWindow; size <= LastWindow; size *= 8)
			{
				Int32 length = Math.Min(size, newLength - scanFrom);
				if (length <= 0)
				{
					return null;
				}

				String window = text.GetText(scanFrom, length);
				Boolean toEnd = scanFrom + length >= newLength;
				List<LexicalSpan> fresh = new List<LexicalSpan>();

				// Back in step: past the edit, and where the scanner of the old text was in plain code too, so that the old spans from there on stay valid.
				Int32 stopped = Scan(window, scanFrom, fresh, position => position >= editEnd && !IsInsideSpan(spans, position - delta));
				if (stopped < 0 && !toEnd)
				{
					continue;
				}

				Int32 before = FirstSpanStartingAtOrAfter(spans, scanFrom);
				Int32 after = stopped < 0 ? spans.Length : FirstSpanStartingAtOrAfter(spans, scanFrom + stopped - delta);
				LexicalSpan[] merged = new LexicalSpan[before + fresh.Count + (spans.Length - after)];
				Array.Copy(spans, 0, merged, 0, before);
				fresh.CopyTo(merged, before);
				Int32 target = before + fresh.Count;
				for (Int32 i = after; i < spans.Length; i++)
				{
					LexicalSpan span = spans[i];
					merged[target++] = span with { Start = span.Start + delta };
				}

				return merged.Length == 0 ? _empty : new LexicalMap(merged);
			}

			return null;
		}

		/// <summary>Gets the kind of the character at an offset.</summary>
		/// <param name="offset">Offset of the character.</param>
		/// <returns>The lexical kind; <see cref="LexicalKind.Code"/> outside every span.</returns>
		public LexicalKind GetKindAt(Int32 offset)
		{
			Int32 index = this.FindSpanContaining(offset);
			return index < 0 ? LexicalKind.Code : this._spans[index].Kind;
		}

		/// <summary>Finds the region that contains the character at an offset.</summary>
		/// <param name="offset">Offset of the character.</param>
		/// <param name="span">Receives the region.</param>
		/// <returns><see langword="true"/> when the character is not plain code.</returns>
		public Boolean TryGetSpanAt(Int32 offset, out LexicalSpan span)
		{
			Int32 index = this.FindSpanContaining(offset);
			span = index < 0 ? default : this._spans[index];
			return index >= 0;
		}

		/// <summary>Determines whether the character at an offset is code.</summary>
		/// <param name="offset">Offset of the character.</param>
		/// <returns><see langword="true"/> when it is outside comments, strings and brace blocks.</returns>
		public Boolean IsCodeAt(Int32 offset)
		{
			return this.GetKindAt(offset) == LexicalKind.Code;
		}

		/// <summary>
		/// Gets the kind of the region the insertion point (caret) is in: text typed there would become part of that region.
		/// </summary>
		/// <param name="caret">The caret offset (between two characters).</param>
		/// <returns>The kind of the region.</returns>
		public LexicalKind GetKindAtCaret(Int32 caret)
		{
			Int32 index = this.FindSpanContaining(caret - 1);
			if (index >= 0)
			{
				LexicalSpan span = this._spans[index];
				if (caret < span.End || !span.IsClosed)
				{
					return span.Kind;
				}
			}

			return LexicalKind.Code;
		}

		/// <summary>Determines whether text typed at the caret would be code.</summary>
		/// <param name="caret">The caret offset.</param>
		/// <returns><see langword="true"/> when the caret is outside comments, strings and brace blocks.</returns>
		public Boolean IsCodeAtCaret(Int32 caret)
		{
			return this.GetKindAtCaret(caret) == LexicalKind.Code;
		}

		/// <summary>
		/// Scans a piece of the text. Without <paramref name="stopAt"/> the whole piece is scanned.
		/// </summary>
		/// <param name="text">The piece; it starts at an offset where the scanner is in plain code.</param>
		/// <param name="baseOffset">The offset of the piece in the document.</param>
		/// <param name="spans">Receives the regions found, with offsets of the document.</param>
		/// <param name="stopAt">Asked (with an offset of the document) before each region or character that is read; scanning stops when it says so.</param>
		/// <returns>The index in <paramref name="text"/> where the scan stopped, or -1 when it reached the end of the piece.</returns>
		private static Int32 Scan(String text, Int32 baseOffset, List<LexicalSpan> spans, Func<Int32, Boolean>? stopAt)
		{
			Int32 length = text.Length;
			Int32 i = 0;
			while (i < length)
			{
				if (stopAt is not null && stopAt(baseOffset + i))
				{
					return i;
				}

				Char c = text[i];
				if (c == '/' && i + 1 < length && text[i + 1] == '/')
				{
					Int32 end = FindLineEnd(text, i);
					spans.Add(new LexicalSpan(baseOffset + i, end - i, LexicalKind.LineComment, false));
					i = end;
				}
				else if (c == '/' && i + 1 < length && text[i + 1] == '*')
				{
					Int32 close = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
					Int32 end = close < 0 ? length : close + 2;
					spans.Add(new LexicalSpan(baseOffset + i, end - i, LexicalKind.BlockComment, close >= 0));
					i = end;
				}
				else if (c == '"')
				{
					Int32 j = i + 1;
					Boolean closed = false;
					while (j < length)
					{
						Char ch = text[j];
						if (ch == '\\' && j + 1 < length && text[j + 1] != '\r' && text[j + 1] != '\n')
						{
							j += 2;
							continue;
						}

						if (ch == '"')
						{
							closed = true;
							j++;
							break;
						}

						if (ch == '\r' || ch == '\n')
						{
							break;
						}

						j++;
					}

					spans.Add(new LexicalSpan(baseOffset + i, j - i, LexicalKind.String, closed));
					i = j;
				}
				else if (c == '{')
				{
					Int32 j = i + 1;
					Boolean closed = false;
					while (j < length)
					{
						Char ch = text[j];
						if (ch == '}')
						{
							closed = true;
							j++;
							break;
						}

						if (ch == '\r' || ch == '\n')
						{
							break;
						}

						j++;
					}

					spans.Add(new LexicalSpan(baseOffset + i, j - i, LexicalKind.Braces, closed));
					i = j;
				}
				else
				{
					i++;
				}
			}

			return -1;
		}

		/// <summary>Finds the first span that ends at or after an offset (the spans are in order and do not overlap).</summary>
		private static Int32 FirstSpanEndingAtOrAfter(LexicalSpan[] spans, Int32 offset)
		{
			Int32 low = 0;
			Int32 high = spans.Length;
			while (low < high)
			{
				Int32 mid = (low + high) >> 1;
				if (spans[mid].End < offset)
				{
					low = mid + 1;
				}
				else
				{
					high = mid;
				}
			}

			return low;
		}

		/// <summary>Finds the first span that starts at or after an offset.</summary>
		private static Int32 FirstSpanStartingAtOrAfter(LexicalSpan[] spans, Int32 offset)
		{
			Int32 low = 0;
			Int32 high = spans.Length;
			while (low < high)
			{
				Int32 mid = (low + high) >> 1;
				if (spans[mid].Start < offset)
				{
					low = mid + 1;
				}
				else
				{
					high = mid;
				}
			}

			return low;
		}

		/// <summary>Determines whether a position is in the middle of a span (a position at the start of a span is not).</summary>
		private static Boolean IsInsideSpan(LexicalSpan[] spans, Int32 position)
		{
			Int32 index = FirstSpanStartingAtOrAfter(spans, position) - 1;
			return index >= 0 && spans[index].Start < position && position < spans[index].End;
		}

		private static Int32 FindLineEnd(String text, Int32 from)
		{
			Int32 end = text.IndexOf('\n', from);
			if (end < 0)
			{
				return text.Length;
			}

			return end > from && text[end - 1] == '\r' ? end - 1 : end;
		}

		private Int32 FindSpanContaining(Int32 offset)
		{
			Int32 low = 0;
			Int32 high = this._spans.Length - 1;
			while (low <= high)
			{
				Int32 mid = (low + high) >> 1;
				LexicalSpan span = this._spans[mid];
				if (offset < span.Start)
				{
					high = mid - 1;
				}
				else if (offset >= span.End)
				{
					low = mid + 1;
				}
				else
				{
					return mid;
				}
			}

			return -1;
		}
	}
}
