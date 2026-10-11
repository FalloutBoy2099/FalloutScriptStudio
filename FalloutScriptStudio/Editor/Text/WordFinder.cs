namespace FalloutScriptStudio.Editor.Text
{
	using System;
	using ICSharpCode.AvalonEdit.Document;

	/// <summary>Finds identifiers (letters, digits and underscores) in a text (legacy <c>TextUtilities.GetWordAt</c> and friends).</summary>
	public static class WordFinder
	{
		/// <summary>Determines whether a character can be part of an identifier.</summary>
		/// <param name="c">The character.</param>
		/// <returns><see langword="true"/> for letters, digits and the underscore.</returns>
		public static Boolean IsWordChar(Char c)
		{
			return Char.IsLetterOrDigit(c) || c == '_';
		}

		/// <summary>Gets the identifier that contains the character at an offset.</summary>
		/// <param name="text">The text.</param>
		/// <param name="offset">Offset of a character.</param>
		/// <returns>The word, or an empty string when the character is not part of an identifier.</returns>
		public static String GetWordAt(ITextSource text, Int32 offset)
		{
			return TryGetWordRange(text, offset, out Int32 start, out Int32 length)
				? text.GetText(start, length)
				: String.Empty;
		}

		/// <summary>
		/// Gets the identifier at the caret: the one that contains the character right of the caret or, when there is
		/// none, the one that ends at the caret.
		/// </summary>
		/// <param name="text">The text.</param>
		/// <param name="caret">The caret offset.</param>
		/// <returns>The word, or an empty string.</returns>
		public static String GetWordNearCaret(ITextSource text, Int32 caret)
		{
			return TryGetWordRangeNearCaret(text, caret, out Int32 start, out Int32 length)
				? text.GetText(start, length)
				: String.Empty;
		}

		/// <summary>Finds the range of the identifier that contains the character at an offset.</summary>
		/// <param name="text">The text.</param>
		/// <param name="offset">Offset of a character.</param>
		/// <param name="start">Receives the offset of the first character of the word.</param>
		/// <param name="length">Receives the length of the word.</param>
		/// <returns><see langword="true"/> when the character is part of an identifier.</returns>
		public static Boolean TryGetWordRange(ITextSource text, Int32 offset, out Int32 start, out Int32 length)
		{
			start = 0;
			length = 0;
			if (offset < 0 || offset >= text.TextLength || !IsWordChar(text.GetCharAt(offset)))
			{
				return false;
			}

			Int32 first = offset;
			while (first > 0 && IsWordChar(text.GetCharAt(first - 1)))
			{
				first--;
			}

			Int32 last = offset;
			while (last + 1 < text.TextLength && IsWordChar(text.GetCharAt(last + 1)))
			{
				last++;
			}

			start = first;
			length = last - first + 1;
			return true;
		}

		/// <summary>Finds the range of the identifier at the caret (see <see cref="GetWordNearCaret"/>).</summary>
		/// <param name="text">The text.</param>
		/// <param name="caret">The caret offset.</param>
		/// <param name="start">Receives the offset of the first character of the word.</param>
		/// <param name="length">Receives the length of the word.</param>
		/// <returns><see langword="true"/> when a word was found.</returns>
		public static Boolean TryGetWordRangeNearCaret(ITextSource text, Int32 caret, out Int32 start, out Int32 length)
		{
			return TryGetWordRange(text, caret, out start, out length)
				|| TryGetWordRange(text, caret - 1, out start, out length);
		}

		/// <summary>Gets the start of the identifier part that ends at (or contains) an offset.</summary>
		/// <param name="text">The text.</param>
		/// <param name="offset">Offset of the character that ends the part.</param>
		/// <returns>The offset of the first character of the identifier; <paramref name="offset"/> when it is not in an identifier.</returns>
		public static Int32 FindWordStart(ITextSource text, Int32 offset)
		{
			if (offset < 0 || offset >= text.TextLength || !IsWordChar(text.GetCharAt(offset)))
			{
				return offset;
			}

			Int32 first = offset;
			while (first > 0 && IsWordChar(text.GetCharAt(first - 1)))
			{
				first--;
			}

			return first;
		}
	}
}
