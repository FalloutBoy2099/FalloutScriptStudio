namespace FalloutScriptStudio.Editor.Text
{
	using System;

	/// <summary>
	/// Pure indentation rules of the SSL language: <c>begin</c> / <c>then</c> / <c>else</c> / <c>do</c> open an indented
	/// block and <c>end</c> closes it.
	/// </summary>
	public static class SslIndentationRules
	{
		/// <summary>Gets the leading white space of a line.</summary>
		/// <param name="line">The line text.</param>
		/// <returns>The spaces and tabs the line starts with.</returns>
		public static String GetLeadingWhitespace(String line)
		{
			Int32 i = 0;
			while (i < line.Length && (line[i] == ' ' || line[i] == '\t'))
			{
				i++;
			}

			return line.Substring(0, i);
		}

		/// <summary>
		/// Reduces a line to its code: removes a trailing <c>//</c> comment (outside strings), trims it and lower-cases it.
		/// </summary>
		/// <param name="line">The line text.</param>
		/// <returns>The normalized code of the line.</returns>
		public static String GetCode(String line)
		{
			Boolean inString = false;
			Int32 end = line.Length;
			for (Int32 i = 0; i < line.Length; i++)
			{
				Char c = line[i];
				if (inString)
				{
					if (c == '\\')
					{
						i++;
					}
					else if (c == '"')
					{
						inString = false;
					}
				}
				else if (c == '"')
				{
					inString = true;
				}
				else if (c == '/' && i + 1 < line.Length && line[i + 1] == '/')
				{
					end = i;
					break;
				}
			}

			return line.Substring(0, end).Trim().ToLowerInvariant();
		}

		/// <summary>Determines whether normalized code starts with a word.</summary>
		/// <param name="code">Code from <see cref="GetCode"/>.</param>
		/// <param name="word">The lower-case word.</param>
		/// <returns><see langword="true"/> when the first word of the code is <paramref name="word"/>.</returns>
		public static Boolean StartsWithWord(String code, String word)
		{
			return code.StartsWith(word, StringComparison.Ordinal)
				&& (code.Length == word.Length || !WordFinder.IsWordChar(code[word.Length]));
		}

		/// <summary>Determines whether normalized code ends with a word.</summary>
		/// <param name="code">Code from <see cref="GetCode"/>.</param>
		/// <param name="word">The lower-case word.</param>
		/// <returns><see langword="true"/> when the last word of the code is <paramref name="word"/>.</returns>
		public static Boolean EndsWithWord(String code, String word)
		{
			return code.EndsWith(word, StringComparison.Ordinal)
				&& (code.Length == word.Length || !WordFinder.IsWordChar(code[code.Length - word.Length - 1]));
		}

		/// <summary>Determines whether the lines that follow a line must be indented deeper.</summary>
		/// <param name="line">The line text.</param>
		/// <returns><see langword="true"/> for a line that ends with <c>begin</c>, <c>then</c>, <c>else</c> or <c>do</c>.</returns>
		public static Boolean OpensBlock(String line)
		{
			String code = GetCode(line);
			if (EndsWithWord(code, "begin") || EndsWithWord(code, "then") || EndsWithWord(code, "do"))
			{
				return true;
			}

			// "end else" is followed by a "begin" on its own line, which aligns with the "end".
			return EndsWithWord(code, "else") && !StartsWithWord(code, "end");
		}

		/// <summary>Gets the indentation of the line that follows <paramref name="previousLine"/>.</summary>
		/// <param name="previousLine">The text of the previous line.</param>
		/// <param name="unit">One indentation level (a tab or spaces).</param>
		/// <returns>The white space to put in front of the new line.</returns>
		public static String GetNextLineIndentation(String previousLine, String unit)
		{
			String indentation = GetLeadingWhitespace(previousLine);
			if (previousLine.Trim().Length > 0 && OpensBlock(previousLine))
			{
				indentation += unit;
			}

			return indentation;
		}

		/// <summary>
		/// Finds the indentation of the <c>begin</c> line a closing <c>end</c> belongs to.
		/// </summary>
		/// <param name="getLine">Returns the text of a line by its zero-based index.</param>
		/// <param name="lineIndex">Zero-based index of the line that contains the <c>end</c>.</param>
		/// <returns>The indentation of the opening line, or <see langword="null"/> when none was found.</returns>
		public static String? FindBlockOpenerIndentation(Func<Int32, String> getLine, Int32 lineIndex)
		{
			Int32 depth = 1;
			for (Int32 i = lineIndex - 1; i >= 0; i--)
			{
				String text = getLine(i);
				String code = GetCode(text);
				if (code.Length == 0)
				{
					continue;
				}

				if (EndsWithWord(code, "begin") && --depth == 0)
				{
					return GetLeadingWhitespace(text);
				}

				if (StartsWithWord(code, "end"))
				{
					depth++;
				}
			}

			return null;
		}
	}
}
