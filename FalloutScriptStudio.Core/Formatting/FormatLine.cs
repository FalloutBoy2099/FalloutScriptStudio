namespace FalloutScriptStudio.Core.Formatting
{
	using System;

	/// <summary>One physical line of the text, with the tokens that start on it.</summary>
	internal sealed class FormatLine
	{
		/// <summary>Initializes a new instance of the <see cref="FormatLine"/> class.</summary>
		/// <param name="index">The zero-based number of the line.</param>
		/// <param name="start">The offset of its first character.</param>
		/// <param name="end">The offset after its last character (before the line break).</param>
		/// <param name="eolLength">The length of its line break (0 for the last line).</param>
		public FormatLine(Int32 index, Int32 start, Int32 end, Int32 eolLength)
		{
			this.Index = index;
			this.Start = start;
			this.End = end;
			this.EolLength = eolLength;
			this.FirstToken = -1;
			this.EndToken = -1;
		}

		/// <summary>Gets the zero-based number of the line.</summary>
		public Int32 Index { get; }

		/// <summary>Gets the offset of the first character.</summary>
		public Int32 Start { get; }

		/// <summary>Gets the offset after the last character, before the line break.</summary>
		public Int32 End { get; }

		/// <summary>Gets the length of the line break (0 for the last line).</summary>
		public Int32 EolLength { get; }

		/// <summary>Gets the offset of the first character of the next line.</summary>
		public Int32 Next => this.End + this.EolLength;

		/// <summary>Gets or sets the index of the first token that starts on the line.</summary>
		public Int32 FirstToken { get; set; }

		/// <summary>Gets or sets the index after the last token that starts on the line (the line break is not counted).</summary>
		public Int32 EndToken { get; set; }

		/// <summary>Gets or sets a value indicating whether the line begins inside a comment or a preprocessor line that started above (its text is never touched).</summary>
		public Boolean Verbatim { get; set; }

		/// <summary>Gets or sets a value indicating whether a comment or a preprocessor line that goes on in the next lines starts on this line.</summary>
		public Boolean StartsMultiLineToken { get; set; }

		/// <summary>Gets the number of characters of the line.</summary>
		public Int32 Length => this.End - this.Start;
	}
}