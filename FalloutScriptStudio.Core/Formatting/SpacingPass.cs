namespace FalloutScriptStudio.Core.Formatting
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// Puts spaces around operators and after commas (the behaviour of the first versions of the formatter, kept character for character),
	/// or removes them. Strings and comments are never touched.
	/// </summary>
	internal static class SpacingPass
	{
		private static readonly String[] _patterns = { ":=", "!=", "==", ">=", "<=", "+=", "-=", "*=", "/=", "%=", ",", ">", "<", "+", "-", "*", "/", "%" };
		private static readonly Char[] _excludeRight = { ' ', '=', '+', '-', '*', '/' };
		private static readonly Char[] _excludeLeft = { ' ', '=', '>', '<', '+', '-', '!', ':', '*', '/', '%' };
		private static readonly Char[] _excludeBeforeNegativeNumber = { ' ', ',', '(' };

		private static readonly HashSet<String> _assignments = new(StringComparer.Ordinal) { ":=", "+=", "-=", "*=", "/=", "%=" };
		private static readonly HashSet<String> _binaryOperators = new(StringComparer.Ordinal) { "!=", "==", ">=", "<=", ">", "<", "+", "-", "*", "/", "%", "&&", "||", "<<", ">>" };
		private static readonly String[] _fusing = { "++", "--", "+=", "-=", "*=", "/=", "%=", "==", "!=", "<=", ">=", "<<", ">>", "&&", "||", ":=", "//", "/*", "*/" };

		private static readonly HashSet<String> _expectsOperand = new(StringComparer.OrdinalIgnoreCase)
		{
			"return", "if", "while", "then", "else", "do", "not", "and", "or", "bwand", "bwor", "bwxor", "bwnot", "in", "call", "case", "begin", "end", "for", "foreach", "switch", "default", "variable", "import", "export",
		};

		private static readonly HashSet<String> _formattedDirectives = new(StringComparer.OrdinalIgnoreCase) { "define", "if", "elif" };

		/// <summary>Applies the spacing rules of the options to a text.</summary>
		/// <param name="text">The text.</param>
		/// <param name="options">The options.</param>
		/// <returns>The text with the spacing applied.</returns>
		public static String Apply(String text, FormatOptions options)
		{
			String result = text;
			if (options.OperatorSpacing == SpacingStyle.Add || options.AssignmentSpacing == SpacingStyle.Add || options.CommaSpacing == SpacingStyle.Add)
			{
				result = AddSpaces(result, options);
			}

			if (options.OperatorSpacing == SpacingStyle.Remove || options.AssignmentSpacing == SpacingStyle.Remove || options.CommaSpacing == SpacingStyle.Remove)
			{
				result = RemoveSpaces(result, options);
			}

			return result;
		}

		private static String AddSpaces(String text, FormatOptions options)
		{
			List<String> patterns = new(_patterns.Length);
			foreach (String pattern in _patterns)
			{
				SpacingStyle style = pattern == "," ? options.CommaSpacing : _assignments.Contains(pattern) ? options.AssignmentSpacing : options.OperatorSpacing;
				if (style == SpacingStyle.Add)
				{
					patterns.Add(pattern);
				}
			}

			String[] lines = text.Split('\n');
			Boolean inBlockComment = false;
			Boolean inDirective = false;
			Boolean directiveSkipped = false;
			for (Int32 i = 0; i < lines.Length; i++)
			{
				Boolean startsInBlock = inBlockComment;
				String trimmed = lines[i].TrimStart();
				FindProtectedSpans(lines[i], startsInBlock, out inBlockComment);

				Boolean skip = trimmed.Length < 3 || (trimmed[0] == '/' && (trimmed[1] == '/' || trimmed[1] == '*'));
				if (!startsInBlock)
				{
					if (!inDirective && trimmed.Length > 0 && trimmed[0] == '#')
					{
						inDirective = true;
						directiveSkipped = !options.FormatPreprocessorLines || !_formattedDirectives.Contains(GetDirectiveName(trimmed));
					}

					if (inDirective)
					{
						skip |= directiveSkipped;
						inDirective = EndsWithBackslash(lines[i]);
					}
				}

				if (skip)
				{
					continue;
				}

				lines[i] = FormatLine(lines[i], startsInBlock, patterns);
			}

			return String.Join("\n", lines);
		}

		private static String GetDirectiveName(String trimmedLine)
		{
			Int32 i = 1;
			while (i < trimmedLine.Length && (trimmedLine[i] == ' ' || trimmedLine[i] == '\t'))
			{
				i++;
			}

			Int32 start = i;
			while (i < trimmedLine.Length && Char.IsLetter(trimmedLine[i]))
			{
				i++;
			}

			return trimmedLine.Substring(start, i - start);
		}

		private static Boolean EndsWithBackslash(String line)
		{
			Int32 end = line.Length;
			while (end > 0 && (line[end - 1] == '\r' || line[end - 1] == ' ' || line[end - 1] == '\t'))
			{
				end--;
			}

			return end > 0 && line[end - 1] == '\\';
		}

		private static String FormatLine(String source, Boolean startsInBlock, List<String> patterns)
		{
			String line = source;
			foreach (String pattern in patterns)
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
		internal static List<(Int32 Start, Int32 End)> FindProtectedSpans(String line, Boolean startsInBlock, out Boolean endsInBlock)
		{
			List<(Int32, Int32)> spans = [];
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

		private static String RemoveSpaces(String text, FormatOptions options)
		{
			FormatModel model = FormatModel.Build(text);
			List<(Int32 Start, Int32 Length)> removals = [];
			List<FormatToken> tokens = model.Tokens;
			for (Int32 t = 0; t < tokens.Count; t++)
			{
				FormatToken token = tokens[t];
				if (token.Kind != FormatTokenKind.Symbol)
				{
					continue;
				}

				String symbol = token.GetText(text);
				if (symbol == ",")
				{
					if (options.CommaSpacing == SpacingStyle.Remove)
					{
						RemoveAround(text, tokens, t, removals, true, true, symbol);
					}

					continue;
				}

				if (_assignments.Contains(symbol))
				{
					if (options.AssignmentSpacing == SpacingStyle.Remove)
					{
						RemoveAround(text, tokens, t, removals, true, true, symbol);
					}

					continue;
				}

				if (options.OperatorSpacing == SpacingStyle.Remove && _binaryOperators.Contains(symbol) && IsBinary(text, tokens, t))
				{
					RemoveAround(text, tokens, t, removals, true, true, symbol);
				}
			}

			return Apply(text, removals);
		}

		private static void RemoveAround(String text, List<FormatToken> tokens, Int32 index, List<(Int32 Start, Int32 Length)> removals, Boolean before, Boolean after, String symbol)
		{
			if (after && index + 2 < tokens.Count && tokens[index + 1].Kind == FormatTokenKind.Space)
			{
				FormatToken next = tokens[index + 2];
				if (next.Kind is not FormatTokenKind.Newline and not FormatTokenKind.LineComment and not FormatTokenKind.BlockComment && !Fuses(symbol, text, next))
				{
					removals.Add((tokens[index + 1].Start, tokens[index + 1].Length));
				}
			}

			if (before && index >= 2 && tokens[index - 1].Kind == FormatTokenKind.Space)
			{
				FormatToken previous = tokens[index - 2];
				if (previous.Kind is not FormatTokenKind.Newline and not FormatTokenKind.LineComment and not FormatTokenKind.BlockComment && !FusesBefore(symbol, text, previous))
				{
					removals.Add((tokens[index - 1].Start, tokens[index - 1].Length));
				}
			}
		}

		private static Boolean Fuses(String symbol, String text, FormatToken next)
		{
			String pair = new String(new[] { symbol[symbol.Length - 1], text[next.Start] });
			return Array.IndexOf(_fusing, pair) >= 0;
		}

		private static Boolean FusesBefore(String symbol, String text, FormatToken previous)
		{
			String pair = new String(new[] { text[previous.End - 1], symbol[0] });
			return Array.IndexOf(_fusing, pair) >= 0;
		}

		private static Boolean IsBinary(String text, List<FormatToken> tokens, Int32 index)
		{
			Int32 t = index - 1;
			while (t >= 0 && tokens[t].Kind == FormatTokenKind.Space)
			{
				t--;
			}

			if (t < 0)
			{
				return false;
			}

			FormatToken previous = tokens[t];
			switch (previous.Kind)
			{
				case FormatTokenKind.Number:
				case FormatTokenKind.String:
					return true;
				case FormatTokenKind.Word:
					return !_expectsOperand.Contains(previous.GetText(text));
				case FormatTokenKind.Symbol:
					Char last = text[previous.End - 1];
					return last == ')' || last == ']' || last == '}';
				default:
					return false;
			}
		}

		private static String Apply(String text, List<(Int32 Start, Int32 Length)> removals)
		{
			if (removals.Count == 0)
			{
				return text;
			}

			removals.Sort((a, b) => a.Start.CompareTo(b.Start));
			System.Text.StringBuilder builder = new System.Text.StringBuilder(text.Length);
			Int32 position = 0;
			foreach ((Int32 start, Int32 length) in removals)
			{
				if (start < position)
				{
					continue;
				}

				builder.Append(text, position, start - position);
				position = start + length;
			}

			builder.Append(text, position, text.Length - position);
			return builder.ToString();
		}
	}
}