namespace FalloutScriptStudio.Services.ScriptTools.Search
{
	using System;
	using System.Text.RegularExpressions;

	/// <summary>
	/// A match inside one line of text.
	/// </summary>
	/// <param name="Index">The 0-based position inside the line.</param>
	/// <param name="Length">The length of the match.</param>
	/// <param name="RegexMatch">The underlying regular expression match (used to expand <c>$1</c> in replacements), or <see langword="null"/> for plain text searches.</param>
	public sealed record LineMatch(Int32 Index, Int32 Length, Match? RegexMatch);
}
