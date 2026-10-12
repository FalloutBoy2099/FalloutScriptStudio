namespace FalloutScriptStudio.Core.TextTools
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// Searches and replaces in plain text; every result is an offset/length pair (legacy <c>Utilities.Search…</c> methods).
	/// </summary>
	/// <remarks>
	/// Every method that runs a regular expression query throws a <see cref="System.Text.RegularExpressions.RegexMatchTimeoutException"/>
	/// when the pattern takes longer than <see cref="SearchQuery.MatchTimeout"/> (catastrophic backtracking).
	/// </remarks>
	public interface ITextSearcher
	{
		/// <summary>Finds the first match at or after an offset.</summary>
		/// <param name="text">The text to search.</param>
		/// <param name="query">What to find.</param>
		/// <param name="startOffset">Where to start (a caret offset or the end of the previous match).</param>
		/// <param name="wrap">Whether to continue from the beginning of the text when nothing is found after the offset.</param>
		/// <returns>The match, or <c>null</c>.</returns>
		/// <exception cref="ArgumentException">The query is a regular expression that is not valid.</exception>
		TextSpan? FindNext(String text, SearchQuery query, Int32 startOffset, Boolean wrap);

		/// <summary>Finds the last match that ends at or before an offset.</summary>
		/// <param name="text">The text to search.</param>
		/// <param name="query">What to find.</param>
		/// <param name="beforeOffset">The search goes backwards from here (a caret offset).</param>
		/// <param name="wrap">Whether to continue from the end of the text when nothing is found before the offset.</param>
		/// <returns>The match, or <c>null</c>.</returns>
		TextSpan? FindPrevious(String text, SearchQuery query, Int32 beforeOffset, Boolean wrap);

		/// <summary>Finds all non-overlapping, non-empty matches.</summary>
		/// <param name="text">The text to search.</param>
		/// <param name="query">What to find.</param>
		/// <returns>The matches in order.</returns>
		IReadOnlyList<TextSpan> FindAll(String text, SearchQuery query);

		/// <summary>Finds the first matches of a text: the search stops as soon as enough matches are found.</summary>
		/// <param name="text">The text to search.</param>
		/// <param name="query">What to find.</param>
		/// <param name="maxCount">The most matches to return.</param>
		/// <returns>The first matches in order, at most <paramref name="maxCount"/>.</returns>
		IReadOnlyList<TextSpan> FindAll(String text, SearchQuery query, Int32 maxCount);

		/// <summary>Determines whether the text contains a match.</summary>
		/// <param name="text">The text to search.</param>
		/// <param name="query">What to find.</param>
		/// <returns><c>true</c> when there is at least one match.</returns>
		Boolean IsMatch(String text, SearchQuery query);

		/// <summary>
		/// Builds the "find all" result rows of one text: one row per line that contains a match.
		/// </summary>
		/// <param name="text">The text to search.</param>
		/// <param name="file">The file name stored in the rows.</param>
		/// <param name="query">What to find.</param>
		/// <returns>The rows in line order.</returns>
		IReadOnlyList<SearchResultRow> FindAllLines(String text, String file, SearchQuery query);

		/// <summary>Replaces every match.</summary>
		/// <param name="text">The text to search.</param>
		/// <param name="query">What to find.</param>
		/// <param name="replacement">The replacement; for a regular expression it may use group references such as <c>$1</c>.</param>
		/// <returns>The new text and the edits.</returns>
		ReplaceResult ReplaceAll(String text, SearchQuery query, String replacement);

		/// <summary>Replaces the first match at or after an offset.</summary>
		/// <param name="text">The text to search.</param>
		/// <param name="query">What to find.</param>
		/// <param name="replacement">The replacement.</param>
		/// <param name="startOffset">Where to start.</param>
		/// <param name="wrap">Whether to continue from the beginning when nothing is found.</param>
		/// <returns>The edit, or <c>null</c> when nothing was found.</returns>
		TextEdit? ReplaceNext(String text, SearchQuery query, String replacement, Int32 startOffset, Boolean wrap);
	}
}
