namespace FalloutScriptStudio.Services.ScriptTools.Search
{
	using System;

	/// <summary>
	/// What to look for.
	/// </summary>
	/// <param name="Text">The text, or the regular expression when <paramref name="UseRegex"/> is set.</param>
	/// <param name="MatchCase">Whether the letter case matters.</param>
	/// <param name="WholeWord">Whether the text must not be part of a longer word; ignored for regular expressions (like in the legacy editor).</param>
	/// <param name="UseRegex">Whether <paramref name="Text"/> is a regular expression.</param>
	public sealed record SearchOptions(String Text, Boolean MatchCase, Boolean WholeWord, Boolean UseRegex);
}
