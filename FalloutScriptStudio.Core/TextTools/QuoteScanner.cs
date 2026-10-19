namespace FalloutScriptStudio.Core.TextTools
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// Finds quoted strings in a line of code (legacy <c>Utilities.GetQuotesPosition</c>).
	/// </summary>
	public static class QuoteScanner
	{
		/// <summary>Finds the positions of all complete <c>"..."</c> pairs of a line.</summary>
		/// <param name="line">The line of code.</param>
		/// <returns>The quoted strings in order; a trailing unpaired quote is ignored.</returns>
		public static IReadOnlyList<QuoteSpan> FindQuotes(String line)
		{
			ArgumentNullException.ThrowIfNull(line);

			var quotes = new List<QuoteSpan>();
			Int32 open = line.IndexOf('"');
			while (open > -1)
			{
				Int32 close = line.IndexOf('"', open + 1);
				if (close == -1)
				{
					break;
				}

				quotes.Add(new QuoteSpan(open, close));
				open = line.IndexOf('"', close + 1);
			}

			return quotes;
		}

		/// <summary>Determines whether an index of a line lies inside a quoted string.</summary>
		/// <param name="line">The line of code.</param>
		/// <param name="index">The index (a caret column).</param>
		/// <returns><c>true</c> when the index is strictly between the quotes of a pair.</returns>
		public static Boolean IsInsideQuotes(String line, Int32 index)
		{
			foreach (QuoteSpan quote in FindQuotes(line))
			{
				if (quote.ContainsInside(index))
				{
					return true;
				}
			}

			return false;
		}
	}
}