namespace FalloutScriptStudio.Core.Formatting
{
	using System;
	using System.Collections.Generic;
	using System.Text;

	/// <summary>Writes the differences between two texts as a unified diff (<c>@@ -1,3 +1,4 @@</c>, then lines that start with a space, <c>-</c> or <c>+</c>), the form every programmer reads.</summary>
	public static class UnifiedDiff
	{
		/// <summary>Creates the diff.</summary>
		/// <param name="oldText">The original text.</param>
		/// <param name="newText">The new text.</param>
		/// <param name="context">The number of unchanged lines shown around each change.</param>
		/// <returns>The diff, with the line breaks of <paramref name="newText"/> (<c>\n</c> when it has none); empty when the texts are equal.</returns>
		public static String Create(String oldText, String newText, Int32 context = 3)
		{
			ArgumentNullException.ThrowIfNull(oldText);
			ArgumentNullException.ThrowIfNull(newText);

			IReadOnlyList<LineChange> changes = LineDiff.GetChanges(oldText, newText);
			if (changes.Count == 0)
			{
				return String.Empty;
			}

			String[] oldLines = SplitLines(oldText);
			String[] newLines = SplitLines(newText);
			String newLine = oldText.Contains("\r\n", StringComparison.Ordinal) || newText.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
			Int32 margin = Math.Max(context, 0);
			StringBuilder builder = new StringBuilder();
			Int32 index = 0;
			while (index < changes.Count)
			{
				// A group of changes whose context overlaps is shown as one hunk.
				Int32 last = index;
				while (last + 1 < changes.Count && changes[last + 1].OldStart - (changes[last].OldStart + changes[last].OldCount) <= 2 * margin)
				{
					last++;
				}

				LineChange first = changes[index];
				LineChange end = changes[last];
				Int32 oldFrom = Math.Max(first.OldStart - margin, 0);
				Int32 oldTo = Math.Min(end.OldStart + end.OldCount + margin, oldLines.Length);
				Int32 newFrom = first.NewStart - (first.OldStart - oldFrom);
				Int32 newTo = end.NewStart + end.NewCount + (oldTo - (end.OldStart + end.OldCount));

				builder.Append("@@ -").Append(oldFrom + 1).Append(',').Append(oldTo - oldFrom)
					.Append(" +").Append(newFrom + 1).Append(',').Append(newTo - newFrom).Append(" @@").Append(newLine);

				Int32 oldPosition = oldFrom;
				for (Int32 i = index; i <= last; i++)
				{
					LineChange change = changes[i];
					for (; oldPosition < change.OldStart; oldPosition++)
					{
						builder.Append(' ').Append(oldLines[oldPosition]).Append(newLine);
					}

					for (Int32 k = 0; k < change.OldCount; k++)
					{
						builder.Append('-').Append(oldLines[change.OldStart + k]).Append(newLine);
					}

					for (Int32 k = 0; k < change.NewCount; k++)
					{
						builder.Append('+').Append(newLines[change.NewStart + k]).Append(newLine);
					}

					oldPosition = change.OldStart + change.OldCount;
				}

				for (; oldPosition < oldTo; oldPosition++)
				{
					builder.Append(' ').Append(oldLines[oldPosition]).Append(newLine);
				}

				index = last + 1;
			}

			return builder.ToString();
		}

		private static String[] SplitLines(String text)
		{
			List<String> lines = [];
			Int32 start = 0;
			for (Int32 i = 0; i < text.Length; i++)
			{
				if (text[i] == '\n' || (text[i] == '\r' && (i + 1 >= text.Length || text[i + 1] != '\n')))
				{
					lines.Add(text.Substring(start, i - start).TrimEnd('\r'));
					start = i + 1;
				}
			}

			if (start < text.Length)
			{
				lines.Add(text.Substring(start));
			}

			return lines.ToArray();
		}
	}
}