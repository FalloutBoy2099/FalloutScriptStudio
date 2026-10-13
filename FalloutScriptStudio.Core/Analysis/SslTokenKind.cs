namespace FalloutScriptStudio.Core.Analysis
{
	/// <summary>The kind of a token of an SSL script.</summary>
	public enum SslTokenKind
	{
		/// <summary>A word: a keyword, a name or a macro.</summary>
		Identifier = 0,

		/// <summary>A number (decimal, hexadecimal or floating point).</summary>
		Number = 1,

		/// <summary>A string literal, including its quotes.</summary>
		String = 2,

		/// <summary>An operator or a separator.</summary>
		Punctuation = 3,

		/// <summary>The <c>#</c> and the name of a preprocessor directive (<c>#define</c>, <c>#include</c>...).</summary>
		Directive = 4,
	}
}
