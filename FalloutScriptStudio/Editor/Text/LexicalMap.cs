namespace FalloutScriptStudio.Editor.Text
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// Immutable map of the comments, strings and brace blocks of an SSL text. It answers "is this position real code?"
	/// without a visual tree (legacy <c>ColorTheme.CheckColorPosition</c> inspected the highlight colors for that).
	/// </summary>
	public sealed class LexicalMap
	{
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
			Int32 length = text.Length;
			Int32 i = 0;
			while (i < length)
			{
				Char c = text[i];
				if (c == '/' && i + 1 < length && text[i + 1] == '/')
				{
					Int32 end = FindLineEnd(text, i);
					spans.Add(new LexicalSpan(i, end - i, LexicalKind.LineComment, false));
					i = end;
				}
				else if (c == '/' && i + 1 < length && text[i + 1] == '*')
				{
					Int32 close = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
					Int32 end = close < 0 ? length : close + 2;
					spans.Add(new LexicalSpan(i, end - i, LexicalKind.BlockComment, close >= 0));
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

					spans.Add(new LexicalSpan(i, j - i, LexicalKind.String, closed));
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

					spans.Add(new LexicalSpan(i, j - i, LexicalKind.Braces, closed));
					i = j;
				}
				else
				{
					i++;
				}
			}

			return new LexicalMap(spans.ToArray());
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
