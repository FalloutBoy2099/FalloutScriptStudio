namespace FalloutScriptStudio.Editor.Text
{
	using System;
	using System.Buffers;
	using ICSharpCode.AvalonEdit.Document;

	/// <summary>
	/// Finds the <c>{placeholder}</c> of a code template at a position (legacy <c>SelectedTextColorRegion</c>): the
	/// text is selected as a whole so that typing replaces it.
	/// </summary>
	public static class PlaceholderFinder
	{
		private static readonly SearchValues<Char> _colonOrComma = SearchValues.Create(":,");

		/// <summary>Finds the placeholder that contains an offset.</summary>
		/// <param name="map">The lexical map of the text.</param>
		/// <param name="text">The text.</param>
		/// <param name="offset">An offset inside the braces or at one of them.</param>
		/// <param name="start">Receives the offset of the opening brace.</param>
		/// <param name="length">Receives the length including both braces.</param>
		/// <returns>
		/// <see langword="true"/> when the offset is in a closed one-line brace block that is not an array or map literal
		/// (it contains no <c>:</c> or <c>,</c>) and is not empty.
		/// </returns>
		public static Boolean TryFind(LexicalMap map, ITextSource text, Int32 offset, out Int32 start, out Int32 length)
		{
			start = 0;
			length = 0;
			if (!map.TryGetSpanAt(offset, out LexicalSpan span) || span.Kind != LexicalKind.Braces || !span.IsClosed || span.Length < 3)
			{
				return false;
			}

			String content = text.GetText(span.Start + 1, span.Length - 2);
			if (content.AsSpan().IndexOfAny(_colonOrComma) >= 0)
			{
				return false;
			}

			start = span.Start;
			length = span.Length;
			return true;
		}
	}
}