namespace FalloutScriptStudio.Core.Analysis
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// Splits the text of an SSL script or header into tokens in a single pass. Comments and white space are skipped, strings end at the
	/// closing quote or at the end of the line, and a preprocessor line (with its backslash continuations) is marked on its tokens, so
	/// that the analyses can ignore it or look at it on purpose.
	/// </summary>
	public static class SslTokenizer
	{
		private static readonly String[] _operators =
		{
			":=", "==", "!=", "<=", ">=", "&&", "||", "++", "--", "+=", "-=", "*=", "/=", "%=", "<<", ">>",
		};

		/// <summary>Tokenizes a text.</summary>
		/// <param name="text">The script text (any line break style).</param>
		/// <returns>The tokens.</returns>
		public static SslTokenStream Tokenize(String text)
		{
			ArgumentNullException.ThrowIfNull(text);

			List<SslToken> tokens = new List<SslToken>((text.Length / 5) + 16);
			Int32 length = text.Length;
			Int32 i = 0;
			Int32 line = 1;
			Boolean lineStart = true;
			Boolean directive = false;

			while (i < length)
			{
				Char c = text[i];
				if (c == '\n' || (c == '\r' && (i + 1 >= length || text[i + 1] != '\n')))
				{
					line++;
					i++;
					lineStart = true;
					directive = false;
					continue;
				}

				if (c == '\r' || c == ' ' || c == '\t' || Char.IsWhiteSpace(c))
				{
					i++;
					continue;
				}

				if (c == '\\' && directive && IsLineContinuation(text, i, out Int32 skip))
				{
					i += skip;
					line++;
					continue;
				}

				if (c == '/' && i + 1 < length && text[i + 1] == '/')
				{
					i += 2;
					while (i < length && text[i] != '\n' && text[i] != '\r')
					{
						i++;
					}

					continue;
				}

				if (c == '/' && i + 1 < length && text[i + 1] == '*')
				{
					i += 2;
					while (i < length && !(text[i] == '*' && i + 1 < length && text[i + 1] == '/'))
					{
						if (text[i] == '\n' || (text[i] == '\r' && (i + 1 >= length || text[i + 1] != '\n')))
						{
							line++;
						}

						i++;
					}

					i = Math.Min(i + 2, length);
					continue;
				}

				Int32 start = i;
				if (c == '#' && lineStart && !directive)
				{
					i++;
					while (i < length && (text[i] == ' ' || text[i] == '\t'))
					{
						i++;
					}

					while (i < length && IsWordChar(text[i]))
					{
						i++;
					}

					directive = true;
					lineStart = false;
					tokens.Add(new SslToken(SslTokenKind.Directive, start, i - start, line, true));
					continue;
				}

				lineStart = false;
				if (c == '"')
				{
					i++;
					while (i < length && text[i] != '"' && text[i] != '\n' && text[i] != '\r')
					{
						i++;
					}

					if (i < length && text[i] == '"')
					{
						i++;
					}

					tokens.Add(new SslToken(SslTokenKind.String, start, i - start, line, directive));
				}
				else if (c >= '0' && c <= '9')
				{
					i++;
					while (i < length && (IsWordChar(text[i]) || text[i] == '.'))
					{
						i++;
					}

					tokens.Add(new SslToken(SslTokenKind.Number, start, i - start, line, directive));
				}
				else if (IsWordStart(c))
				{
					i++;
					while (i < length && IsWordChar(text[i]))
					{
						i++;
					}

					tokens.Add(new SslToken(SslTokenKind.Identifier, start, i - start, line, directive));
				}
				else
				{
					Int32 size = GetOperatorLength(text, i);
					i += size;
					tokens.Add(new SslToken(SslTokenKind.Punctuation, start, size, line, directive));
				}
			}

			return new SslTokenStream(text, tokens.ToArray());
		}

		private static Boolean IsWordStart(Char c)
		{
			return c == '_' || Char.IsLetter(c);
		}

		private static Boolean IsWordChar(Char c)
		{
			return c == '_' || Char.IsLetterOrDigit(c);
		}

		private static Boolean IsLineContinuation(String text, Int32 index, out Int32 skip)
		{
			skip = 0;
			Int32 next = index + 1;
			while (next < text.Length && (text[next] == ' ' || text[next] == '\t'))
			{
				next++;
			}

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
	}
}
