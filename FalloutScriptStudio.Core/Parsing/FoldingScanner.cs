namespace FalloutScriptStudio.Core.Parsing
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// Stateless scanner for the <c>variable begin ... end</c> and <c>#if ... #endif</c> blocks of a script
	/// (legacy <c>ParserInternal.GetFoldingBlock</c>).
	/// </summary>
	internal static class FoldingScanner
	{
		internal const String VariablesTitle = " - Variables - ";

		/// <summary>Finds the variable and <c>#if</c> blocks.</summary>
		/// <param name="text">The script text.</param>
		/// <returns>The regions with 1-based lines, in order of their end.</returns>
		internal static List<FoldingRegion> Scan(String text)
		{
			var result = new List<FoldingRegion>();
			String[] lines = SslSyntax.SplitLines(text);
			var comments = new CommentTracker();

			Int32 ifBegin = -1;
			Int32 variablesBegin = -1;

			for (Int32 i = 0; i < lines.Length; i++)
			{
				String buffer = lines[i].TrimStart().ToLowerInvariant();
				if (comments.ShouldSkip(ref buffer))
				{
					continue;
				}

				buffer = SslSyntax.RemoveTrailingComment(buffer, 0);

				if (ifBegin == -1 && buffer.StartsWith("#if ", StringComparison.Ordinal))
				{
					ifBegin = i;
					continue;
				}

				if (ifBegin > -1 && buffer.StartsWith("#endif", StringComparison.Ordinal))
				{
					result.Add(new FoldingRegion(ifBegin + 1, i + 1, FoldingKind.PreprocessorIf, lines[ifBegin].Trim() + " "));
					ifBegin = -1;
					continue;
				}

				if (variablesBegin == -1 && buffer.StartsWith(SslKeywords.Variable, StringComparison.Ordinal))
				{
					if (IsVariableBegin(buffer))
					{
						variablesBegin = i;
					}
				}
				else if (variablesBegin > -1 && IsEndKeyword(buffer))
				{
					result.Add(new FoldingRegion(variablesBegin + 1, i + 1, FoldingKind.Variables, VariablesTitle));
					variablesBegin = -1;
				}
				else if (variablesBegin > -1
					&& (buffer.StartsWith(SslKeywords.Variable, StringComparison.Ordinal)
						|| buffer.StartsWith(SslKeywords.Procedure, StringComparison.Ordinal)))
				{
					variablesBegin = -1;
				}
			}

			return result;
		}

		private static Boolean IsVariableBegin(String buffer)
		{
			Int32 offset = buffer.IndexOf(" " + SslKeywords.Begin, StringComparison.Ordinal) + 1;
			if (offset <= 0)
			{
				return false;
			}

			Int32 length = offset + SslKeywords.Begin.Length;
			if (buffer.Length > length)
			{
				buffer = buffer.Remove(length);
			}

			Int32 doubleSpace = buffer.IndexOf("  ", 8, StringComparison.Ordinal);
			if (doubleSpace > 0)
			{
				buffer = SslSyntax.RemoveDoubleWhiteSpaces(buffer, doubleSpace, offset);
			}

			return buffer.StartsWith(SslKeywords.Variable + SslKeywords.Begin, StringComparison.Ordinal);
		}

		private static Boolean IsEndKeyword(String buffer)
		{
			return buffer.StartsWith(SslKeywords.End, StringComparison.Ordinal)
				&& (buffer.Length == 3 || (buffer.Length > 3 && !SslSyntax.IsLetterDigitOrUnderscore(buffer[3])));
		}
	}
}