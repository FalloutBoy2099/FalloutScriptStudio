namespace FalloutScriptStudio.Core.Formatting
{
	using System;
	using System.Collections.Generic;
	using System.Text;

	/// <summary>Breaks the lines that are longer than the limit after a comma of an argument list (a comma inside brackets). A line without such a comma is left alone.</summary>
	internal static class WrapPass
	{
		/// <summary>Wraps the long lines.</summary>
		/// <param name="text">The text.</param>
		/// <param name="options">The options.</param>
		/// <returns>The text with the long lines wrapped.</returns>
		public static String Apply(String text, FormatOptions options)
		{
			FormatModel model = FormatModel.Build(text);
			LayoutInfo info = LayoutAnalyzer.Analyze(model, options);
			StringBuilder builder = new StringBuilder(text.Length + 128);
			Boolean changed = false;
			foreach (FormatLine line in model.Lines)
			{
				if (line.Verbatim || line.StartsMultiLineToken || !IsTooLong(model, line, options) || !WrapLine(model, info, line, options, builder))
				{
					builder.Append(text, line.Start, line.Next - line.Start);
				}
				else
				{
					changed = true;
				}
			}

			return changed ? builder.ToString() : text;
		}

		private static Boolean IsTooLong(FormatModel model, FormatLine line, FormatOptions options)
		{
			Int32 first = model.FirstCodeToken(line);
			if (first < 0 || model.Tokens[first].Kind == FormatTokenKind.Directive)
			{
				return false;
			}

			Int32 end = model.LastCodeToken(line);
			return Width(model.Text, line.Start, model.Tokens[end].End, 0, options.IndentSize) > options.MaxLineLength;
		}

		private static Boolean WrapLine(FormatModel model, LayoutInfo info, FormatLine line, FormatOptions options, StringBuilder builder)
		{
			String text = model.Text;
			List<FormatToken> tokens = model.Tokens;
			Int32 size = options.IndentSize;
			Int32 max = options.MaxLineLength;
			String indent = model.GetIndent(line);
			String unit = options.UseTabs ? "\t" : new String(' ', size);
			String hanging = info.IsContinuation[line.Index] ? indent : indent + Repeat(unit, options.ContinuationIndent);
			Int32 lastCode = model.LastCodeToken(line);

			Int32 pieceStart = line.Start + indent.Length;
			Int32 pieceIndentWidth = Width(indent, 0, indent.Length, 0, size);
			String pieceIndent = indent;
			List<Bracket> stack = [];
			Candidate? candidate = null;
			Int32 breaks = 0;
			StringBuilder output = new StringBuilder(line.Length + 32);
			output.Append(indent);

			Int32 t = line.FirstToken;
			while (t < line.EndToken)
			{
				FormatToken token = tokens[t];
				if (token.IsTrivia || token.Kind == FormatTokenKind.Directive)
				{
					t++;
					continue;
				}

				Int32 endColumn = Width(text, pieceStart, token.End, pieceIndentWidth, size);
				if (endColumn > max && candidate is not null)
				{
					BreakAt(model, candidate, ref pieceStart, ref pieceIndent, ref pieceIndentWidth, ref stack, output, hanging, options);
					breaks++;
					t = candidate.Token + 1;
					candidate = null;
					continue;
				}

				if (token.Kind == FormatTokenKind.Symbol && token.Length == 1)
				{
					Char c = text[token.Start];
					if (c == '(' || c == '[' || c == '{')
					{
						Int32 afterBracket = t + 1 < tokens.Count && tokens[t + 1].Kind == FormatTokenKind.Space ? tokens[t + 1].End : token.End;
						stack.Add(new Bracket(Width(text, pieceStart, afterBracket, pieceIndentWidth, size), lastCode > t));
					}
					else if ((c == ')' || c == ']' || c == '}') && stack.Count > 0)
					{
						stack.RemoveAt(stack.Count - 1);
					}
					else if (c == ',' && info.DepthBefore[t] >= 1 && HasCodeAfter(model, t, line))
					{
						candidate = new Candidate(t, new(stack));
						if (endColumn > max)
						{
							BreakAt(model, candidate, ref pieceStart, ref pieceIndent, ref pieceIndentWidth, ref stack, output, hanging, options);
							breaks++;
							candidate = null;
						}
					}
				}

				t++;
			}

			if (breaks == 0)
			{
				return false;
			}

			output.Append(text, pieceStart, line.Next - pieceStart);
			builder.Append(output);
			return true;
		}

		private static void BreakAt(
			FormatModel model,
			Candidate candidate,
			ref Int32 pieceStart,
			ref String pieceIndent,
			ref Int32 pieceIndentWidth,
			ref List<Bracket> stack,
			StringBuilder output,
			String hanging,
			FormatOptions options)
		{
			FormatToken comma = model.Tokens[candidate.Token];
			output.Append(model.Text, pieceStart, comma.End - pieceStart);
			output.Append(model.NewLine);

			Int32 next = candidate.Token + 1;
			while (next < model.Tokens.Count && model.Tokens[next].Kind == FormatTokenKind.Space)
			{
				next++;
			}

			stack = new(candidate.Stack);
			if (options.AlignContinuationToBracket && stack.Count > 0 && stack[stack.Count - 1].HasContent)
			{
				Int32 column = stack[stack.Count - 1].Column;
				pieceIndent = options.UseTabs
					? new String('\t', column / options.IndentSize) + new String(' ', column % options.IndentSize)
					: new String(' ', column);
			}
			else
			{
				pieceIndent = hanging;
			}

			pieceIndentWidth = Width(pieceIndent, 0, pieceIndent.Length, 0, options.IndentSize);
			output.Append(pieceIndent);
			pieceStart = model.Tokens[next].Start;
		}

		private static Boolean HasCodeAfter(FormatModel model, Int32 token, FormatLine line)
		{
			for (Int32 t = token + 1; t < line.EndToken; t++)
			{
				if (!model.Tokens[t].IsTrivia)
				{
					return true;
				}
			}

			return false;
		}

		private static String Repeat(String value, Int32 count)
		{
			StringBuilder builder = new StringBuilder(value.Length * count);
			for (Int32 i = 0; i < count; i++)
			{
				builder.Append(value);
			}

			return builder.ToString();
		}

		/// <summary>Gets the column reached after the characters between two offsets, tabs advancing to the next multiple of the tab size.</summary>
		private static Int32 Width(String text, Int32 from, Int32 to, Int32 startColumn, Int32 size)
		{
			Int32 column = startColumn;
			for (Int32 i = from; i < to; i++)
			{
				column += text[i] == '\t' ? size - (column % size) : 1;
			}

			return column;
		}

		private sealed record Bracket(Int32 Column, Boolean HasContent);

		private sealed record Candidate(Int32 Token, List<Bracket> Stack);
	}
}