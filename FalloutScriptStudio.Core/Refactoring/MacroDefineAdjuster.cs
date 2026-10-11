namespace FalloutScriptStudio.Core.Refactoring
{
	using System;
	using System.Text.RegularExpressions;
	using FalloutScriptStudio.Core.TextTools;

	/// <summary>
	/// Keeps the value of a <c>#define</c> aligned when its name changes length (legacy <c>DefineMacroAdjustSpaces</c>).
	/// </summary>
	public static class MacroDefineAdjuster
	{
		/// <summary>
		/// Creates the edit that compensates the length change of a renamed macro name on its definition line.
		/// </summary>
		/// <param name="text">The text of the file that holds the definition, before the rename.</param>
		/// <param name="token">The macro name before the rename.</param>
		/// <param name="declaredLine">The 1-based line of the <c>#define</c>.</param>
		/// <param name="lengthDiff">The new name length minus the old one.</param>
		/// <returns>
		/// When the name gets longer, up to <paramref name="lengthDiff"/> spaces (but never the last one) are removed after the
		/// name and its parameter list; when it gets shorter the same number of spaces is inserted. <c>null</c> when the line is
		/// not a definition of the macro, has no value, or needs no change.
		/// </returns>
		public static TextEdit? CreateEdit(String text, String token, Int32 declaredLine, Int32 lengthDiff)
		{
			ArgumentNullException.ThrowIfNull(text);
			ArgumentNullException.ThrowIfNull(token);

			if (lengthDiff == 0)
			{
				return null;
			}

			var map = new TextLineMap(text);
			if (declaredLine < 1 || declaredLine > map.LineCount)
			{
				return null;
			}

			String line = map.GetLineText(declaredLine);
			Match match = Regex.Match(line, @"^\s*#\s*define\s+" + Regex.Escape(token) + @"(?![\p{L}\p{N}_])", RegexOptions.CultureInvariant);
			if (!match.Success)
			{
				return null;
			}

			Int32 nameEnd = match.Length;
			if (nameEnd < line.Length && line[nameEnd] == '(')
			{
				Int32 close = line.IndexOf(')', nameEnd);
				if (close < 0)
				{
					return null;
				}

				nameEnd = close + 1;
			}

			Int32 white = 0;
			while (nameEnd + white < line.Length && (line[nameEnd + white] == ' ' || line[nameEnd + white] == '\t'))
			{
				white++;
			}

			if (nameEnd + white >= line.Length)
			{
				return null;
			}

			Int32 offset = map.GetLineStart(declaredLine) + nameEnd;
			if (lengthDiff > 0)
			{
				Int32 remove = Math.Min(lengthDiff, white - 1);
				return remove > 0 ? TextEdit.Delete(offset, remove) : null;
			}

			return TextEdit.Insert(offset, new String(' ', -lengthDiff));
		}
	}
}
