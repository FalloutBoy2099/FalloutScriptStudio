namespace FalloutScriptStudio.Core.Indexing
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// The matching and ranking rules of the "Go to symbol" filter: case-insensitive, a name that starts with the filter comes before a
	/// name that has the filter at the start of a word (<c>get_level</c> for <c>level</c>, <c>GetLevel</c> for <c>level</c>), which comes before a name
	/// that merely contains it, which comes before a name that contains its letters in order (<c>gdl</c> for <c>get_dude_level</c>).
	/// </summary>
	public static class SymbolMatcher
	{
		/// <summary>The result for a name that does not match.</summary>
		public const Int32 NoMatch = -1;

		/// <summary>The name is the filter.</summary>
		public const Int32 Exact = 0;

		/// <summary>The name starts with the filter.</summary>
		public const Int32 Prefix = 1;

		/// <summary>The filter is at the start of a word of the name.</summary>
		public const Int32 WordStart = 2;

		/// <summary>The name contains the filter.</summary>
		public const Int32 Substring = 3;

		/// <summary>The name contains the letters of the filter in order.</summary>
		public const Int32 Subsequence = 4;

		/// <summary>Splits a filter into its terms (separated by white space).</summary>
		/// <param name="filter">The filter text.</param>
		/// <returns>The terms; empty when the filter is blank.</returns>
		public static IReadOnlyList<String> SplitTerms(String? filter)
		{
			return String.IsNullOrWhiteSpace(filter)
				? Array.Empty<String>()
				: filter.Split((Char[]?)null, StringSplitOptions.RemoveEmptyEntries);
		}

		/// <summary>Gets how well a name matches one term.</summary>
		/// <param name="name">The name.</param>
		/// <param name="term">The term.</param>
		/// <returns>One of the tier constants of this class (a smaller value is a better match), or <see cref="NoMatch"/>.</returns>
		public static Int32 GetTier(String name, String term)
		{
			ArgumentNullException.ThrowIfNull(name);
			ArgumentNullException.ThrowIfNull(term);

			if (term.Length == 0)
			{
				return Prefix;
			}

			if (name.Length < term.Length)
			{
				return NoMatch;
			}

			if (name.Length == term.Length && name.Equals(term, StringComparison.OrdinalIgnoreCase))
			{
				return Exact;
			}

			if (name.StartsWith(term, StringComparison.OrdinalIgnoreCase))
			{
				return Prefix;
			}

			Boolean found = false;
			Int32 index = name.IndexOf(term, 1, StringComparison.OrdinalIgnoreCase);
			while (index >= 0)
			{
				found = true;
				if (IsWordStart(name, index))
				{
					return WordStart;
				}

				index = index + 1 < name.Length ? name.IndexOf(term, index + 1, StringComparison.OrdinalIgnoreCase) : -1;
			}

			if (found)
			{
				return Substring;
			}

			return term.Length >= 2 && IsSubsequence(name, term) ? Subsequence : NoMatch;
		}

		/// <summary>Gets how well a symbol matches a filter.</summary>
		/// <param name="symbol">The symbol.</param>
		/// <param name="terms">The terms of the filter (see <see cref="SplitTerms"/>). The first one is matched against the name, the next ones against the name or the file name.</param>
		/// <returns>The tier of the first term, or <see cref="NoMatch"/>.</returns>
		public static Int32 GetTier(IndexedSymbol symbol, IReadOnlyList<String> terms)
		{
			ArgumentNullException.ThrowIfNull(symbol);
			ArgumentNullException.ThrowIfNull(terms);

			if (terms.Count == 0)
			{
				return Prefix;
			}

			Int32 tier = GetTier(symbol.Name, terms[0]);
			if (tier == NoMatch)
			{
				return NoMatch;
			}

			for (Int32 i = 1; i < terms.Count; i++)
			{
				if (GetTier(symbol.Name, terms[i]) == NoMatch && symbol.File.IndexOf(terms[i], StringComparison.OrdinalIgnoreCase) < 0)
				{
					return NoMatch;
				}
			}

			return tier;
		}

		/// <summary>Compares two matches: the better tier first, then the shorter name, then the name, the file and the line.</summary>
		/// <param name="tierA">The tier of the first symbol.</param>
		/// <param name="a">The first symbol.</param>
		/// <param name="tierB">The tier of the second symbol.</param>
		/// <param name="b">The second symbol.</param>
		/// <returns>The comparison result.</returns>
		public static Int32 Compare(Int32 tierA, IndexedSymbol a, Int32 tierB, IndexedSymbol b)
		{
			ArgumentNullException.ThrowIfNull(a);
			ArgumentNullException.ThrowIfNull(b);

			Int32 result = tierA.CompareTo(tierB);
			if (result != 0)
			{
				return result;
			}

			result = a.Name.Length.CompareTo(b.Name.Length);
			if (result != 0)
			{
				return result;
			}

			result = String.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
			if (result != 0)
			{
				return result;
			}

			result = a.Kind.CompareTo(b.Kind);
			if (result != 0)
			{
				return result;
			}

			result = String.Compare(a.File, b.File, StringComparison.OrdinalIgnoreCase);
			return result != 0 ? result : a.Line.CompareTo(b.Line);
		}

		private static Boolean IsWordStart(String name, Int32 index)
		{
			if (index <= 0)
			{
				return true;
			}

			Char previous = name[index - 1];
			Char current = name[index];
			if (!Char.IsLetterOrDigit(previous))
			{
				return true;
			}

			if (Char.IsUpper(current) && Char.IsLower(previous))
			{
				return true;
			}

			return Char.IsDigit(current) != Char.IsDigit(previous);
		}

		private static Boolean IsSubsequence(String name, String term)
		{
			Int32 next = 0;
			foreach (Char c in name)
			{
				if (Char.ToUpperInvariant(c) == Char.ToUpperInvariant(term[next]))
				{
					next++;
					if (next == term.Length)
					{
						return true;
					}
				}
			}

			return false;
		}
	}
}