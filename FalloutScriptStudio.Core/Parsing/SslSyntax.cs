namespace FalloutScriptStudio.Core.Parsing
{
	using System;
	using System.Collections.Generic;
	using System.Text;

	/// <summary>
	/// Pure text helpers shared by the text based parsers. The class holds no state.
	/// </summary>
	internal static class SslSyntax
	{
		private static readonly Char[] _procedureEnd = { ';', ')' };

		/// <summary>Splits a script into lines; the line terminator (<c>\n</c> or <c>\r\n</c>) is not part of the lines.</summary>
		internal static String[] SplitLines(String text)
		{
			String[] lines = text.Split('\n');
			for (Int32 i = 0; i < lines.Length; i++)
			{
				if (lines[i].EndsWith('\r'))
				{
					lines[i] = lines[i].Substring(0, lines[i].Length - 1);
				}
			}

			return lines;
		}

		/// <summary>Determines whether a character may be part of an identifier.</summary>
		internal static Boolean IsLetterDigitOrUnderscore(Char c)
		{
			return Char.IsLetterOrDigit(c) || c == '_';
		}

		/// <summary>
		/// Removes leading procedure modifiers (<c>import</c>, <c>export</c>, <c>inline</c>, <c>critical</c>, <c>pure</c>).
		/// </summary>
		/// <param name="line">The line without leading white space; modified in place.</param>
		/// <returns>The flags corresponding to the removed modifiers.</returns>
		internal static ProcedureFlags StripProcedureModifiers(ref String line)
		{
			ProcedureFlags flags = ProcedureFlags.None;
			while (true)
			{
				if (StartsWithKeyword(line, "import "))
				{
					flags |= ProcedureFlags.Import;
					line = line.Substring(7).TrimStart();
				}
				else if (StartsWithKeyword(line, "export "))
				{
					flags |= ProcedureFlags.Export;
					line = line.Substring(7).TrimStart();
				}
				else if (StartsWithKeyword(line, "inline "))
				{
					flags |= ProcedureFlags.Inline;
					line = line.Substring(7).TrimStart();
				}
				else if (StartsWithKeyword(line, "critical "))
				{
					flags |= ProcedureFlags.Critical;
					line = line.Substring(9).TrimStart();
				}
				else if (StartsWithKeyword(line, "pure "))
				{
					flags |= ProcedureFlags.Pure;
					line = line.Substring(5).TrimStart();
				}
				else
				{
					return flags;
				}
			}
		}

		/// <summary>Returns the index of the first comment marker (<c>//</c> or <c>/*</c>) at or after <paramref name="from"/>, or -1.</summary>
		internal static Int32 FindCommentStart(String text, Int32 from)
		{
			if (from > text.Length)
			{
				return -1;
			}

			Int32 lineComment = text.IndexOf(SslKeywords.LineComment, from, StringComparison.Ordinal);
			Int32 blockComment = text.IndexOf(SslKeywords.BlockCommentStart, from, StringComparison.Ordinal);
			return lineComment > 0 && blockComment > 0 ? Math.Min(lineComment, blockComment) : Math.Max(lineComment, blockComment);
		}

		/// <summary>Removes a trailing comment (and the white space before it) from a line.</summary>
		internal static String RemoveTrailingComment(String text, Int32 from)
		{
			Int32 index = FindCommentStart(text, from);
			return index > 0 ? text.Remove(index).TrimEnd() : text;
		}

		/// <summary>
		/// Collapses runs of white space between <paramref name="start"/> (inclusive) and <paramref name="end"/> (exclusive)
		/// to a single character.
		/// </summary>
		/// <param name="text">The text.</param>
		/// <param name="start">First index to examine.</param>
		/// <param name="end">Index at which to stop; a value of 0 or less means "up to the end of the text".</param>
		internal static String RemoveDoubleWhiteSpaces(String text, Int32 start, Int32 end)
		{
			Char[] chars = text.ToCharArray();
			Int32 limit = end > 0 ? Math.Min(end, chars.Length - 1) : chars.Length - 1;
			var removed = new List<Int32>();
			for (Int32 i = Math.Max(start, 0); i < limit; i++)
			{
				if (Char.IsWhiteSpace(chars[i]) && Char.IsWhiteSpace(chars[i + 1]))
				{
					removed.Add(i);
				}
			}

			if (removed.Count == 0)
			{
				return text;
			}

			var result = new StringBuilder(chars.Length);
			Int32 next = 0;
			for (Int32 i = 0; i < chars.Length; i++)
			{
				if (next < removed.Count && removed[next] == i)
				{
					next++;
					continue;
				}

				result.Append(chars[i]);
			}

			return result.ToString();
		}

		/// <summary>
		/// Determines whether a (lower-cased) line is the <c>procedure</c> line of the given procedure; double spaces in the
		/// header are collapsed in place.
		/// </summary>
		/// <param name="line">The lower-cased line without modifiers.</param>
		/// <param name="lowerName">The lower-cased procedure name.</param>
		internal static Boolean IsProcedureLine(ref String line, String lowerName)
		{
			if (!line.StartsWith(SslKeywords.Procedure, StringComparison.Ordinal))
			{
				return false;
			}

			Int32 doubleSpace = line.IndexOf("  ", SslKeywords.ProcedureLength - 1, StringComparison.Ordinal);
			if (doubleSpace > 0)
			{
				Int32 limit = FindCommentStart(line, SslKeywords.ProcedureLength - 1);
				Int32 paren = line.IndexOfAny(_procedureEnd, SslKeywords.ProcedureLength - 1);
				if (paren > 0 && (limit == -1 || paren < limit))
				{
					limit = paren;
				}

				if (limit == -1 || doubleSpace < limit)
				{
					line = RemoveDoubleWhiteSpaces(line, doubleSpace, limit);
				}
			}

			if (!line.StartsWith(SslKeywords.Procedure + lowerName, StringComparison.Ordinal))
			{
				return false;
			}

			Int32 position = SslKeywords.ProcedureLength + lowerName.Length;
			return position >= line.Length
				|| line[position] == ';'
				|| line[position] == '('
				|| (!Char.IsLetterOrDigit(line[position]) && !Char.IsPunctuation(line[position]));
		}

		private static Boolean StartsWithKeyword(String line, String keyword)
		{
			return line.StartsWith(keyword, StringComparison.OrdinalIgnoreCase);
		}
	}
}
