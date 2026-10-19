namespace FalloutScriptStudio.Services.ScriptTools.Search
{
	using System;
	using System.Collections.Generic;
	using System.Text.RegularExpressions;
	using FalloutScriptStudio.Core.TextTools;
	using FalloutScriptStudio.Localization;

	/// <summary>
	/// Finds text, whole words or regular expressions. Matching is done line by line (a match never spans a line break),
	/// which is what the legacy editor did for folder searches and what the result lists show.
	/// </summary>
	public sealed class SearchMatcher
	{
		private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(3);

		private readonly Regex? _regex;
		private readonly StringComparison _comparison;

		private SearchMatcher(SearchOptions options, Regex? regex)
		{
			this.Options = options;
			this._regex = regex;
			this._comparison = options.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
		}

		/// <summary>Gets the options this matcher was created with.</summary>
		public SearchOptions Options { get; }

		/// <summary>
		/// Creates a matcher.
		/// </summary>
		/// <param name="options">What to look for.</param>
		/// <param name="matcher">The matcher; <see langword="null"/> when the options are not valid.</param>
		/// <param name="error">Why the options are not valid; <see langword="null"/> on success.</param>
		/// <param name="regexTimeout">The time limit of one regular expression operation; <see langword="null"/> for the default of 3 seconds.</param>
		/// <returns><see langword="true"/> when a matcher was created.</returns>
		/// <remarks>
		/// A regular expression that exceeds the time limit (catastrophic backtracking) makes the matching methods throw a
		/// <see cref="RegexMatchTimeoutException"/>; callers on the UI or a worker thread catch it and report the pattern as too slow.
		/// </remarks>
		public static Boolean TryCreate(SearchOptions options, out SearchMatcher? matcher, out String? error, TimeSpan? regexTimeout = null)
		{
			ArgumentNullException.ThrowIfNull(options);

			matcher = null;
			if (String.IsNullOrEmpty(options.Text))
			{
				error = Loc.Get("Str.ScriptTools.SearchErrEnterText");
				return false;
			}

			if (!options.UseRegex)
			{
				matcher = new SearchMatcher(options, null);
				error = null;
				return true;
			}

			RegexOptions regexOptions = RegexOptions.CultureInvariant;
			if (!options.MatchCase)
			{
				regexOptions |= RegexOptions.IgnoreCase;
			}

			try
			{
				matcher = new SearchMatcher(options, new Regex(options.Text, regexOptions, regexTimeout ?? RegexTimeout));
				error = null;
				return true;
			}
			catch (ArgumentException ex)
			{
				error = Loc.Format("Str.ScriptTools.SearchErrRegexInvalid", ex.Message);
				return false;
			}
		}

		/// <summary>Determines whether the line contains a match.</summary>
		/// <param name="line">The line, without its line break.</param>
		/// <returns><see langword="true"/> when there is a match.</returns>
		public Boolean IsMatch(String line)
		{
			return this.FindFirst(line, 0) is not null;
		}

		/// <summary>Finds the first match of a line that starts at or after a position.</summary>
		/// <param name="line">The line, without its line break.</param>
		/// <param name="startAt">The position to start from.</param>
		/// <returns>The match, or <see langword="null"/>.</returns>
		public LineMatch? FindFirst(String line, Int32 startAt)
		{
			ArgumentNullException.ThrowIfNull(line);

			if (startAt > line.Length)
			{
				return null;
			}

			if (this._regex is not null)
			{
				for (Match match = this._regex.Match(line, startAt); match.Success; match = match.NextMatch())
				{
					if (match.Length > 0)
					{
						return new LineMatch(match.Index, match.Length, match);
					}
				}

				return null;
			}

			String text = this.Options.Text;
			Int32 position = startAt;
			while (position <= line.Length - text.Length)
			{
				Int32 index = line.IndexOf(text, position, this._comparison);
				if (index < 0)
				{
					return null;
				}

				if (!this.Options.WholeWord || IsWholeWord(line, index, text))
				{
					return new LineMatch(index, text.Length, null);
				}

				position = index + 1;
			}

			return null;
		}

		/// <summary>Finds all non-overlapping matches of a line.</summary>
		/// <param name="line">The line, without its line break.</param>
		/// <returns>The matches in order.</returns>
		public IReadOnlyList<LineMatch> FindAll(String line)
		{
			List<LineMatch> matches = [];
			Int32 position = 0;
			while (position <= line.Length)
			{
				LineMatch? match = this.FindFirst(line, position);
				if (match is null)
				{
					break;
				}

				matches.Add(match);
				position = match.Index + match.Length;
			}

			return matches;
		}

		/// <summary>Finds the first match of a text at or after an offset.</summary>
		/// <param name="text">The text.</param>
		/// <param name="start">The offset to start from.</param>
		/// <param name="wrap">Continue from the beginning of the text when nothing is found up to its end.</param>
		/// <returns>The match, or <see langword="null"/>.</returns>
		public TextMatch? FindNext(String text, Int32 start, Boolean wrap)
		{
			ArgumentNullException.ThrowIfNull(text);

			TextLineMap map = new(text);
			Int32 startLine = map.GetLineFromOffset(Math.Clamp(start, 0, text.Length));
			for (Int32 line = startLine; line <= map.LineCount; line++)
			{
				Int32 from = line == startLine ? Math.Clamp(start, 0, text.Length) - map.GetLineStart(line) : 0;
				TextMatch? found = this.FindInLine(map, line, from);
				if (found is not null)
				{
					return found;
				}
			}

			if (!wrap)
			{
				return null;
			}

			for (Int32 line = 1; line <= startLine; line++)
			{
				TextMatch? found = this.FindInLine(map, line, 0);
				if (found is not null)
				{
					return found;
				}
			}

			return null;
		}

		/// <summary>
		/// Computes the edits that replace every match of a text.
		/// </summary>
		/// <param name="text">The text.</param>
		/// <param name="replacement">The replacement; for regular expressions it may refer to groups (<c>$1</c>).</param>
		/// <returns>The edits in ascending order of offset (they never overlap).</returns>
		public IReadOnlyList<TextEdit> ComputeReplacements(String text, String replacement)
		{
			ArgumentNullException.ThrowIfNull(text);
			ArgumentNullException.ThrowIfNull(replacement);

			List<TextEdit> edits = [];
			TextLineMap map = new(text);
			for (Int32 line = 1; line <= map.LineCount; line++)
			{
				String lineText = map.GetLineText(line);
				if (lineText.Length == 0)
				{
					continue;
				}

				Int32 lineStart = map.GetLineStart(line);
				foreach (LineMatch match in this.FindAll(lineText))
				{
					edits.Add(new TextEdit(lineStart + match.Index, match.Length, this.Expand(match, replacement)));
				}
			}

			return edits;
		}

		/// <summary>Gets the text that replaces a match.</summary>
		/// <param name="match">The match.</param>
		/// <param name="replacement">The replacement text.</param>
		/// <returns>The replacement; group references are expanded for regular expressions.</returns>
		public String Expand(LineMatch match, String replacement)
		{
			ArgumentNullException.ThrowIfNull(match);
			ArgumentNullException.ThrowIfNull(replacement);

			return this.Options.UseRegex && match.RegexMatch is not null ? match.RegexMatch.Result(replacement) : replacement;
		}

		private static Boolean IsWordChar(Char c)
		{
			return Char.IsLetterOrDigit(c) || c == '_';
		}

		private static Boolean IsWholeWord(String line, Int32 index, String text)
		{
			if (IsWordChar(text[0]) && index > 0 && IsWordChar(line[index - 1]))
			{
				return false;
			}

			Int32 end = index + text.Length;
			return !(IsWordChar(text[text.Length - 1]) && end < line.Length && IsWordChar(line[end]));
		}

		private TextMatch? FindInLine(TextLineMap map, Int32 line, Int32 from)
		{
			Int32 length = map.GetLineLength(line);
			if (length == 0 || from > length)
			{
				return null;
			}

			LineMatch? match = this.FindFirst(map.GetLineText(line), from);
			return match is null ? null : new TextMatch(map.GetLineStart(line) + match.Index, match.Length, line, match.Index + 1);
		}
	}
}