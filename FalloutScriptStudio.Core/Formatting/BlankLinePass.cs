namespace FalloutScriptStudio.Core.Formatting
{
	using System;
	using System.Collections.Generic;
	using System.Text;

	/// <summary>Sets the number of blank lines between procedures and limits the blank lines in a row.</summary>
	internal static class BlankLinePass
	{
		/// <summary>Applies the rules of the options.</summary>
		/// <param name="text">The text.</param>
		/// <param name="options">The options.</param>
		/// <returns>The text with the blank lines adjusted.</returns>
		public static String Apply(String text, FormatOptions options)
		{
			String result = text;
			if (options.BlankLinesBetweenProcedures != FormatOptions.KeepBlankLines)
			{
				result = SetBlankLinesBetweenProcedures(result, options);
			}

			if (options.MaxConsecutiveBlankLines > 0)
			{
				result = LimitRuns(result, options.MaxConsecutiveBlankLines);
			}

			return result;
		}

		private static String SetBlankLinesBetweenProcedures(String text, FormatOptions options)
		{
			FormatModel model = FormatModel.Build(text);
			LayoutInfo info = LayoutAnalyzer.Analyze(model, options);
			if (info.ProcedureStartLines.Count == 0 || info.ProcedureEndLines.Count == 0)
			{
				return text;
			}

			// For each gap: the first line of the lines that are replaced by `blank lines` empty lines.
			Dictionary<Int32, (Int32 LastBlank, Int32 Count)> gaps = [];
			foreach (Int32 start in info.ProcedureStartLines)
			{
				Int32 first = start;
				while (first > 0 && IsLeadingComment(model, model.Lines[first - 1]))
				{
					first--;
				}

				Int32 above = first - 1;
				while (above >= 0 && IsPlainBlank(model, model.Lines[above]))
				{
					above--;
				}

				if (above < 0 || !info.ProcedureEndLines.Contains(above))
				{
					continue;
				}

				gaps[above + 1] = (first - 1, options.BlankLinesBetweenProcedures);
			}

			if (gaps.Count == 0)
			{
				return text;
			}

			StringBuilder builder = new StringBuilder(text.Length + 32);
			Int32 index = 0;
			while (index < model.Lines.Count)
			{
				if (gaps.TryGetValue(index, out (Int32 LastBlank, Int32 Count) gap))
				{
					for (Int32 i = 0; i < gap.Count; i++)
					{
						builder.Append(model.NewLine);
					}

					index = gap.LastBlank + 1;
				}

				FormatLine line = model.Lines[index];
				builder.Append(text, line.Start, line.Next - line.Start);
				index++;
			}

			return builder.ToString();
		}

		private static String LimitRuns(String text, Int32 limit)
		{
			FormatModel model = FormatModel.Build(text);
			StringBuilder builder = new StringBuilder(text.Length);
			Int32 run = 0;
			Boolean changed = false;
			for (Int32 i = 0; i < model.Lines.Count; i++)
			{
				FormatLine line = model.Lines[i];
				Boolean last = i == model.Lines.Count - 1;
				if (!last && IsPlainBlank(model, line))
				{
					run++;
					if (run > limit)
					{
						changed = true;
						continue;
					}
				}
				else
				{
					run = 0;
				}

				builder.Append(text, line.Start, line.Next - line.Start);
			}

			return changed ? builder.ToString() : text;
		}

		private static Boolean IsPlainBlank(FormatModel model, FormatLine line)
		{
			return !line.Verbatim && !line.StartsMultiLineToken && model.IsBlank(line) && line.EolLength > 0;
		}

		/// <summary>Determines whether a line is part of the comment block that sits right above a procedure.</summary>
		private static Boolean IsLeadingComment(FormatModel model, FormatLine line)
		{
			if (model.IsBlank(line))
			{
				return false;
			}

			if (model.FirstCodeToken(line) >= 0)
			{
				return false;
			}

			return line.Verbatim || line.StartsMultiLineToken || model.IsCommentOnly(line);
		}
	}
}