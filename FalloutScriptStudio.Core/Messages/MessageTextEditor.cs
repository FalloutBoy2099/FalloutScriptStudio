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
		/// <summary>
		/// Replaces the text of the message with the given number.
		/// </summary>
		/// <remarks>
		/// A message whose text spans several lines is collapsed into one entry. (The legacy editor overwrote the line that
		/// followed such a message; this is fixed here.)
		/// </remarks>
		/// <param name="lines">The lines of the file; modified in place on success.</param>
		/// <param name="messageId">The message number.</param>
		/// <param name="newText">The new text; may contain line breaks.</param>
		/// <returns><see langword="true"/> if the message was found and replaced.</returns>
		public static Boolean TryReplaceText(List<String> lines, Int32 messageId, String newText)
		{
			ArgumentNullException.ThrowIfNull(lines);
			ArgumentNullException.ThrowIfNull(newText);

			for (Int32 i = 0; i < lines.Count; i++)
			{
				String[] parts = lines[i].Split('}', 2, StringSplitOptions.RemoveEmptyEntries);
				if (parts.Length != 2
					|| !Int32.TryParse(parts[0].Trim('{', ' ', '\t'), NumberStyles.Integer, CultureInfo.InvariantCulture, out Int32 index)
					|| index != messageId)
				{
					continue;
				}

				Int32 textStart = lines[i].LastIndexOf('{') + 1;
				String prefix = lines[i].Remove(textStart);

				// The text may continue on the following lines until the closing brace is found.
				Int32 last = i;
				Int32 closing = lines[i].IndexOf('}', textStart);
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
	}
}
