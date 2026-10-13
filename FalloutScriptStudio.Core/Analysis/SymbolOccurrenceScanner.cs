namespace FalloutScriptStudio.Core.Analysis
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// Finds where a name is written in a script text. Comments are skipped, a name in a string is reported only when it is the whole
	/// string, and the tokens of preprocessor lines are looked at (a macro body can call a procedure).
	/// </summary>
	public static class SymbolOccurrenceScanner
	{
		/// <summary>Scans a text.</summary>
		/// <param name="text">The script text.</param>
		/// <param name="file">The path of the file the text comes from (copied into the occurrences).</param>
		/// <param name="name">The name to look for.</param>
		/// <param name="caseSensitive">Whether the case matters (macros) or not (procedures and variables).</param>
		/// <param name="includeStrings">Whether a string literal that is exactly the name is reported as well.</param>
		/// <returns>The occurrences in text order.</returns>
		public static IReadOnlyList<SymbolOccurrence> Scan(String text, String file, String name, Boolean caseSensitive, Boolean includeStrings = false)
		{
			ArgumentNullException.ThrowIfNull(text);
			ArgumentNullException.ThrowIfNull(file);
			ArgumentException.ThrowIfNullOrEmpty(name);

			StringComparison comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
			if (text.IndexOf(name, comparison) < 0)
			{
				return Array.Empty<SymbolOccurrence>();
			}

			SslTokenStream tokens = SslTokenizer.Tokenize(text);
			List<SymbolOccurrence> result = new List<SymbolOccurrence>();
			for (Int32 i = 0; i < tokens.Count; i++)
			{
				SslToken token = tokens[i];
				if (token.Kind == SslTokenKind.Identifier)
				{
					if (token.Length == name.Length && tokens.Span(i).Equals(name, comparison))
					{
						Boolean declaration = (i > 0 && tokens.IsKeyword(i - 1, "procedure") && !tokens[i - 1].InDirective)
							|| (i > 0 && tokens[i - 1].Kind == SslTokenKind.Directive && tokens.GetDirectiveName(i - 1) is "define" or "undef");
						result.Add(new SymbolOccurrence(file, token.Offset, token.Line, tokens.GetColumn(token.Offset), token.Length, declaration ? SymbolOccurrenceKind.Declaration : SymbolOccurrenceKind.Reference));
					}
				}
				else if (includeStrings && token.Kind == SslTokenKind.String && token.Length == name.Length + 2 && !IsIncludeDirective(tokens, i))
				{
					if (text.AsSpan(token.Offset + 1, name.Length).Equals(name, comparison))
					{
						result.Add(new SymbolOccurrence(file, token.Offset + 1, token.Line, tokens.GetColumn(token.Offset) + 1, name.Length, SymbolOccurrenceKind.StringLiteral));
					}
				}
			}

			return result;
		}

		private static Boolean IsIncludeDirective(SslTokenStream tokens, Int32 index)
		{
			return index > 0 && tokens[index - 1].Kind == SslTokenKind.Directive && tokens.GetDirectiveName(index - 1) == "include";
		}
	}
}
