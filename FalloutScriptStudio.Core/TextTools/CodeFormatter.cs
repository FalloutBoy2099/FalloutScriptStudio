namespace FalloutScriptStudio.Core.TextTools
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// Default <see cref="ICodeFormatter"/>. Operators (<c>:= != == &gt;= &lt;= += -= *= /= %= , &gt; &lt; + - * / %</c>) get a
	/// space on each side (commas only on the right); unary minus, <c>++</c>/<c>--</c>, strings and comments are left alone.
	/// </summary>
	/// <remarks>
	/// Differences from the legacy formatter: comments (<c>//</c> tails and <c>/* */</c> blocks, also across lines) are not
	/// touched, the string positions are recomputed after every inserted space, and no exception is thrown for an operator at the
	/// very start or end of a line.
	/// </remarks>
	public sealed class CodeFormatter : ICodeFormatter
	{
		private static readonly String[] _patterns = { ":=", "!=", "==", ">=", "<=", "+=", "-=", "*=", "/=", "%=", ",", ">", "<", "+", "-", "*", "/", "%" };
		private static readonly Char[] _excludeRight = { ' ', '=', '+', '-', '*', '/' };
		private static readonly Char[] _excludeLeft = { ' ', '=', '>', '<', '+', '-', '!', ':', '*', '/', '%' };
		private static readonly Char[] _excludeBeforeNegativeNumber = { ' ', ',', '(' };

		/// <inheritdoc />
		public String Format(String text)
		{
			ArgumentNullException.ThrowIfNull(text);

			String[] lines = text.Split('\n');
			Boolean inBlockComment = false;
			for (Int32 i = 0; i < lines.Length; i++)
			{
				Boolean startsInBlock = inBlockComment;
				String trimmed = lines[i].TrimStart();
				FindProtectedSpans(lines[i], startsInBlock, out inBlockComment);
				if (trimmed.Length < 3 || (trimmed[0] == '/' && (trimmed[1] == '/' || trimmed[1] == '*')))
				{
					continue;
				}

				lines[i] = FormatLine(lines[i], startsInBlock);
			}

			return String.Join("\n", lines);
		}

		/// <inheritdoc />
		public TextEdit? FormatSelectionOrLine(String text, TextSpan selection)
		{
			ArgumentNullException.ThrowIfNull(text);

			Int32 offset;
			Int32 length;
			if (selection.IsEmpty)
			{
				var map = new TextLineMap(text);
				Int32 line = map.GetLineFromOffset(selection.Offset);
				offset = map.GetLineStart(line);
				length = map.GetLineLength(line);
			}
			else
			{
				offset = selection.Offset;
				length = selection.Length;
			}

			String original = text.Substring(offset, length);
			String formatted = this.Format(original);
			return String.Equals(original, formatted, StringComparison.Ordinal) ? null : new TextEdit(offset, length, formatted);
		}

		/// <inheritdoc />
		public SmartFormatResult? FormatSmart(String text, Int32 caretOffset)
		{
			ArgumentNullException.ThrowIfNull(text);

			var map = new TextLineMap(text);
			Int32 line = map.GetLineFromOffset(caretOffset);
			Int32 lineStart = map.GetLineStart(line);
			Int32 column = Math.Min(Math.Max(caretOffset - lineStart, 0), map.GetLineLength(line));

			String before = text.Substring(lineStart, column);
			String formatted = this.Format(before);
			Int32 growth = formatted.Length - before.Length;
			if (growth <= 0)
			{
				return null;
			}

			return new SmartFormatResult(new TextEdit(lineStart, before.Length, formatted), caretOffset + growth);
		}

		private static String FormatLine(String source, Boolean startsInBlock)
		{
			String line = source;
			foreach (String pattern in _patterns)
			{
				Int32 n = 0;
				do
				{
					n = line.IndexOf(pattern, n, StringComparison.Ordinal);
					if (n < 0)
					{
						break;
					}

					List<(Int32 Start, Int32 End)> spans = FindProtectedSpans(line, startsInBlock, out _);
					Int32 protectedEnd = GetProtectedEnd(spans, n);
					if (protectedEnd >= 0)
					{
						n = protectedEnd;
						continue;
					}

					Int32 after = n + pattern.Length;
					if (line.Length > after && !Char.IsWhiteSpace(line, after))
					{
						if (pattern.Length == 2)
						{
							line = line.Insert(after, " ");
						}
						else if (Array.IndexOf(_excludeRight, line[after]) < 0)
						{
							Boolean negativeNumber = pattern == "-" && Char.IsDigit(CharAt(line, n + 1))
								&& (n == 0 || Array.IndexOf(_excludeBeforeNegativeNumber, line[n - 1]) >= 0);
							Boolean doubled = (pattern == "+" || pattern == "-") && CharAt(line, n - 1) == pattern[0];
							if (!negativeNumber && !doubled)
							{
								line = line.Insert(n + 1, " ");
							}
						}
					}

					if (n > 0 && pattern != "," && !Char.IsWhiteSpace(line, n - 1))
					{
						if (pattern.Length == 2)
						{
							line = line.Insert(n, " ");
						}
						else if (Array.IndexOf(_excludeLeft, line[n - 1]) < 0)
						{
							Boolean negativeNumber = pattern == "-" && Char.IsDigit(CharAt(line, n + 1)) && line[n - 1] == '(';
							Boolean doubled = (pattern == "+" || pattern == "-") && CharAt(line, n + 1) == pattern[0];
							if (!negativeNumber && !doubled)
							{
								line = line.Insert(n, " ");
							}
						}
					}

					n += pattern.Length;
				}
				while (n < line.Length);
			}

			return line;
		}

		private static Char CharAt(String text, Int32 index)
		{
			return index >= 0 && index < text.Length ? text[index] : '\0';
		}

		private static Int32 GetProtectedEnd(List<(Int32 Start, Int32 End)> spans, Int32 index)
		{
			foreach ((Int32 start, Int32 end) in spans)
			{
				if (index >= start && index < end)
				{
					return end;
				}
			}

			return -1;
		}

		/// <summary>Finds the parts of a line that must not be formatted: strings and comments.</summary>
		private static List<(Int32 Start, Int32 End)> FindProtectedSpans(String line, Boolean startsInBlock, out Boolean endsInBlock)
		{
			var spans = new List<(Int32, Int32)>();
			endsInBlock = false;
			Int32 i = 0;
			if (startsInBlock)
			{
				Int32 close = line.IndexOf("*/", StringComparison.Ordinal);
				if (close < 0)
				{
					spans.Add((0, line.Length));
					endsInBlock = true;
					return spans;
				}

				spans.Add((0, close + 2));
				i = close + 2;
			}

			while (i < line.Length)
			{
				Char c = line[i];
				if (c == '"')
				{
					Int32 close = line.IndexOf('"', i + 1);
					if (close < 0)
					{
						i++;
						continue;
					}

					spans.Add((i, close + 1));
					i = close + 1;
				}
				else if (c == '/' && CharAt(line, i + 1) == '/')
				{
					spans.Add((i, line.Length));
					i = line.Length;
				}
				else if (c == '/' && CharAt(line, i + 1) == '*')
				{
					Int32 close = line.IndexOf("*/", i + 2, StringComparison.Ordinal);
					if (close < 0)
					{
						spans.Add((i, line.Length));
						endsInBlock = true;
						i = line.Length;
					}
					else
					{
						spans.Add((i, close + 2));
						i = close + 2;
					}
				}
				else
				{
					i++;
				}
			}

			return spans;
		}
	}
}
