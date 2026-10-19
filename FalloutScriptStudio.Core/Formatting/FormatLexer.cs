namespace FalloutScriptStudio.Core.Formatting
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// Splits a script into tokens without losing a character (white space, comments and line breaks are tokens too), so that the passes
	/// of the formatter can rebuild the text from their edits. A string ends at the closing quote or at the end of its line; a comment or a
	/// preprocessor line may span several lines.
	/// </summary>
	internal static class FormatLexer
	{
		private static readonly String[] _operators =
		{
			":=", "==", "!=", "<=", ">=", "&&", "||", "++", "--", "+=", "-=", "*=", "/=", "%=", "<<", ">>",
		};

		/// <summary>Tokenizes a text.</summary>
		/// <param name="text">The text (any line break style).</param>
		/// <returns>The tokens, in order, contiguous.</returns>
		public static List<FormatToken> Lex(String text)
		{
			List<FormatToken> tokens = new((text.Length / 4) + 8);
			Int32 length = text.Length;
			Int32 i = 0;
			Boolean lineStart = true;
			while (i < length)
			{
				Char c = text[i];
				Int32 start = i;
				if (c == '\r' || c == '\n')
				{
					i += c == '\r' && i + 1 < length && text[i + 1] == '\n' ? 2 : 1;
					tokens.Add(new FormatToken(FormatTokenKind.Newline, start, i - start));
					lineStart = true;
					continue;
				}

				if (c == ' ' || c == '\t' || (Char.IsWhiteSpace(c) && c != '\r' && c != '\n'))
				{
					while (i < length && (text[i] == ' ' || text[i] == '\t' || (Char.IsWhiteSpace(text[i]) && text[i] != '\r' && text[i] != '\n')))
					{
						i++;
					}

					tokens.Add(new FormatToken(FormatTokenKind.Space, start, i - start));
					continue;
				}

				if (c == '#' && lineStart)
				{
					i = SkipDirective(text, i);
					tokens.Add(new FormatToken(FormatTokenKind.Directive, start, i - start));
					lineStart = false;
					continue;
				}

				lineStart = false;
				if (c == '/' && i + 1 < length && text[i + 1] == '/')
				{
					i += 2;
					while (i < length && text[i] != '\r' && text[i] != '\n')
					{
						i++;
					}

					tokens.Add(new FormatToken(FormatTokenKind.LineComment, start, i - start));
				}
				else if (c == '/' && i + 1 < length && text[i + 1] == '*')
				{
					i = SkipBlockComment(text, i);
					tokens.Add(new FormatToken(FormatTokenKind.BlockComment, start, i - start));
				}
				else if (c == '"')
				{
					i = SkipString(text, i);
					tokens.Add(new FormatToken(FormatTokenKind.String, start, i - start));
				}
				else if (c >= '0' && c <= '9')
				{
					i++;
					while (i < length && (IsWordChar(text[i]) || text[i] == '.'))
					{
						i++;
					}

					tokens.Add(new FormatToken(FormatTokenKind.Number, start, i - start));
				}
				else if (IsWordStart(c))
				{
					i++;
					while (i < length && IsWordChar(text[i]))
					{
						i++;
					}

					tokens.Add(new FormatToken(FormatTokenKind.Word, start, i - start));
				}
				else
				{
					i += GetOperatorLength(text, i);
					tokens.Add(new FormatToken(FormatTokenKind.Symbol, start, i - start));
				}
			}

			return tokens;
		}

		/// <summary>Determines whether a character can start an identifier.</summary>
		/// <param name="c">The character.</param>
		/// <returns><see langword="true"/> for a letter or an underscore.</returns>
		public static Boolean IsWordStart(Char c)
		{
			return c == '_' || Char.IsLetter(c);
		}

		/// <summary>Determines whether a character can continue an identifier.</summary>
		/// <param name="c">The character.</param>
		/// <returns><see langword="true"/> for a letter, a digit or an underscore.</returns>
		public static Boolean IsWordChar(Char c)
		{
			return c == '_' || Char.IsLetterOrDigit(c);
		}

		private static Int32 GetOperatorLength(String text, Int32 index)
		{
			if (index + 1 < text.Length)
			{
				foreach (String candidate in _operators)
				{
					if (text[index] == candidate[0] && text[index + 1] == candidate[1])
					{
						return 2;
					}
				}
			}

			return 1;
		}

		private static Int32 SkipString(String text, Int32 index)
		{
			Int32 i = index + 1;
			while (i < text.Length && text[i] != '"' && text[i] != '\r' && text[i] != '\n')
			{
				i++;
			}

			return i < text.Length && text[i] == '"' ? i + 1 : i;
		}

		private static Int32 SkipBlockComment(String text, Int32 index)
		{
			Int32 close = text.IndexOf("*/", index + 2, StringComparison.Ordinal);
			return close < 0 ? text.Length : close + 2;
		}

		/// <summary>Finds the end of a preprocessor line: backslash continuations, block comments that span lines and strings are followed.</summary>
		private static Int32 SkipDirective(String text, Int32 index)
		{
			Int32 length = text.Length;
			Int32 i = index;
			while (i < length)
			{
				Char c = text[i];
				if (c == '\r' || c == '\n')
				{
					return i;
				}

				if (c == '\\' && IsContinuation(text, i, out Int32 skip))
				{
					i += skip;
					continue;
				}

				if (c == '"')
				{
					i = SkipString(text, i);
					continue;
				}

				if (c == '/' && i + 1 < length && text[i + 1] == '/')
				{
					while (i < length && text[i] != '\r' && text[i] != '\n')
					{
						i++;
					}

					return i;
				}

				if (c == '/' && i + 1 < length && text[i + 1] == '*')
				{
					i = SkipBlockComment(text, i);
					continue;
				}

				i++;
			}

			return i;
		}

		private static Boolean IsContinuation(String text, Int32 index, out Int32 skip)
		{
			skip = 0;
			Int32 next = index + 1;
			if (next < text.Length && text[next] == '\r')
			{
				next++;
				if (next < text.Length && text[next] == '\n')
				{
					next++;
				}
			}
			else if (next < text.Length && text[next] == '\n')
			{
				next++;
			}
			else
			{
				return false;
			}

			skip = next - index;
			return true;
		}
	}
}