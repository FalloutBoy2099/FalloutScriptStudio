namespace FalloutScriptStudio.Editor.Text
{
	/// <summary>The lexical category of a piece of SSL source text that matters for editor behaviour.</summary>
	public enum LexicalKind
	{
		/// <summary>Ordinary code.</summary>
		Code,

		/// <summary>A <c>//</c> comment up to the end of the line.</summary>
		LineComment,

		/// <summary>A <c>/* ... */</c> comment (may span lines).</summary>
		BlockComment,

		/// <summary>A <c>"..."</c> string literal (single line).</summary>
		String,

		/// <summary>A <c>{...}</c> block on one line: a code template placeholder or an array / map literal.</summary>
		Braces,
	}
}
