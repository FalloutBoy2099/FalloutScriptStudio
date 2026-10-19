namespace FalloutScriptStudio.Core.TextTools
{
	using System;

	/// <summary>Finds the comment lines that document a procedure (the comment block directly above it).</summary>
	public static class ProcedureComments
	{
		/// <summary>
		/// Finds the first line of the comment block that sits directly above a line (no blank line between). The block is made of
		/// <c>//</c> lines and <c>/* ... */</c> blocks; a line made only of a decoration (<c>// ======</c>, <c>/* ===== bundled ===== */</c>)
		/// separates sections of the file and is not part of the documentation of the procedure.
		/// </summary>
		/// <param name="map">The line index of the text.</param>
		/// <param name="line">The 1-based line of the <c>procedure</c> keyword.</param>
		/// <returns>The first line of the comment block, or <paramref name="line"/> when there is none.</returns>
		public static Int32 FindLeadingCommentStartLine(TextLineMap map, Int32 line)
		{
			ArgumentNullException.ThrowIfNull(map);

			Int32 first = line;
			Int32 current = line - 1;
			while (current >= 1)
			{
				String trimmed = map.GetLineText(current).Trim();
				if (trimmed.Length == 0)
				{
					break;
				}

				if (trimmed.StartsWith("//", StringComparison.Ordinal))
				{
					if (IsDecoration(trimmed))
					{
						break;
					}

					first = current;
					current--;
					continue;
				}

				if (trimmed.EndsWith("*/", StringComparison.Ordinal))
				{
					Int32 start = current;
					while (start >= 1 && !map.GetLineText(start).TrimStart().StartsWith("/*", StringComparison.Ordinal))
					{
						if (start < current && map.GetLineText(start).Contains("*/", StringComparison.Ordinal))
						{
							// Another comment ends above: this one does not start in this block.
							start = 0;
							break;
						}

						start--;
					}

					if (start < 1 || IsDecoration(map.GetLinesText(start, current)))
					{
						break;
					}

					first = start;
					current = start - 1;
					continue;
				}

				break;
			}

			return first;
		}

		private static Boolean IsDecoration(String comment)
		{
			Int32 letters = 0;
			foreach (Char c in comment)
			{
				if (Char.IsLetterOrDigit(c))
				{
					letters++;
				}
			}

			if (comment.Contains("====", StringComparison.Ordinal) || comment.Contains("----", StringComparison.Ordinal)
				|| comment.Contains("****", StringComparison.Ordinal) || comment.Contains("####", StringComparison.Ordinal))
			{
				return true;
			}

			return letters == 0 && comment.Length > 6;
		}
	}
}