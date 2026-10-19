namespace FalloutScriptStudio.Core.TextTools
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Text;

	/// <summary>
	/// Ranks how well a short typed text matches a name, ignoring the letter case and the accents: an exact name comes first, then a name that
	/// starts with the text, then a name with a word that starts with it, then a name that contains it, then a name that has its letters in
	/// order (<c>fmtdoc</c> finds <c>Format Document</c>). Several words must all match. Pure and stateless.
	/// </summary>
	public static class FuzzyMatcher
	{
		/// <summary>The score of a name equal to the text.</summary>
		public const Int32 ExactScore = 1000;

		/// <summary>The lowest score of a name that starts with the text.</summary>
		public const Int32 PrefixScore = 800;

		/// <summary>The lowest score of a name that has a word starting with the text.</summary>
		public const Int32 WordStartScore = 600;

		/// <summary>The lowest score of a name that contains the text.</summary>
		public const Int32 SubstringScore = 400;

		/// <summary>The highest score of a name that only has the letters of the text in order.</summary>
		public const Int32 SubsequenceScore = 300;

		/// <summary>
		/// Gets the kind of match a score stands for, so that callers can order by kind first and by something else (how recently a command was
		/// used) before the score itself.
		/// </summary>
		/// <param name="score">A score returned by <see cref="Score"/>.</param>
		/// <returns>4 for an exact name, 3 for a name that starts with the text, 2 for a word that starts with it, 1 for a name that contains it, 0 for letters in order.</returns>
		public static Int32 GetTier(Int32 score)
		{
			return score >= ExactScore ? 4 : score > PrefixScore ? 3 : score > WordStartScore ? 2 : score > SubstringScore ? 1 : 0;
		}

		/// <summary>
		/// Scores a text against a name.
		/// </summary>
		/// <param name="pattern">What the user typed; white space separates the words that must all match.</param>
		/// <param name="name">The name to look in.</param>
		/// <returns>The score (higher is better; an empty pattern gives 0), or <see langword="null"/> when the name does not match.</returns>
		public static Int32? Score(String pattern, String name)
		{
			ArgumentNullException.ThrowIfNull(pattern);
			ArgumentNullException.ThrowIfNull(name);

			String[] tokens = Fold(pattern).Split((Char[]?)null, StringSplitOptions.RemoveEmptyEntries);
			if (tokens.Length == 0)
			{
				return 0;
			}

			String folded = Fold(name);
			Int32 total = 0;
			Int32 previousEnd = -1;
			Boolean inOrder = true;
			foreach (String token in tokens)
			{
				Int32? score = ScoreToken(token, folded, out Int32 position);
				if (score is null)
				{
					return null;
				}

				total += score.Value;
				inOrder &= position >= previousEnd;
				previousEnd = position + token.Length;
			}

			Int32 average = total / tokens.Length;
			return tokens.Length > 1 && inOrder ? average + 10 : average;
		}

		/// <summary>
		/// Folds a text for comparison: lower case and without accents. Every character gives exactly one character, so a position in the
		/// result is a position in the text.
		/// </summary>
		/// <param name="text">The text.</param>
		/// <returns>The folded text.</returns>
		public static String Fold(String text)
		{
			ArgumentNullException.ThrowIfNull(text);

			StringBuilder builder = new StringBuilder(text.Length);
			foreach (Char c in text)
			{
				builder.Append(FoldChar(c));
			}

			return builder.ToString();
		}

		/// <summary>
		/// Finds the characters of a name that a typed text matched, so that they can be emphasized. Each word of the text is looked up the way
		/// <see cref="Score"/> does (at the start of the name, at the start of a word, inside the name, or as letters in order); a word that is
		/// not found in the name is ignored.
		/// </summary>
		/// <param name="pattern">What the user typed.</param>
		/// <param name="name">The name the matched characters are looked for in.</param>
		/// <returns>The runs of matched characters, in order and without overlap; empty when nothing matched.</returns>
		public static IReadOnlyList<MatchRange> FindMatchRanges(String pattern, String name)
		{
			ArgumentNullException.ThrowIfNull(pattern);
			ArgumentNullException.ThrowIfNull(name);

			String[] tokens = Fold(pattern).Split((Char[]?)null, StringSplitOptions.RemoveEmptyEntries);
			if (tokens.Length == 0 || name.Length == 0)
			{
				return Array.Empty<MatchRange>();
			}

			String folded = Fold(name);
			Boolean[] matched = new Boolean[folded.Length];
			foreach (String token in tokens)
			{
				MarkToken(token, folded, matched);
			}

			List<MatchRange> ranges = [];
			Int32 index = 0;
			while (index < matched.Length)
			{
				if (!matched[index])
				{
					index++;
					continue;
				}

				Int32 start = index;
				while (index < matched.Length && matched[index])
				{
					index++;
				}

				ranges.Add(new MatchRange(start, index - start));
			}

			return ranges;
		}

		private static void MarkToken(String token, String name, Boolean[] matched)
		{
			Int32 start = FindContiguous(token, name);
			if (start >= 0)
			{
				for (Int32 i = 0; i < token.Length; i++)
				{
					matched[start + i] = true;
				}

				return;
			}

			List<Int32> positions = new(token.Length);
			for (Int32 i = 0; i < name.Length && positions.Count < token.Length; i++)
			{
				if (name[i] == token[positions.Count])
				{
					positions.Add(i);
				}
			}

			if (positions.Count == token.Length)
			{
				foreach (Int32 position in positions)
				{
					matched[position] = true;
				}
			}
		}

		private static Int32 FindContiguous(String token, String name)
		{
			if (name.StartsWith(token, StringComparison.Ordinal))
			{
				return 0;
			}

			for (Int32 i = 1; i < name.Length; i++)
			{
				if (Char.IsLetterOrDigit(name[i]) && !Char.IsLetterOrDigit(name[i - 1]) && String.CompareOrdinal(name, i, token, 0, token.Length) == 0)
				{
					return i;
				}
			}

			return name.IndexOf(token, StringComparison.Ordinal);
		}

		private static Char FoldChar(Char c)
		{
			if (c < 0x80)
			{
				return Char.ToLowerInvariant(c);
			}

			switch (c)
			{
				case 'ł':
				case 'Ł':
					return 'l';
				case 'ø':
				case 'Ø':
					return 'o';
				case 'đ':
				case 'Đ':
					return 'd';
			}

			String decomposed = c.ToString().Normalize(NormalizationForm.FormD);
			Char baseChar = decomposed.Length > 0 && CharUnicodeInfo.GetUnicodeCategory(decomposed[0]) != UnicodeCategory.NonSpacingMark ? decomposed[0] : c;
			return Char.ToLowerInvariant(baseChar);
		}

		private static Int32? ScoreToken(String token, String name, out Int32 position)
		{
			position = -1;
			if (name.Length == 0)
			{
				return null;
			}

			if (name.Length == token.Length && String.Equals(name, token, StringComparison.Ordinal))
			{
				position = 0;
				return ExactScore;
			}

			Int32 lengthPenalty = Math.Min(name.Length - token.Length, 99);
			if (name.StartsWith(token, StringComparison.Ordinal))
			{
				position = 0;
				return (PrefixScore + 100) - lengthPenalty;
			}

			Int32 wordIndex = 0;
			for (Int32 i = 0; i < name.Length; i++)
			{
				Boolean start = Char.IsLetterOrDigit(name[i]) && (i == 0 || !Char.IsLetterOrDigit(name[i - 1]));
				if (!start)
				{
					continue;
				}

				if (String.CompareOrdinal(name, i, token, 0, token.Length) == 0)
				{
					position = i;
					return (WordStartScore + 100) - Math.Min(wordIndex * 8, 60) - Math.Min(lengthPenalty / 4, 30);
				}

				wordIndex++;
			}

			Int32 inside = name.IndexOf(token, StringComparison.Ordinal);
			if (inside >= 0)
			{
				position = inside;
				return (SubstringScore + 100) - Math.Min(inside, 60) - Math.Min(lengthPenalty / 4, 30);
			}

			return ScoreSubsequence(token, name, out position);
		}

		private static Int32? ScoreSubsequence(String token, String name, out Int32 position)
		{
			position = -1;
			Int32 matched = 0;
			Int32 bonus = 0;
			Int32 previous = -2;
			for (Int32 i = 0; i < name.Length && matched < token.Length; i++)
			{
				if (name[i] != token[matched])
				{
					continue;
				}

				if (position < 0)
				{
					position = i;
				}

				if (i == previous + 1)
				{
					bonus += 6;
				}

				if (i == 0 || !Char.IsLetterOrDigit(name[i - 1]))
				{
					bonus += 10;
				}

				previous = i;
				matched++;
			}

			if (matched < token.Length)
			{
				position = -1;
				return null;
			}

			Int32 span = previous - position + 1 - token.Length;
			return Math.Clamp((SubsequenceScore - 100) + bonus - Math.Min(span, 80), 100, SubsequenceScore);
		}

		/// <summary>
		/// Orders names by their score, best first (the order of equal scores is kept).
		/// </summary>
		/// <typeparam name="T">The type of the items.</typeparam>
		/// <param name="items">The items.</param>
		/// <param name="pattern">What the user typed.</param>
		/// <param name="nameOf">Gets the name an item is matched on.</param>
		/// <returns>The items that match, best first.</returns>
		public static IReadOnlyList<T> Rank<T>(IEnumerable<T> items, String pattern, Func<T, String> nameOf)
		{
			ArgumentNullException.ThrowIfNull(items);
			ArgumentNullException.ThrowIfNull(pattern);
			ArgumentNullException.ThrowIfNull(nameOf);

			List<(T Item, Int32 Score, Int32 Index)> scored = [];
			Int32 index = 0;
			foreach (T item in items)
			{
				if (Score(pattern, nameOf(item)) is { } score)
				{
					scored.Add((item, score, index));
				}

				index++;
			}

			scored.Sort(static (a, b) =>
			{
				Int32 byScore = b.Score.CompareTo(a.Score);
				return byScore != 0 ? byScore : a.Index.CompareTo(b.Index);
			});

			List<T> result = new(scored.Count);
			foreach ((T item, _, _) in scored)
			{
				result.Add(item);
			}

			return result;
		}
	}
}