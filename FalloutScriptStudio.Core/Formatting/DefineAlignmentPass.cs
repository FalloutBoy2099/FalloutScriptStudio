namespace FalloutScriptStudio.Core.Formatting
{
	using System;
	using System.Collections.Generic;
	using System.Text;

	/// <summary>Makes the values of consecutive <c>#define</c> lines start in the same column. Only the white space between a macro and its value changes.</summary>
	internal static class DefineAlignmentPass
	{
		/// <summary>The longest prefix (<c>#define NAME(params)</c>) that takes part in an alignment; a longer one is left alone.</summary>
		private const Int32 MaxPrefix = 48;

		/// <summary>Aligns the values.</summary>
		/// <param name="text">The text.</param>
		/// <returns>The aligned text.</returns>
		public static String Apply(String text)
		{
			FormatModel model = FormatModel.Build(text);
			DefineLine?[] parsed = new DefineLine?[model.Lines.Count];
			Boolean any = false;
			for (Int32 i = 0; i < model.Lines.Count; i++)
			{
				parsed[i] = Parse(model, model.Lines[i]);
				any |= parsed[i] is not null;
			}

			if (!any)
			{
				return text;
			}

			StringBuilder builder = new StringBuilder(text.Length + 64);
			Boolean changed = false;
			Int32 index = 0;
			while (index < model.Lines.Count)
			{
				if (parsed[index] is null)
				{
					FormatLine plain = model.Lines[index];
					builder.Append(text, plain.Start, plain.Next - plain.Start);
					index++;
					continue;
				}

				Int32 end = index;
				while (end + 1 < model.Lines.Count && parsed[end + 1] is not null)
				{
					end++;
				}

				Int32 width = 0;
				for (Int32 i = index; i <= end; i++)
				{
					DefineLine define = parsed[i]!;
					if (define.Value.Length > 0 && define.Prefix.Length <= MaxPrefix)
					{
						width = Math.Max(width, define.Prefix.Length);
					}
				}

				for (Int32 i = index; i <= end; i++)
				{
					DefineLine define = parsed[i]!;
					FormatLine line = model.Lines[i];
					if (define.Value.Length == 0 || define.Prefix.Length > MaxPrefix)
					{
						builder.Append(text, line.Start, line.Next - line.Start);
						continue;
					}

					String aligned = define.Prefix + new String(' ', width - define.Prefix.Length + 1) + define.Value;
					if (!String.Equals(aligned, text.Substring(line.Start, line.Length), StringComparison.Ordinal))
					{
						changed = true;
					}

					builder.Append(aligned);
					builder.Append(text, line.End, line.EolLength);
				}

				index = end + 1;
			}

			return changed ? builder.ToString() : text;
		}

		/// <summary>Reads a line of the form <c>#define NAME value</c> (not continued on the next line, no comment instead of the value).</summary>
		private static DefineLine? Parse(FormatModel model, FormatLine line)
		{
			if (line.Verbatim || line.StartsMultiLineToken)
			{
				return null;
			}

			String text = model.GetLineText(line);
			Int32 i = 0;
			while (i < text.Length && (text[i] == ' ' || text[i] == '\t'))
			{
				i++;
			}

			if (i >= text.Length || text[i] != '#')
			{
				return null;
			}

			i++;
			while (i < text.Length && (text[i] == ' ' || text[i] == '\t'))
			{
				i++;
			}

			if (String.Compare(text, i, "define", 0, 6, StringComparison.Ordinal) != 0)
			{
				return null;
			}

			i += 6;
			if (i >= text.Length || (text[i] != ' ' && text[i] != '\t'))
			{
				return null;
			}

			while (i < text.Length && (text[i] == ' ' || text[i] == '\t'))
			{
				i++;
			}

			Int32 nameStart = i;
			while (i < text.Length && FormatLexer.IsWordChar(text[i]))
			{
				i++;
			}

			if (i == nameStart)
			{
				return null;
			}

			if (i < text.Length && text[i] == '(')
			{
				Int32 close = text.IndexOf(')', i);
				if (close < 0)
				{
					return null;
				}

				i = close + 1;
			}

			String prefix = text.Substring(0, i);
			while (i < text.Length && (text[i] == ' ' || text[i] == '\t'))
			{
				i++;
			}

			String value = text.Substring(i);
			String trimmed = value.TrimEnd();
			if (trimmed.EndsWith('\\'))
			{
				return null;
			}

			if (trimmed.Length == 0 || (trimmed[0] == '/' && trimmed.Length > 1 && (trimmed[1] == '/' || trimmed[1] == '*')))
			{
				value = String.Empty;
			}

			// The text after the name when there is no value (or only a comment) is kept as it is.
			return new DefineLine(prefix, value);
		}

		private sealed record DefineLine(String Prefix, String Value);
	}
}