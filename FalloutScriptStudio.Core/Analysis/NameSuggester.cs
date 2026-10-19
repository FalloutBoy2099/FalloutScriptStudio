namespace FalloutScriptStudio.Core.Analysis
{
	using System;
	using System.Collections.Generic;

	/// <summary>Finds the known name that is nearest to a misspelled one (the "Did you mean ...?" of a typo), by the edit distance.</summary>
	public static class NameSuggester
	{
		/// <summary>The shortest name that gets a suggestion: a suggestion for a very short word is more often wrong than useful.</summary>
		public const Int32 MinimumLength = 4;

		/// <summary>Gets the most edits (insertion, deletion, replacement, swap of neighbours) a suggestion may differ by, for a name of some length.</summary>
		/// <param name="length">The length of the misspelled name.</param>
		/// <returns>1 for names up to 7 characters, 2 up to 11, 3 beyond; 0 when the name is too short for a suggestion.</returns>
		public static Int32 GetMaximumDistance(Int32 length)
		{
			return length < MinimumLength ? 0 : Math.Min(3, Math.Max(1, length / 4));
		}

		/// <summary>Finds the nearest candidate.</summary>
		/// <param name="name">The misspelled name.</param>
		/// <param name="candidates">The known names, most preferred first: of two candidates at the same distance the first one wins.</param>
		/// <returns>The candidate as written in <paramref name="candidates"/>, or <see langword="null"/> when none is near enough.</returns>
		public static String? Suggest(String name, IEnumerable<String> candidates)
		{
			ArgumentNullException.ThrowIfNull(name);
			ArgumentNullException.ThrowIfNull(candidates);

			Int32 limit = GetMaximumDistance(name.Length);
			if (limit == 0)
			{
				return null;
			}

			String? best = null;
			Int32 bestDistance = limit + 1;
			foreach (String candidate in candidates)
			{
				Int32 lengthGap = Math.Abs(candidate.Length - name.Length);
				if (lengthGap > limit || lengthGap >= bestDistance)
				{
					continue;
				}

				Int32 distance = GetDistance(name, candidate, bestDistance - 1);
				if (distance < bestDistance && distance > 0)
				{
					best = candidate;
					bestDistance = distance;
					if (distance == 1)
					{
						break;
					}
				}
			}

			return best;
		}

		/// <summary>Computes the edit distance (optimal string alignment: insertion, deletion, replacement and swap of two neighbours), ignoring the case.</summary>
		/// <param name="a">The first text.</param>
		/// <param name="b">The second text.</param>
		/// <param name="limit">The distance from which the exact value does not matter; the result is then at least <c>limit + 1</c>.</param>
		/// <returns>The distance, or more than <paramref name="limit"/> when it is larger.</returns>
		public static Int32 GetDistance(String a, String b, Int32 limit)
		{
			ArgumentNullException.ThrowIfNull(a);
			ArgumentNullException.ThrowIfNull(b);

			Int32 n = a.Length;
			Int32 m = b.Length;
			if (Math.Abs(n - m) > limit)
			{
				return limit + 1;
			}

			Span<Int32> rows = m < 120 ? stackalloc Int32[(m + 1) * 3] : new Int32[(m + 1) * 3];
			Span<Int32> previousPrevious = rows.Slice(0, m + 1);
			Span<Int32> previous = rows.Slice(m + 1, m + 1);
			Span<Int32> current = rows.Slice(2 * (m + 1), m + 1);
			for (Int32 j = 0; j <= m; j++)
			{
				previous[j] = j;
			}

			for (Int32 i = 1; i <= n; i++)
			{
				current[0] = i;
				Int32 rowMinimum = current[0];
				Char ca = Char.ToLowerInvariant(a[i - 1]);
				for (Int32 j = 1; j <= m; j++)
				{
					Char cb = Char.ToLowerInvariant(b[j - 1]);
					Int32 cost = ca == cb ? 0 : 1;
					Int32 value = Math.Min(Math.Min(previous[j] + 1, current[j - 1] + 1), previous[j - 1] + cost);
					if (i > 1 && j > 1 && ca == Char.ToLowerInvariant(b[j - 2]) && Char.ToLowerInvariant(a[i - 2]) == cb)
					{
						value = Math.Min(value, previousPrevious[j - 2] + 1);
					}

					current[j] = value;
					rowMinimum = Math.Min(rowMinimum, value);
				}

				if (rowMinimum > limit)
				{
					return limit + 1;
				}

				Span<Int32> recycled = previousPrevious;
				previousPrevious = previous;
				previous = current;
				current = recycled;
			}

			return previous[m];
		}
	}
}