namespace FalloutScriptStudio.Core.Formatting
{
	using System;
	using System.Collections.Generic;
	using System.Text;

	/// <summary>
	/// Moves the keywords that open and close blocks: the <c>begin</c> of a block (same line as its header or on a line of its own), the pair
	/// <c>else begin</c> and the <c>else</c> that follows an <c>end</c>. Only the white space between two tokens changes, and nothing moves
	/// when a comment or a preprocessor line is in the way.
	/// </summary>
	internal static class PlacementPass
	{
		/// <summary>Applies the placement rules of the options.</summary>
		/// <param name="text">The text.</param>
		/// <param name="options">The options.</param>
		/// <returns>The text with the keywords placed.</returns>
		public static String Apply(String text, FormatOptions options)
		{
			String result = text;
			if (options.Else != ElsePlacement.Keep)
			{
				result = PlaceElse(result, options.Else);
			}

			if (options.Begin != BeginPlacement.Keep)
			{
				result = PlaceBegin(result, options.Begin);
			}

			if (options.ElseBegin != ElseBeginStyle.Keep)
			{
				result = PlaceElseBegin(result, options.ElseBegin);
			}

			return result;
		}

		private static String PlaceElse(String text, ElsePlacement placement)
		{
			FormatModel model = FormatModel.Build(text);
			List<Gap> gaps = [];
			for (Int32 t = 0; t < model.Tokens.Count; t++)
			{
				if (!model.Tokens[t].IsWord(text, "else"))
				{
					continue;
				}

				Gap? gap = FindGapBefore(model, t);
				if (gap is null || !model.Tokens[gap.Previous].IsWord(text, "end"))
				{
					continue;
				}

				if (placement == ElsePlacement.AfterEnd && gap.HasNewline)
				{
					gaps.Add(gap with { Replacement = " " });
				}
				else if (placement == ElsePlacement.OwnLine && !gap.HasNewline)
				{
					gaps.Add(gap with { Replacement = model.NewLine + model.GetIndent(model.Lines[LineOf(model, model.Tokens[gap.Previous].Start)]) });
				}
			}

			return ApplyGaps(text, gaps);
		}

		private static String PlaceBegin(String text, BeginPlacement placement)
		{
			FormatModel model = FormatModel.Build(text);
			List<Gap> gaps = [];
			for (Int32 t = 0; t < model.Tokens.Count; t++)
			{
				if (!model.Tokens[t].IsWord(text, "begin"))
				{
					continue;
				}

				Gap? gap = FindGapBefore(model, t);
				if (gap is null || !OpensBlock(model, gap.Previous))
				{
					continue;
				}

				if (placement == BeginPlacement.SameLine && gap.HasNewline)
				{
					gaps.Add(gap with { Replacement = " " });
				}
				else if (placement == BeginPlacement.NextLine && !gap.HasNewline && EndsLine(model, t))
				{
					gaps.Add(gap with { Replacement = model.NewLine + model.GetIndent(model.Lines[LineOf(model, model.Tokens[gap.Previous].Start)]) });
				}
			}

			return ApplyGaps(text, gaps);
		}

		private static String PlaceElseBegin(String text, ElseBeginStyle style)
		{
			FormatModel model = FormatModel.Build(text);
			List<Gap> gaps = [];
			for (Int32 t = 0; t < model.Tokens.Count; t++)
			{
				if (!model.Tokens[t].IsWord(text, "begin"))
				{
					continue;
				}

				Gap? gap = FindGapBefore(model, t);
				if (gap is null || !model.Tokens[gap.Previous].IsWord(text, "else"))
				{
					continue;
				}

				if (style == ElseBeginStyle.Join && gap.HasNewline)
				{
					gaps.Add(gap with { Replacement = " " });
				}
				else if (style == ElseBeginStyle.Split && !gap.HasNewline && EndsLine(model, t))
				{
					gaps.Add(gap with { Replacement = model.NewLine + model.GetIndent(model.Lines[LineOf(model, model.Tokens[gap.Previous].Start)]) });
				}
			}

			return ApplyGaps(text, gaps);
		}

		/// <summary>Finds the white space between a token and the code token before it; <see langword="null"/> when a comment or a directive is in the way.</summary>
		private static Gap? FindGapBefore(FormatModel model, Int32 token)
		{
			Boolean newline = false;
			Int32 t = token - 1;
			while (t >= 0 && model.Tokens[t].Kind is FormatTokenKind.Space or FormatTokenKind.Newline)
			{
				newline |= model.Tokens[t].Kind == FormatTokenKind.Newline;
				t--;
			}

			if (t < 0 || model.Tokens[t].IsTrivia || model.Tokens[t].Kind == FormatTokenKind.Directive)
			{
				return null;
			}

			FormatToken previous = model.Tokens[t];
			return new Gap(t, previous.End, model.Tokens[token].Start - previous.End, newline, String.Empty);
		}

		/// <summary>Determines whether a token is something a <c>begin</c> may follow when it opens a block: <c>then</c>, <c>do</c>, <c>else</c>, a closing parenthesis, a colon or the name of a procedure.</summary>
		private static Boolean OpensBlock(FormatModel model, Int32 previous)
		{
			FormatToken token = model.Tokens[previous];
			String text = model.Text;
			if (token.IsWord(text, "then") || token.IsWord(text, "do") || token.IsWord(text, "else") || token.IsSymbol(text, ")") || token.IsSymbol(text, ":"))
			{
				return true;
			}

			if (token.Kind != FormatTokenKind.Word)
			{
				return false;
			}

			FormatLine line = model.Lines[LineOf(model, token.Start)];
			Int32 first = model.FirstCodeToken(line);
			return first >= 0 && model.Tokens[first].IsWord(text, "procedure");
		}

		/// <summary>Determines whether nothing but white space and comments follows a token on its line.</summary>
		private static Boolean EndsLine(FormatModel model, Int32 token)
		{
			Int32 t = token + 1;
			while (t < model.Tokens.Count && model.Tokens[t].Kind != FormatTokenKind.Newline)
			{
				if (!model.Tokens[t].IsTrivia)
				{
					return false;
				}

				t++;
			}

			return true;
		}

		private static Int32 LineOf(FormatModel model, Int32 offset)
		{
			Int32 low = 0;
			Int32 high = model.Lines.Count - 1;
			while (low < high)
			{
				Int32 middle = (low + high + 1) / 2;
				if (model.Lines[middle].Start <= offset)
				{
					low = middle;
				}
				else
				{
					high = middle - 1;
				}
			}

			return low;
		}

		private static String ApplyGaps(String text, List<Gap> gaps)
		{
			if (gaps.Count == 0)
			{
				return text;
			}

			StringBuilder builder = new StringBuilder(text.Length + (gaps.Count * 8));
			Int32 position = 0;
			foreach (Gap gap in gaps)
			{
				if (gap.Start < position)
				{
					continue;
				}

				builder.Append(text, position, gap.Start - position);
				builder.Append(gap.Replacement);
				position = gap.Start + gap.Length;
			}

			builder.Append(text, position, text.Length - position);
			return builder.ToString();
		}

		/// <summary>The white space between two code tokens.</summary>
		private sealed record Gap(Int32 Previous, Int32 Start, Int32 Length, Boolean HasNewline, String Replacement);
	}
}