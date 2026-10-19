namespace FalloutScriptStudio.Core.Formatting
{
	using System;

	/// <summary>A piece of the text: a kind and a range.</summary>
	/// <param name="Kind">The kind.</param>
	/// <param name="Start">The offset of the first character.</param>
	/// <param name="Length">The number of characters.</param>
	internal readonly record struct FormatToken(FormatTokenKind Kind, Int32 Start, Int32 Length)
	{
		/// <summary>Gets the offset just after the token.</summary>
		public Int32 End => this.Start + this.Length;

		/// <summary>Gets a value indicating whether the token is a comment.</summary>
		public Boolean IsComment => this.Kind is FormatTokenKind.LineComment or FormatTokenKind.BlockComment;

		/// <summary>Gets a value indicating whether the token is white space or a comment (it does not take part in the structure of the code).</summary>
		public Boolean IsTrivia => this.Kind is FormatTokenKind.Space or FormatTokenKind.Newline or FormatTokenKind.LineComment or FormatTokenKind.BlockComment;

		/// <summary>Gets the text of the token.</summary>
		/// <param name="text">The text the token belongs to.</param>
		/// <returns>The characters.</returns>
		public String GetText(String text)
		{
			return text.Substring(this.Start, this.Length);
		}

		/// <summary>Determines whether the token is a given word.</summary>
		/// <param name="text">The text the token belongs to.</param>
		/// <param name="word">The word (compared ignoring the case).</param>
		/// <returns><see langword="true"/> for that word.</returns>
		public Boolean IsWord(String text, String word)
		{
			return this.Kind == FormatTokenKind.Word
				&& this.Length == word.Length
				&& String.Compare(text, this.Start, word, 0, word.Length, StringComparison.OrdinalIgnoreCase) == 0;
		}

		/// <summary>Determines whether the token is a given symbol.</summary>
		/// <param name="text">The text the token belongs to.</param>
		/// <param name="symbol">The symbol.</param>
		/// <returns><see langword="true"/> for that symbol.</returns>
		public Boolean IsSymbol(String text, String symbol)
		{
			return this.Kind == FormatTokenKind.Symbol
				&& this.Length == symbol.Length
				&& String.CompareOrdinal(text, this.Start, symbol, 0, symbol.Length) == 0;
		}
	}
}