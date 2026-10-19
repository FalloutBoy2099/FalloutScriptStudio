namespace FalloutScriptStudio.Core.Dialogue
{
	using System;

	/// <summary>
	/// Blanks what is not code in a script: comments, the inside of string literals and preprocessor directives. Every other character
	/// stays where it is (line breaks included), so offsets, lines and columns of the result are those of the script.
	/// </summary>
	internal static class ScriptMessageMasker
	{
		/// <summary>Masks a script.</summary>
		/// <param name="text">The text.</param>
		/// <param name="maskDirectives">Whether the lines that start with <c>#</c> (and their continuation lines) are blanked too.</param>
		/// <returns>The masked text, as long as <paramref name="text"/>.</returns>
		public static String Mask(String text, Boolean maskDirectives = true)
		{
			Char[] chars = text.ToCharArray();
			Boolean lineStart = true;
			Int32 i = 0;
			while (i < chars.Length)
			{
				Char c = chars[i];
				if (c == '\n' || c == '\r')
				{
					lineStart = true;
					i++;
				}
				else if (Char.IsWhiteSpace(c))
				{
					i++;
				}
				else if (lineStart && maskDirectives && c == '#')
				{
					i = MaskDirective(chars, i);
				}
				else if (c == '/' && i + 1 < chars.Length && chars[i + 1] == '/')
				{
					i = MaskUntilLineEnd(chars, i);
					lineStart = false;
				}
				else if (c == '/' && i + 1 < chars.Length && chars[i + 1] == '*')
				{
					i = MaskBlockComment(chars, i);
					lineStart = false;
				}
				else if (c == '"')
				{
					i = MaskString(chars, i);
					lineStart = false;
				}
				else
				{
					lineStart = false;
					i++;
				}
			}

			return new String(chars);
		}

		private static Int32 MaskDirective(Char[] chars, Int32 start)
		{
			Int32 i = start;
			while (i < chars.Length)
			{
				Int32 end = i;
				while (end < chars.Length && chars[end] != '\n' && chars[end] != '\r')
				{
					end++;
				}

				Int32 last = end - 1;
				Boolean continues = last >= i && chars[last] == '\\';
				Blank(chars, i, end);
				i = end;
				if (!continues)
				{
					break;
				}

				// The next physical line belongs to the directive.
				if (i < chars.Length && chars[i] == '\r')
				{
					i++;
				}

				if (i < chars.Length && chars[i] == '\n')
				{
					i++;
				}
			}

			return i;
		}

		private static Int32 MaskUntilLineEnd(Char[] chars, Int32 start)
		{
			Int32 end = start;
			while (end < chars.Length && chars[end] != '\n' && chars[end] != '\r')
			{
				end++;
			}

			Blank(chars, start, end);
			return end;
		}

		private static Int32 MaskBlockComment(Char[] chars, Int32 start)
		{
			Int32 end = start + 2;
			while (end < chars.Length && !(chars[end] == '*' && end + 1 < chars.Length && chars[end + 1] == '/'))
			{
				end++;
			}

			end = Math.Min(chars.Length, end + 2);
			Blank(chars, start, end);
			return end;
		}

		private static Int32 MaskString(Char[] chars, Int32 start)
		{
			Int32 end = start + 1;
			while (end < chars.Length && chars[end] != '"' && chars[end] != '\n' && chars[end] != '\r')
			{
				end++;
			}

			Blank(chars, start + 1, end);
			return end < chars.Length && chars[end] == '"' ? end + 1 : end;
		}

		private static void Blank(Char[] chars, Int32 start, Int32 end)
		{
			for (Int32 i = start; i < end; i++)
			{
				if (chars[i] != '\n' && chars[i] != '\r')
				{
					chars[i] = ' ';
				}
			}
		}
	}
}