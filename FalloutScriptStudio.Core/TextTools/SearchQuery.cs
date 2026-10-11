namespace FalloutScriptStudio.Core.TextTools
{
	using System;
	using System.Text.RegularExpressions;

	/// <summary>
	/// What to search for (the options of the legacy search form and of the quick search panel).
	/// </summary>
	/// <param name="Pattern">The text to find, or a regular expression when <see cref="UseRegex"/> is set.</param>
	public sealed record SearchQuery(String Pattern)
	{
		private static readonly TimeSpan _defaultMatchTimeout = TimeSpan.FromSeconds(2);

		/// <summary>Gets a value indicating whether the search is case sensitive.</summary>
		public Boolean MatchCase { get; init; }

		/// <summary>Gets a value indicating whether only whole words match (ignored for regular expressions).</summary>
		public Boolean WholeWord { get; init; }

		/// <summary>Gets a value indicating whether <see cref="Pattern"/> is a regular expression.</summary>
		public Boolean UseRegex { get; init; }

		/// <summary>
		/// Gets the time limit of one regular expression operation. A pattern with catastrophic backtracking such as
		/// <c>(a+)+b</c> then ends with a <see cref="RegexMatchTimeoutException"/> instead of freezing the caller.
		/// </summary>
		public TimeSpan MatchTimeout { get; init; } = _defaultMatchTimeout;

		/// <summary>
		/// Checks that the query can be executed.
		/// </summary>
		/// <param name="error">Receives the reason when the query is invalid; otherwise <c>null</c>.</param>
		/// <returns><c>false</c> when the pattern is empty or an invalid regular expression.</returns>
		public Boolean TryValidate(out String? error)
		{
			if (String.IsNullOrEmpty(this.Pattern))
			{
				error = "The search text is empty.";
				return false;
			}

			if (this.UseRegex)
			{
				try
				{
					_ = new Regex(this.Pattern, RegexOptions.CultureInvariant, this.MatchTimeout);
				}
				catch (ArgumentException ex)
				{
					error = ex.Message;
					return false;
				}
			}

			error = null;
			return true;
		}

		/// <summary>
		/// Creates the regular expression that implements the query.
		/// </summary>
		/// <returns>A regular expression for a regex or whole-word query, or <c>null</c> for a plain text search.</returns>
		/// <exception cref="ArgumentException">The pattern is an invalid regular expression.</exception>
		/// <remarks>The regular expression stops with a <see cref="RegexMatchTimeoutException"/> after <see cref="MatchTimeout"/>.</remarks>
		public Regex? CreateRegex()
		{
			RegexOptions options = RegexOptions.CultureInvariant;
			if (!this.MatchCase)
			{
				options |= RegexOptions.IgnoreCase;
			}

			if (this.UseRegex)
			{
				return new Regex(this.Pattern, options, this.MatchTimeout);
			}

			if (this.WholeWord)
			{
				return new Regex(@"(?<![\p{L}\p{N}_])" + Regex.Escape(this.Pattern) + @"(?![\p{L}\p{N}_])", options, this.MatchTimeout);
			}

			return null;
		}

		/// <summary>Gets the string comparison used for plain text searches.</summary>
		/// <returns>Ordinal comparison, case sensitive or not.</returns>
		public StringComparison GetComparison()
		{
			return this.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
		}
	}
}
