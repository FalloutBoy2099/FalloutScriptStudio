namespace FalloutScriptStudio.Core.TextTools
{
	using System;
	using System.Collections.Generic;
	using System.Text;
	using Parsing;

	/// <summary>
	/// Indentation and white space operations of the editor menu: decrease indent, align to the first line, tab/space
	/// conversions and trailing white space removal. Every method returns edits in the coordinates of the given text.
	/// </summary>
	public static class WhitespaceTools
	{
		/// <summary>
		/// Removes one indentation level from the selected lines (legacy <c>Utilities.DecIndent</c>).
		/// </summary>
		/// <param name="text">The document text.</param>
		/// <param name="selection">The selection; an empty span is the caret, which selects its line.</param>
		/// <param name="tabSize">The width of a tab stop.</param>
		/// <returns>
		/// The edits. The amount removed is the distance to the previous tab stop of the first non-blank line, but never more
		/// than the smallest indentation of the selected lines, so the relative indentation is kept. A leading tab counts as
		/// one tab stop (the legacy code treated it as a single column).
		/// </returns>
		public static IReadOnlyList<TextEdit> DecreaseIndent(String text, TextSpan selection, Int32 tabSize)
		{
			ArgumentNullException.ThrowIfNull(text);

			Int32 size = Math.Max(tabSize, 1);
			var map = new TextLineMap(text);
			LineRange lines = map.GetSelectedLines(selection);

			Int32 indent = -1;
			for (Int32 line = lines.StartLine; line <= lines.EndLine; line++)
			{
				if (map.IsBlank(line))
				{
					continue;
				}

				Int32 width = GetIndentWidth(map.GetLineText(line), size);
				if (indent == -1)
				{
					Int32 adjust = width % size;
					indent = adjust > 0 ? adjust : size;
				}

				indent = Math.Min(indent, width);
			}

			var edits = new List<TextEdit>();
			if (indent <= 0)
			{
				return edits;
			}

			for (Int32 line = lines.StartLine; line <= lines.EndLine; line++)
			{
				if (map.IsBlank(line))
				{
					continue;
				}

				String lineText = map.GetLineText(line);
				Int32 chars = 0;
				Int32 columns = 0;
				while (chars < lineText.Length && columns < indent && (lineText[chars] == ' ' || lineText[chars] == '\t'))
				{
					columns += lineText[chars] == '\t' ? size - (columns % size) : 1;
					chars++;
				}

				if (chars > 0)
				{
					edits.Add(TextEdit.Delete(map.GetLineStart(line), chars));
				}
			}

			return edits;
		}

		/// <summary>
		/// Moves lines that are indented deeper than the first selected line to the indentation of that line (legacy <c>AlignToLeft</c>).
		/// </summary>
		/// <param name="text">The document text.</param>
		/// <param name="selection">The selection; nothing happens for an empty span.</param>
		/// <returns>The edits.</returns>
		public static IReadOnlyList<TextEdit> AlignToFirstLine(String text, TextSpan selection)
		{
			ArgumentNullException.ThrowIfNull(text);

			var edits = new List<TextEdit>();
			if (selection.IsEmpty)
			{
				return edits;
			}

			var map = new TextLineMap(text);
			LineRange lines = map.GetSelectedLines(selection);
			Int32 align = CountLeadingWhiteSpace(map.GetLineText(lines.StartLine));
			for (Int32 line = lines.StartLine + 1; line <= lines.EndLine; line++)
			{
				Int32 length = CountLeadingWhiteSpace(map.GetLineText(line));
				if (length == 0 || length <= align)
				{
					continue;
				}

				edits.Add(TextEdit.Delete(map.GetLineStart(line), length - align));
			}

			return edits;
		}

		/// <summary>
		/// Replaces every tab of the selection (or of the whole text) with spaces.
		/// </summary>
		/// <param name="text">The document text.</param>
		/// <param name="selection">The selection; an empty span means the whole text.</param>
		/// <param name="tabSize">The number of spaces that replace a tab.</param>
		/// <returns>The edits (none when there is no tab).</returns>
		public static IReadOnlyList<TextEdit> ConvertTabsToSpaces(String text, TextSpan selection, Int32 tabSize)
		{
			ArgumentNullException.ThrowIfNull(text);

			String spaces = new String(' ', Math.Max(tabSize, 1));
			return ReplaceInRange(text, selection, range => range.Replace("\t", spaces, StringComparison.Ordinal));
		}

		/// <summary>
		/// Replaces every run of <paramref name="tabSize"/> spaces of the selection (or of the whole text) with a tab.
		/// </summary>
		/// <param name="text">The document text.</param>
		/// <param name="selection">The selection; an empty span means the whole text.</param>
		/// <param name="tabSize">The number of spaces that make a tab.</param>
		/// <returns>The edits (none when nothing matches).</returns>
		public static IReadOnlyList<TextEdit> ConvertSpacesToTabs(String text, TextSpan selection, Int32 tabSize)
		{
			ArgumentNullException.ThrowIfNull(text);

			String spaces = new String(' ', Math.Max(tabSize, 1));
			return ReplaceInRange(text, selection, range => range.Replace(spaces, "\t", StringComparison.Ordinal));
		}

		/// <summary>
		/// Replaces the tabs of the indentation (not of the rest of the line) of the selected lines with spaces.
		/// </summary>
		/// <param name="text">The document text.</param>
		/// <param name="selection">The selection; an empty span means all lines.</param>
		/// <param name="tabSize">The number of spaces that replace a tab.</param>
		/// <returns>One edit per changed line.</returns>
		public static IReadOnlyList<TextEdit> ConvertLeadingTabsToSpaces(String text, TextSpan selection, Int32 tabSize)
		{
			ArgumentNullException.ThrowIfNull(text);

			String spaces = new String(' ', Math.Max(tabSize, 1));
			return EditLines(text, selection, line =>
			{
				Int32 length = CountLeadingWhiteSpace(line);
				if (length == 0)
				{
					return null;
				}

				String prefix = line.Substring(0, length);
				String converted = prefix.Replace("\t", spaces, StringComparison.Ordinal);
				return converted == prefix ? null : new TextEdit(0, length, converted);
			});
		}

		/// <summary>
		/// Replaces runs of <paramref name="tabSize"/> spaces in the indentation of the selected lines with tabs.
		/// </summary>
		/// <param name="text">The document text.</param>
		/// <param name="selection">The selection; an empty span means all lines.</param>
		/// <param name="tabSize">The number of spaces that make a tab.</param>
		/// <returns>One edit per changed line.</returns>
		public static IReadOnlyList<TextEdit> ConvertLeadingSpacesToTabs(String text, TextSpan selection, Int32 tabSize)
		{
			ArgumentNullException.ThrowIfNull(text);

			Int32 size = Math.Max(tabSize, 1);
			return EditLines(text, selection, line =>
			{
				Int32 length = CountLeadingSpacesAndTabs(line);
				if (length == 0)
				{
					return null;
				}

				String converted = LeadingWhiteSpaceToTabs(line.Substring(0, length), size);
				return converted == line.Substring(0, length) ? null : new TextEdit(0, length, converted);
			});
		}

		/// <summary>
		/// Removes the white space at the end of the selected lines (legacy <c>RemoveTrailingWS</c>, which also skipped the
		/// last line).
		/// </summary>
		/// <param name="text">The document text.</param>
		/// <param name="selection">The selection; an empty span means all lines.</param>
		/// <returns>One edit per changed line.</returns>
		public static IReadOnlyList<TextEdit> TrimTrailingWhitespace(String text, TextSpan selection)
		{
			ArgumentNullException.ThrowIfNull(text);

			return EditLines(text, selection, line =>
			{
				Int32 end = line.Length;
				while (end > 0 && Char.IsWhiteSpace(line[end - 1]))
				{
					end--;
				}

				return end == line.Length ? null : TextEdit.Delete(end, line.Length - end);
			});
		}

		/// <summary>Removes the white space at the end of every line.</summary>
		/// <param name="text">The document text.</param>
		/// <returns>The trimmed text.</returns>
		public static String TrimTrailingWhitespace(String text)
		{
			return TextEditApplier.Apply(text, TrimTrailingWhitespace(text, default));
		}

		private static Int32 GetIndentWidth(String line, Int32 tabSize)
		{
			Int32 columns = 0;
			foreach (Char c in line)
			{
				if (c == ' ')
				{
					columns++;
				}
				else if (c == '\t')
				{
					columns += tabSize - (columns % tabSize);
				}
				else
				{
					break;
				}
			}

			return columns;
		}

		private static Int32 CountLeadingWhiteSpace(String line)
		{
			Int32 count = 0;
			while (count < line.Length && Char.IsWhiteSpace(line[count]))
			{
				count++;
			}

			return count;
		}

		private static Int32 CountLeadingSpacesAndTabs(String line)
		{
			Int32 count = 0;
			while (count < line.Length && (line[count] == ' ' || line[count] == '\t'))
			{
				count++;
			}

			return count;
		}

		/// <summary>Converts the leading white space to tabs (the editor library's <c>LeadingWhiteSpaceToTabs</c>).</summary>
		private static String LeadingWhiteSpaceToTabs(String prefix, Int32 tabSize)
		{
			var result = new StringBuilder(prefix.Length);
			Int32 consecutiveSpaces = 0;
			foreach (Char c in prefix)
			{
				if (c == ' ')
				{
					consecutiveSpaces++;
					if (consecutiveSpaces == tabSize)
					{
						result.Append('\t');
						consecutiveSpaces = 0;
					}
				}
				else
				{
					result.Append('\t');
					consecutiveSpaces = 0;
				}
			}

			result.Append(' ', consecutiveSpaces);
			return result.ToString();
		}

		private static TextEdit[] ReplaceInRange(String text, TextSpan selection, Func<String, String> convert)
		{
			TextSpan range = selection.IsEmpty ? new TextSpan(0, text.Length) : selection;
			String original = text.Substring(range.Offset, range.Length);
			TextEdit? diff = TextEditApplier.CreateDiff(original, convert(original));
			if (diff is null)
			{
				return Array.Empty<TextEdit>();
			}

			return new[] { diff with { Offset = diff.Offset + range.Offset } };
		}

		private static List<TextEdit> EditLines(String text, TextSpan selection, Func<String, TextEdit?> editLine)
		{
			var map = new TextLineMap(text);
			LineRange lines = selection.IsEmpty ? new LineRange(1, map.LineCount) : map.GetSelectedLines(selection);
			var edits = new List<TextEdit>();
			for (Int32 line = lines.StartLine; line <= lines.EndLine; line++)
			{
				TextEdit? edit = editLine(map.GetLineText(line));
				if (edit is not null)
				{
					edits.Add(edit with { Offset = edit.Offset + map.GetLineStart(line) });
				}
			}

			return edits;
		}
	}
}