namespace FalloutScriptStudio.Core.TextTools
{
	/// <summary>
	/// What kind of text a position of a script is in.
	/// </summary>
	public enum LexicalContext
	{
		/// <summary>Ordinary code.</summary>
		Code,

		/// <summary>Inside a <c>// ...</c> comment.</summary>
		LineComment,

		/// <summary>Inside a <c>/* ... */</c> comment.</summary>
		BlockComment,

		/// <summary>Inside a <c>"..."</c> string.</summary>
		StringLiteral,
	}
}