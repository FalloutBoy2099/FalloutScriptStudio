namespace FalloutScriptStudio.Core.TextTools
{
	using System;

	/// <summary>
	/// Finds identifiers (letters, digits and underscores) in a text.
	/// </summary>
	public static class WordScanner
	{
		/// <summary>Determines whether a character may be part of an identifier.</summary>
		/// <param name="c">The character.</param>
		/// <returns><c>true</c> for letters, digits and the underscore.</returns>
		public static Boolean IsWordChar(Char c)
		{
			return Char.IsLetterOrDigit(c) || c == '_';
		}

		/// <summary>Finds the start of the identifier that ends at (or contains) an offset.</summary>
		/// <param name="text">The text.</param>
		/// <param name="offset">An offset in the identifier or just after it.</param>
		/// <returns>The offset of the first character of the identifier; <paramref name="offset"/> when no identifier precedes it.</returns>
		public static Int32 FindWordStart(String text, Int32 offset)
		{
			ArgumentNullException.ThrowIfNull(text);

			Int32 position = Math.Clamp(offset, 0, text.Length);
			while (position > 0 && IsWordChar(text[position - 1]))
			{
				position--;
			}

			return position;
		}

		/// <summary>Finds the end of the identifier that starts at (or contains) an offset.</summary>
		/// <param name="text">The text.</param>
		/// <param name="offset">An offset in the identifier or just before it.</param>
		/// <returns>The offset just after the last character of the identifier.</returns>
		public static Int32 FindWordEnd(String text, Int32 offset)
		{
			ArgumentNullException.ThrowIfNull(text);

			Int32 position = Math.Clamp(offset, 0, text.Length);
			while (position < text.Length && IsWordChar(text[position]))
			{
				position++;
			}

			return position;
		}

		/// <summary>Finds the identifier that contains the character at an offset.</summary>
		/// <param name="text">The text.</param>
		/// <param name="offset">Offset of a character.</param>
		/// <returns>The span of the identifier, or <c>null</c> when the character is not part of an identifier.</returns>
		public static TextSpan? FindWordAt(String text, Int32 offset)
		{
			ArgumentNullException.ThrowIfNull(text);

			if (offset < 0 || offset >= text.Length || !IsWordChar(text[offset]))
			{
				return null;
			}

			Int32 start = FindWordStart(text, offset);
			Int32 end = FindWordEnd(text, offset);
			return TextSpan.FromBounds(start, end);
		}

		/// <summary>Gets the identifier that contains the character at an offset.</summary>
		/// <param name="text">The text.</param>
		/// <param name="offset">Offset of a character.</param>
		/// <returns>The identifier, or an empty string.</returns>
		public static String GetWordAt(String text, Int32 offset)
		{
			TextSpan? span = FindWordAt(text, offset);
			return span is null ? String.Empty : text.Substring(span.Value.Offset, span.Value.Length);
		}
	}
}