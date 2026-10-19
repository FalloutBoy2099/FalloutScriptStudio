namespace FalloutScriptStudio.Core.Formatting
{
	using System;
	using System.Collections.Generic;
	using System.Text;

	/// <summary>Sets the white space just inside parentheses: none, or one space. Text that is not on the same line as the parenthesis is left alone.</summary>
	internal static class ParenthesisPass
	{
		/// <summary>Applies the rule.</summary>
		/// <param name="text">The text.</param>
		/// <param name="style">The rule.</param>
		/// <returns>The text with the rule applied.</returns>
		public static String Apply(String text, InnerSpacingStyle style)
		{
			if (style == InnerSpacingStyle.Keep)
			{
				return text;
			}

			List<FormatToken> tokens = FormatLexer.Lex(text);
			List<(Int32 Start, Int32 Length, String Replacement)> edits = [];
			for (Int32 t = 0; t < tokens.Count; t++)
			{
				FormatToken token = tokens[t];
				if (token.Kind != FormatTokenKind.Symbol || token.Length != 1)
				{
					continue;
				}

				Char c = text[token.Start];
				if (c == '(')
				{
					AdjustAfterOpen(text, tokens, t, style, edits);
				}
				else if (c == ')')
				{
					AdjustBeforeClose(text, tokens, t, style, edits);
				}
			}

			if (edits.Count == 0)
			{
				return text;
			}

			StringBuilder builder = new StringBuilder(text.Length + edits.Count);
			Int32 position = 0;
			foreach ((Int32 start, Int32 length, String replacement) in edits)
			{
				if (start < position)
				{
					continue;
				}

				builder.Append(text, position, start - position);
				builder.Append(replacement);
				position = start + length;
			}

			builder.Append(text, position, text.Length - position);
			return builder.ToString();
		}

		private static void AdjustAfterOpen(String text, List<FormatToken> tokens, Int32 open, InnerSpacingStyle style, List<(Int32, Int32, String)> edits)
		{
			Int32 next = open + 1;
			if (next >= tokens.Count)
			{
				return;
			}

			Boolean hasSpace = tokens[next].Kind == FormatTokenKind.Space;
			Int32 after = hasSpace ? next + 1 : next;
			if (after >= tokens.Count || !IsInlineCode(tokens[after]) || tokens[after].IsSymbol(text, ")"))
			{
				// The end of the line, a comment or an empty pair: nothing to do.
				return;
			}

			if (style == InnerSpacingStyle.None && hasSpace)
			{
				edits.Add((tokens[next].Start, tokens[next].Length, String.Empty));
			}
			else if (style == InnerSpacingStyle.One && (!hasSpace || !IsSingleSpace(text, tokens[next])))
			{
				edits.Add(hasSpace ? (tokens[next].Start, tokens[next].Length, " ") : (tokens[open].End, 0, " "));
			}
		}

		private static void AdjustBeforeClose(String text, List<FormatToken> tokens, Int32 close, InnerSpacingStyle style, List<(Int32, Int32, String)> edits)
		{
			Int32 before = close - 1;
			if (before < 0)
			{
				return;
			}

			Boolean hasSpace = tokens[before].Kind == FormatTokenKind.Space;
			Int32 beyond = hasSpace ? before - 1 : before;
			if (beyond < 0 || !IsInlineCode(tokens[beyond]) || tokens[beyond].IsSymbol(text, "("))
			{
				// The start of the line, a comment or an empty pair: nothing to do.
				return;
			}

			if (style == InnerSpacingStyle.None && hasSpace)
			{
				edits.Add((tokens[before].Start, tokens[before].Length, String.Empty));
			}
			else if (style == InnerSpacingStyle.One && (!hasSpace || !IsSingleSpace(text, tokens[before])))
			{
				edits.Add(hasSpace ? (tokens[before].Start, tokens[before].Length, " ") : (tokens[close].Start, 0, " "));
			}
		}

		private static Boolean IsInlineCode(FormatToken token)
		{
			return token.Kind is not FormatTokenKind.Newline and not FormatTokenKind.LineComment and not FormatTokenKind.BlockComment and not FormatTokenKind.Space;
		}

		private static Boolean IsSingleSpace(String text, FormatToken token)
		{
			return token.Length == 1 && text[token.Start] == ' ';
		}
	}
}