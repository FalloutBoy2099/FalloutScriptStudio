namespace FalloutScriptStudio.Core.Messages
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;

	/// <summary>
	/// Replaces the text of a single message directly in the lines of a <c>.msg</c> file, leaving every other line exactly as it is.
	/// </summary>
	public static class MessageTextEditor
	{
		private static readonly Char[] _braces = { '{', '}' };

		/// <summary>
		/// Tells whether a message text contains a curly brace. Braces delimit the fields of a message line, so a text with a brace
		/// would break the structure of the file (the text would end at the brace and the rest would become the description).
		/// </summary>
		/// <param name="text">The message text.</param>
		/// <returns><see langword="true"/> if the text contains <c>{</c> or <c>}</c>.</returns>
		public static Boolean ContainsBraces(String text)
		{
			ArgumentNullException.ThrowIfNull(text);
			return text.IndexOfAny(_braces) >= 0;
		}

		/// <summary>
		/// Replaces the text of the message with the given number.
		/// </summary>
		/// <remarks>
		/// A message whose text spans several lines is collapsed into one entry. (The legacy editor overwrote the line that
		/// followed such a message; this is fixed here.) The line is split the way <see cref="MessageDocument.Parse"/> splits it:
		/// the number, the sound name and the text end at the first three closing braces, and what follows is the description.
		/// </remarks>
		/// <param name="lines">The lines of the file; modified in place on success.</param>
		/// <param name="messageId">The message number.</param>
		/// <param name="newText">The new text; may contain line breaks but no curly brace.</param>
		/// <returns><see langword="true"/> if the message was found and replaced; <see langword="false"/> if it was not found or the text contains a brace.</returns>
		public static Boolean TryReplaceText(List<String> lines, Int32 messageId, String newText)
		{
			ArgumentNullException.ThrowIfNull(lines);
			ArgumentNullException.ThrowIfNull(newText);

			if (ContainsBraces(newText))
			{
				return false;
			}

			for (Int32 i = 0; i < lines.Count; i++)
			{
				String line = lines[i];
				if (line.Length == 0 || line.TrimStart().StartsWith(MessageFileTexts.CommentPrefix, StringComparison.Ordinal))
				{
					continue;
				}

				Int32 idEnd = line.IndexOf('}');
				Int32 soundEnd = idEnd < 0 ? -1 : line.IndexOf('}', idEnd + 1);
				if (soundEnd < 0 || !TryGetId(line, idEnd, out Int32 index) || index != messageId)
				{
					continue;
				}

				Int32 textEnd = line.IndexOf('}', soundEnd + 1);
				Int32 opening = line.IndexOf('{', soundEnd + 1);
				Int32 textStart = opening >= 0 && (textEnd < 0 || opening < textEnd) ? opening + 1 : soundEnd + 1;
				String prefix = line.Substring(0, textStart);

				// The text may continue on the following lines until the closing brace is found.
				Int32 last = i;
				Int32 closing = textEnd;
				while (closing == -1 && ++last < lines.Count)
				{
					closing = lines[last].IndexOf('}');
				}

				String suffix;
				if (closing == -1)
				{
					last = lines.Count - 1;
					suffix = "}";
				}
				else
				{
					suffix = lines[last].Substring(closing);
				}

				lines.RemoveRange(i, last - i + 1);
				lines.Insert(i, prefix + newText + suffix);
				return true;
			}

			return false;
		}

		private static Boolean TryGetId(String line, Int32 idEnd, out Int32 id)
		{
			String idPart = line.Substring(0, idEnd);
			return Int32.TryParse(idPart.Substring(idPart.IndexOf('{') + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out id);
		}
	}
}
