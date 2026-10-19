namespace FalloutScriptStudio.Core.TextTools
{
	using System;
	using System.Buffers;
	using System.Collections.Generic;

	/// <summary>
	/// Text helpers for the selection: highlighting the other occurrences of the selected text and selecting a <c>{placeholder}</c>
	/// (legacy <c>Utilities.HighlightingSelectedText</c> and <c>SelectedTextColorRegion</c>).
	/// </summary>
	public static class SelectionHelper
	{
		private static readonly SearchValues<Char> _colonOrComma = SearchValues.Create(":,");

		/// <summary>
		/// Finds the other places where the selected text occurs.
		/// </summary>
		/// <param name="text">The document text.</param>
		/// <param name="selection">The selection.</param>
		/// <returns>
		/// All occurrences (including the selection itself), case-sensitive. When the selection is exactly one word only whole-word
		/// occurrences count. Nothing is returned for an empty selection, for white space only and for two non-alphanumeric characters.
		/// </returns>
		public static IReadOnlyList<TextSpan> FindOccurrences(String text, TextSpan selection)
		{
			ArgumentNullException.ThrowIfNull(text);

			var result = new List<TextSpan>();
			if (selection.IsEmpty || selection.End > text.Length)
			{
				return result;
			}

			String word = text.Substring(selection.Offset, selection.Length).Trim();
			if (word.Length == 0 || (word.Length == 2 && !Char.IsLetterOrDigit(word[0]) && !Char.IsLetterOrDigit(word[1])))
			{
				return result;
			}

			TextSpan? wordSpan = WordScanner.FindWordAt(text, text.IndexOf(word, selection.Offset, StringComparison.Ordinal));
			Boolean wholeWord = wordSpan is not null && wordSpan.Value.Length == word.Length && String.Equals(text.Substring(wordSpan.Value.Offset, wordSpan.Value.Length), word, StringComparison.Ordinal);

			Int32 seek = 0;
			while (seek < text.Length)
			{
				seek = text.IndexOf(word, seek, StringComparison.Ordinal);
				if (seek == -1)
				{
					break;
				}

				Boolean startsWord = seek == 0 || !WordScanner.IsWordChar(text[seek - 1]);
				Boolean endsWord = seek + word.Length >= text.Length || !WordScanner.IsWordChar(text[seek + word.Length]);
				if (!wholeWord || (startsWord && endsWord))
				{
					result.Add(new TextSpan(seek, word.Length));
				}

				seek += word.Length;
			}

			return result;
		}

		/// <summary>
		/// Finds the <c>{placeholder}</c> of a code template around (or next to) the caret, to select it as a whole.
		/// </summary>
		/// <param name="text">The document text.</param>
		/// <param name="caretOffset">The caret offset.</param>
		/// <returns>
		/// The span from <c>{</c> to <c>}</c> inclusive, or <c>null</c> when the caret is not in a one-line brace pair that has
		/// something inside and contains neither <c>:</c> nor <c>,</c> (those braces are array or map literals, not placeholders).
		/// </returns>
		public static TextSpan? FindPlaceholder(String text, Int32 caretOffset)
		{
			ArgumentNullException.ThrowIfNull(text);

			Int32 caret = Math.Clamp(caretOffset, 0, text.Length);
			var map = new TextLineMap(text);
			Int32 line = map.GetLineFromOffset(caret);
			Int32 lineStart = map.GetLineStart(line);
			Int32 lineEnd = map.GetLineEnd(line);

			Int32 open = -1;
			for (Int32 i = Math.Min(caret, lineEnd - 1); i >= lineStart; i--)
			{
				if (text[i] == '}' && i < caret)
				{
					return null;
				}

				if (text[i] == '{')
				{
					open = i;
					break;
				}
			}

			if (open < 0)
			{
				return null;
			}

			Int32 close = text.IndexOf('}', open + 1);
			if (close < 0 || close >= lineEnd || close - open < 2)
			{
				return null;
			}

			String inside = text.Substring(open + 1, close - open - 1);
			return inside.AsSpan().IndexOfAny(_colonOrComma) >= 0 ? null : TextSpan.FromBounds(open, close + 1);
		}
	}
}