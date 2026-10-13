namespace FalloutScriptStudio.Core.Analysis
{
	using System;

	/// <summary>Finds where statements and blocks of a script end, working on the tokens.</summary>
	public static class SslStatements
	{
		/// <summary>The value returned when a statement or block is not closed before the end of the tokens.</summary>
		public const Int32 Unterminated = Int32.MaxValue;

		/// <summary>Finds the <c>end</c> that closes the <c>begin</c> at a token.</summary>
		/// <param name="tokens">The tokens.</param>
		/// <param name="begin">The index of the <c>begin</c>.</param>
		/// <returns>The index of the matching <c>end</c>, or <see cref="Unterminated"/>.</returns>
		public static Int32 FindMatchingEnd(SslTokenStream tokens, Int32 begin)
		{
			ArgumentNullException.ThrowIfNull(tokens);

			Int32 depth = 0;
			for (Int32 i = begin; i < tokens.Count; i++)
			{
				if (tokens[i].InDirective || tokens[i].Kind != SslTokenKind.Identifier)
				{
					continue;
				}

				if (tokens.IsKeyword(i, "begin"))
				{
					depth++;
				}
				else if (tokens.IsKeyword(i, "end"))
				{
					depth--;
					if (depth == 0)
					{
						return i;
					}
				}
			}

			return Unterminated;
		}

		/// <summary>Finds the closing parenthesis that matches the opening one at a token.</summary>
		/// <param name="tokens">The tokens.</param>
		/// <param name="open">The index of the <c>(</c>.</param>
		/// <returns>The index of the matching <c>)</c>, or <see cref="Unterminated"/>.</returns>
		public static Int32 FindMatchingParenthesis(SslTokenStream tokens, Int32 open)
		{
			ArgumentNullException.ThrowIfNull(tokens);

			Int32 depth = 0;
			for (Int32 i = open; i < tokens.Count; i++)
			{
				if (tokens[i].InDirective)
				{
					continue;
				}

				if (tokens.IsPunctuation(i, '('))
				{
					depth++;
				}
				else if (tokens.IsPunctuation(i, ')'))
				{
					depth--;
					if (depth == 0)
					{
						return i;
					}
				}
			}

			return Unterminated;
		}

		/// <summary>
		/// Finds the last token of the statement that starts at a token: a block (<c>begin ... end</c>), a control statement with its body
		/// (and its <c>else</c> part), or a simple statement up to its semicolon.
		/// </summary>
		/// <param name="tokens">The tokens.</param>
		/// <param name="start">The index of the first token of the statement.</param>
		/// <returns>The index of the last token, or <see cref="Unterminated"/> when the statement is not complete.</returns>
		public static Int32 FindStatementEnd(SslTokenStream tokens, Int32 start)
		{
			ArgumentNullException.ThrowIfNull(tokens);

			Int32 count = tokens.Count;
			Int32 i = start;
			while (true)
			{
				if (i >= count)
				{
					return Unterminated;
				}

				Int32 end;
				if (tokens.IsKeyword(i, "begin"))
				{
					end = FindMatchingEnd(tokens, i);
				}
				else if (IsControl(tokens, i))
				{
					Int32 next = i + 1;
					if (tokens.IsPunctuation(next, '('))
					{
						Int32 close = FindMatchingParenthesis(tokens, next);
						if (close == Unterminated)
						{
							return Unterminated;
						}

						next = close + 1;
					}

					if (tokens.IsKeyword(next, "then") || tokens.IsKeyword(next, "do"))
					{
						next++;
					}

					Int32 bodyEnd = FindStatementEnd(tokens, next);
					if (bodyEnd == Unterminated)
					{
						return Unterminated;
					}

					end = bodyEnd;
					if (tokens.IsKeyword(i, "if") && tokens.IsKeyword(end + 1, "else"))
					{
						// The else part is a statement of its own; continue with it.
						i = end + 2;
						continue;
					}
				}
				else
				{
					end = FindSimpleStatementEnd(tokens, i);
				}

				return end;
			}
		}

		private static Boolean IsControl(SslTokenStream tokens, Int32 index)
		{
			return tokens.IsKeyword(index, "if") || tokens.IsKeyword(index, "while") || tokens.IsKeyword(index, "for")
				|| tokens.IsKeyword(index, "foreach") || tokens.IsKeyword(index, "switch");
		}

		private static Int32 FindSimpleStatementEnd(SslTokenStream tokens, Int32 start)
		{
			Int32 depth = 0;
			for (Int32 i = start; i < tokens.Count; i++)
			{
				if (tokens[i].InDirective)
				{
					continue;
				}

				if (tokens.IsPunctuation(i, '(') || tokens.IsPunctuation(i, '['))
				{
					depth++;
				}
				else if (tokens.IsPunctuation(i, ')') || tokens.IsPunctuation(i, ']'))
				{
					depth--;
				}
				else if (depth <= 0 && tokens.IsPunctuation(i, ';'))
				{
					return i;
				}
				else if (depth <= 0 && (tokens.IsKeyword(i, "end") || tokens.IsKeyword(i, "begin") || tokens.IsKeyword(i, "procedure")))
				{
					return Unterminated;
				}
			}

			return Unterminated;
		}
	}
}
