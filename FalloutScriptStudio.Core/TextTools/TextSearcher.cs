namespace FalloutScriptStudio.Core.TextTools
{
	using System;
	using System.Collections.Generic;
	using System.Text.RegularExpressions;

	/// <summary>
	/// Default <see cref="ITextSearcher"/>.
	/// </summary>
	/// <remarks>
	/// Differences from the legacy code: whole-word queries escape the search text and use look-arounds (so they also work for
	/// words that start or end with a symbol), "find all" and "replace all" work on non-overlapping matches (the legacy replace
	/// loop could run forever when the replacement contained the search text), and empty regular expression matches are ignored.
	/// </remarks>
	public sealed class TextSearcher : ITextSearcher
	{
		/// <inheritdoc />
		public TextSpan? FindNext(String text, SearchQuery query, Int32 startOffset, Boolean wrap)
		{
			ArgumentNullException.ThrowIfNull(text);
			ArgumentNullException.ThrowIfNull(query);

			Regex? regex = query.CreateRegex();
			Int32 start = Math.Clamp(startOffset, 0, text.Length);
			Found? found = Next(text, query, regex, start);
			if (found is null && wrap && start > 0)
			{
				found = Next(text, query, regex, 0);
			}

			return found?.Span;
		}

		/// <inheritdoc />
		public TextSpan? FindPrevious(String text, SearchQuery query, Int32 beforeOffset, Boolean wrap)
		{
			ArgumentNullException.ThrowIfNull(text);
			ArgumentNullException.ThrowIfNull(query);

			Regex? regex = query.CreateRegex();
			Int32 limit = Math.Clamp(beforeOffset, 0, text.Length);
			TextSpan? found = Last(text, query, regex, limit);
			if (found is null && wrap && limit < text.Length)
			{
				found = Last(text, query, regex, text.Length);
			}

			return found;
		}

		/// <inheritdoc />
		public IReadOnlyList<TextSpan> FindAll(String text, SearchQuery query)
		{
			ArgumentNullException.ThrowIfNull(text);
			ArgumentNullException.ThrowIfNull(query);

			var result = new List<TextSpan>();
			foreach (Found found in Enumerate(text, query, query.CreateRegex()))
			{
				result.Add(found.Span);
			}

			return result;
		}

		/// <inheritdoc />
		public Boolean IsMatch(String text, SearchQuery query)
		{
			ArgumentNullException.ThrowIfNull(text);
			ArgumentNullException.ThrowIfNull(query);

			return Next(text, query, query.CreateRegex(), 0) is not null;
		}

		/// <inheritdoc />
		public IReadOnlyList<SearchResultRow> FindAllLines(String text, String file, SearchQuery query)
		{
			ArgumentNullException.ThrowIfNull(text);
			ArgumentNullException.ThrowIfNull(file);
			ArgumentNullException.ThrowIfNull(query);

			Regex? regex = query.CreateRegex();
			var map = new TextLineMap(text);
			var rows = new List<SearchResultRow>();
			for (Int32 line = 1; line <= map.LineCount; line++)
			{
				String lineText = map.GetLineText(line);
				Found? found = Next(lineText, query, regex, 0);
				if (found is not null)
				{
					rows.Add(new SearchResultRow(file, line, found.Span.Offset + 1, found.Span.Length, lineText.Trim()));
				}
			}

			return rows;
		}

		/// <inheritdoc />
		public ReplaceResult ReplaceAll(String text, SearchQuery query, String replacement)
		{
			ArgumentNullException.ThrowIfNull(text);
			ArgumentNullException.ThrowIfNull(query);
			ArgumentNullException.ThrowIfNull(replacement);

			var edits = new List<TextEdit>();
			foreach (Found found in Enumerate(text, query, query.CreateRegex()))
			{
				edits.Add(new TextEdit(found.Span.Offset, found.Span.Length, Expand(found, replacement)));
			}

			return new ReplaceResult(TextEditApplier.Apply(text, edits), edits);
		}

		/// <inheritdoc />
		public TextEdit? ReplaceNext(String text, SearchQuery query, String replacement, Int32 startOffset, Boolean wrap)
		{
			ArgumentNullException.ThrowIfNull(text);
			ArgumentNullException.ThrowIfNull(query);
			ArgumentNullException.ThrowIfNull(replacement);

			Regex? regex = query.CreateRegex();
			Int32 start = Math.Clamp(startOffset, 0, text.Length);
			Found? found = Next(text, query, regex, start);
			if (found is null && wrap && start > 0)
			{
				found = Next(text, query, regex, 0);
			}

			return found is null ? null : new TextEdit(found.Span.Offset, found.Span.Length, Expand(found, replacement));
		}

		private static String Expand(Found found, String replacement)
		{
			return found.Match is null ? replacement : found.Match.Result(replacement);
		}

		private static Found? Next(String text, SearchQuery query, Regex? regex, Int32 start)
		{
			if (regex is null)
			{
				if (query.Pattern.Length == 0)
				{
					return null;
				}

				Int32 index = text.IndexOf(query.Pattern, start, query.GetComparison());
				return index < 0 ? null : new Found(new TextSpan(index, query.Pattern.Length), null);
			}

			Match match = regex.Match(text, start);
			while (match.Success && match.Length == 0)
			{
				match = match.NextMatch();
			}

			return match.Success ? new Found(new TextSpan(match.Index, match.Length), match) : null;
		}

		private static TextSpan? Last(String text, SearchQuery query, Regex? regex, Int32 limit)
		{
			if (regex is null)
			{
				if (query.Pattern.Length == 0 || limit < query.Pattern.Length)
				{
					return null;
				}

				Int32 index = text.LastIndexOf(query.Pattern, limit - 1, query.GetComparison());
				return index < 0 ? null : new TextSpan(index, query.Pattern.Length);
			}

			TextSpan? last = null;
			Match match = regex.Match(text, 0, limit);
			while (match.Success)
			{
				if (match.Length > 0)
				{
					last = new TextSpan(match.Index, match.Length);
				}

				match = match.NextMatch();
			}

			return last;
		}

		private static IEnumerable<Found> Enumerate(String text, SearchQuery query, Regex? regex)
		{
			Int32 position = 0;
			while (position <= text.Length)
			{
				Found? found = Next(text, query, regex, position);
				if (found is null)
				{
					yield break;
				}

				yield return found;
				position = found.Span.End;
			}
		}

		private sealed record Found(TextSpan Span, Match? Match);
	}
}
