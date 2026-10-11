namespace FalloutScriptStudio.Core.TextTools
{
	using System;
	using System.Text;

	/// <summary>
	/// Changes the case of the selected text (upper, lower, capitalize).
	/// </summary>
	public static class CaseTools
	{
		/// <summary>Converts the selection to upper case.</summary>
		/// <param name="text">The document text.</param>
		/// <param name="selection">The selection; nothing happens for an empty span.</param>
		/// <returns>The edit, or <c>null</c> when the text does not change.</returns>
		public static TextEdit? ToUpper(String text, TextSpan selection)
		{
			return Convert(text, selection, s => s.ToUpperInvariant());
		}

		/// <summary>Converts the selection to lower case.</summary>
		/// <param name="text">The document text.</param>
		/// <param name="selection">The selection; nothing happens for an empty span.</param>
		/// <returns>The edit, or <c>null</c> when the text does not change.</returns>
		public static TextEdit? ToLower(String text, TextSpan selection)
		{
			return Convert(text, selection, s => s.ToLowerInvariant());
		}

		/// <summary>
		/// Upper-cases the character that follows every non-letter (so every word start) of the selection, including the
		/// first character of the selection; other characters are left as they are (legacy <c>CapitalizeAction</c>).
		/// </summary>
		/// <param name="text">The document text.</param>
		/// <param name="selection">The selection; nothing happens for an empty span.</param>
		/// <returns>The edit, or <c>null</c> when the text does not change.</returns>
		/// <remarks>The legacy code did not capitalize the first character when the selection started at the beginning of the document.</remarks>
		public static TextEdit? Capitalize(String text, TextSpan selection)
		{
			ArgumentNullException.ThrowIfNull(text);

			if (selection.IsEmpty)
			{
				return null;
			}

			Int32 offset = selection.Offset;
			Int32 length = selection.Length;
			Boolean previousIsLetter = offset > 0 && Char.IsLetter(text[offset - 1]);
			var result = new StringBuilder(text.Substring(offset, length));
			for (Int32 i = 0; i < result.Length; i++)
			{
				Boolean afterNonLetter = i == 0 ? !previousIsLetter : !Char.IsLetter(result[i - 1]);
				if (afterNonLetter)
				{
					result[i] = Char.ToUpperInvariant(result[i]);
				}
			}

			String original = text.Substring(offset, length);
			return result.ToString() == original ? null : new TextEdit(offset, length, result.ToString());
		}

		private static TextEdit? Convert(String text, TextSpan selection, Func<String, String> convert)
		{
			ArgumentNullException.ThrowIfNull(text);

			if (selection.IsEmpty)
			{
				return null;
			}

			String original = text.Substring(selection.Offset, selection.Length);
			String converted = convert(original);
			return String.Equals(original, converted, StringComparison.Ordinal) ? null : new TextEdit(selection.Offset, selection.Length, converted);
		}
	}
}
