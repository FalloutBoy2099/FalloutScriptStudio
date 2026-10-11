namespace FalloutScriptStudio.Core.Messages
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using FalloutScriptStudio.Core.Compilation;

	/// <summary>
	/// <see cref="IMessageStructureChecker"/> implementation. It tracks the curly braces of the
	/// <c>{id}{sound}{text}</c> groups and reports bracket errors, bad or duplicate numbers and a few text style problems.
	/// </summary>
	public sealed class MessageStructureChecker : IMessageStructureChecker
	{
		private const Char OpenBrace = '{';
		private const Char CloseBrace = '}';

		private static readonly Char[] _punctuation = { '.', ',', '!', '?' };

		/// <inheritdoc/>
		public MessageStructureReport Check(String text)
		{
			ArgumentNullException.ThrowIfNull(text);

			Int32 openCurve = 0;
			Int32 closeCurve = 0;
			Int32 previousOpenOffset = 0;
			Int32 lastOpenOffset = 0;
			Int32 closeOffset = 0;
			Int32 offsetError = -1;
			Int32 numberStart = -1;
			Int32 openCount = 0;
			Boolean onlyOnce = false;
			Boolean newLine = false;

			List<Int32> usedNumbers = new List<Int32>();
			List<(BuildErrorType Type, String Message, Int32 Offset, Int32 Length)> numberIssues = new List<(BuildErrorType, String, Int32, Int32)>();
			List<(String Message, Int32 Offset)> hints = new List<(String, Int32)>();
			List<Int32> missingCommentMarks = new List<Int32>();

			for (Int32 offset = 0; offset < text.Length; offset++)
			{
				Char ch = text[offset];
				if (ch == '\r' || ch == '\n')
				{
					// Check for a white space at the beginning of a new line of a message text.
					if (!newLine && openCurve == 1 && closeCurve == 0 && openCount == 3)
					{
						Int32 i = -1;
						Boolean whiteSpace;
						do
						{
							whiteSpace = CharAt(text, offset + i) == ' ';
							i++;
						}
						while (!whiteSpace && i < 3);

						if (!whiteSpace)
						{
							hints.Add(("A new line without a white space character.", offset - 1));
						}
					}

					newLine = true;
					onlyOnce = false;
					continue;
				}

				if (ch == OpenBrace)
				{
					previousOpenOffset = lastOpenOffset;
					lastOpenOffset = offset;
					openCurve++;
					if (closeCurve > 0)
					{
						closeCurve--;
					}

					openCount++;
					if (openCount == 1)
					{
						numberStart = offset + 1;
					}
				}
				else if (ch == CloseBrace)
				{
					closeOffset = offset;
					closeCurve++;
					openCurve--;

					if (openCount == 1)
					{
						CheckMessageNumber(text, numberStart, offset - numberStart, usedNumbers, numberIssues);
					}
					else if (openCount == 3)
					{
						openCount = 0;
					}
				}

				if (closeCurve > 1)
				{
					offsetError = closeOffset;
					break;
				}

				if (openCurve > 1)
				{
					offsetError = previousOpenOffset;
					break;
				}

				if (openCurve < 0)
				{
					offsetError = closeOffset;
					break;
				}

				// Check the white space around punctuation.
				if (openCurve == 1 && closeCurve == 0 && Array.IndexOf(_punctuation, ch) >= 0)
				{
					Char next = CharAt(text, offset + 1);
					if (Char.IsLetter(next))
					{
						hints.Add(("The white space character is missing.", offset));
					}

					if (Char.IsWhiteSpace(next)
						&& CharAt(text, offset - 1) != _punctuation[0]
						&& ch != _punctuation[1]
						&& offset + 2 < text.Length
						&& Char.IsLower(text, offset + 2))
					{
						hints.Add(("The sentence begins with a small letter instead of the capital letter.", offset + 2));
					}
				}

				// Text after the closing brace must be a comment.
				if (openCurve == 0 && closeCurve == 1)
				{
					if (!onlyOnce && !Char.IsWhiteSpace(ch) && ch != OpenBrace && ch != CloseBrace)
					{
						onlyOnce = true;
						if (ch != '#')
						{
							missingCommentMarks.Add(offset);
						}
					}
				}
				else
				{
					onlyOnce = false;
				}

				newLine = false;
			}

			return new MessageStructureReport(BuildIssues(text, numberIssues, offsetError, hints, missingCommentMarks));
		}

		private static Char CharAt(String text, Int32 offset)
		{
			return offset >= 0 && offset < text.Length ? text[offset] : '\0';
		}

		private static List<MessageStructureIssue> BuildIssues(
			String text,
			List<(BuildErrorType Type, String Message, Int32 Offset, Int32 Length)> numberIssues,
			Int32 offsetError,
			List<(String Message, Int32 Offset)> hints,
			List<Int32> missingCommentMarks)
		{
			TextPositionMap map = new TextPositionMap(text);
			List<MessageStructureIssue> issues = new List<MessageStructureIssue>();

			foreach ((BuildErrorType type, String message, Int32 offset, Int32 length) in numberIssues)
			{
				(Int32 line, Int32 column) = map.GetPosition(offset);

				// An empty number is highlighted together with its braces.
				Int32 spanStart = length == 0 ? Math.Max(0, offset - 1) : offset;
				Int32 spanLength = length == 0 ? 2 : length;
				issues.Add(new MessageStructureIssue(type, message, spanStart, spanLength, line + 1, column + 1));
			}

			if (offsetError > -1)
			{
				(Int32 line, _) = map.GetPosition(offsetError);
				issues.Add(new MessageStructureIssue(
					BuildErrorType.Error,
					"Wrong structure of the paired curves brackets.",
					map.GetLineStart(line),
					map.GetLineLength(line, text),
					line + 1,
					1));
			}

			foreach ((String message, Int32 offset) in hints)
			{
				(Int32 line, Int32 column) = map.GetPosition(offset);
				issues.Add(new MessageStructureIssue(BuildErrorType.Message, message, Math.Max(0, offset - 1), 3, line + 1, column + 1));
			}

			foreach (Int32 offset in missingCommentMarks)
			{
				(Int32 line, Int32 column) = map.GetPosition(offset);
				Int32 lineEnd = map.GetLineStart(line) + map.GetLineLength(line, text);
				issues.Add(new MessageStructureIssue(BuildErrorType.None, "Missing '#' character of comment?", offset, lineEnd - offset, line + 1, column + 1));
			}

			return issues;
		}

		private static void CheckMessageNumber(
			String text,
			Int32 start,
			Int32 length,
			List<Int32> usedNumbers,
			List<(BuildErrorType Type, String Message, Int32 Offset, Int32 Length)> numberIssues)
		{
			String numberText = text.Substring(start, length);
			if (numberText.Trim().Length == 0)
			{
				numberIssues.Add((BuildErrorType.Error, "Missing line number of the message.", start, length));
			}
			else if (Int32.TryParse(numberText, NumberStyles.Integer, CultureInfo.InvariantCulture, out Int32 number) && number > 0)
			{
				if (usedNumbers.Contains(number))
				{
					numberIssues.Add((BuildErrorType.Warning, "Duplicate message line number: " + number.ToString(CultureInfo.InvariantCulture), start, length));
				}
				else
				{
					usedNumbers.Add(number);
				}
			}
			else
			{
				numberIssues.Add((BuildErrorType.Error, "Invalid line number of the message.", start, length));
			}
		}
	}
}
