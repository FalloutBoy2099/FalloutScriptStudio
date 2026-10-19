namespace FalloutScriptStudio.Core.TextTools
{
	using System;

	/// <summary>A run of characters of a name that the typed text matched (see <see cref="FuzzyMatcher.FindMatchRanges"/>).</summary>
	/// <param name="Start">The index of the first matched character.</param>
	/// <param name="Length">The number of matched characters.</param>
	public readonly record struct MatchRange(Int32 Start, Int32 Length);
}