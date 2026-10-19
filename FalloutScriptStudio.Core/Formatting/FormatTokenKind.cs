namespace FalloutScriptStudio.Core.Formatting
{
	/// <summary>The kinds of token the formatter sees. Together they cover every character of the text.</summary>
	internal enum FormatTokenKind
	{
		/// <summary>Spaces and tabs.</summary>
		Space,

		/// <summary>One line break (<c>\r\n</c>, <c>\n</c> or a lone <c>\r</c>).</summary>
		Newline,

		/// <summary>An identifier or a keyword.</summary>
		Word,

		/// <summary>A number (decimal, hexadecimal, with a fraction).</summary>
		Number,

		/// <summary>A string literal (it ends at the closing quote or at the end of the line).</summary>
		String,

		/// <summary>A <c>//</c> comment up to the end of the line.</summary>
		LineComment,

		/// <summary>A <c>/* */</c> comment, possibly over several lines.</summary>
		BlockComment,

		/// <summary>A whole preprocessor line: from the <c>#</c> to the end of the logical line (backslash continuations and trailing comments included).</summary>
		Directive,

		/// <summary>An operator or a punctuation mark.</summary>
		Symbol,
	}
}