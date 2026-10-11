namespace FalloutScriptStudio.Core.Messages
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;

	/// <summary>
	/// The in-memory model of a <c>.msg</c> file: an ordered list of entries (messages, comments, empty lines).
	/// This is the model the Message Editor window works on.
	/// </summary>
	public sealed class MessageDocument
	{
		/// <summary>Gets the entries of the file in their original order.</summary>
		public List<MessageEntry> Entries { get; } = new List<MessageEntry>();

		/// <summary>
		/// Parses the physical lines of a message file.
		/// </summary>
		/// <param name="lines">The lines of the file.</param>
		/// <returns>The parsed document.</returns>
		public static MessageDocument Parse(IReadOnlyList<String> lines)
		{
			ArgumentNullException.ThrowIfNull(lines);

			MessageDocument document = new MessageDocument();
			for (Int32 i = 0; i < lines.Count; i++)
			{
				MessageEntry entry = ParseLine(lines[i], out Boolean continues);
				while (continues && ++i < lines.Count)
				{
					continues = AppendContinuation(entry, lines[i]);
				}

				document.Entries.Add(entry);
			}

			return document;
		}

		/// <summary>
		/// Formats all entries; a multi-line message yields one string with embedded line breaks.
		/// </summary>
		/// <returns>The formatted entries.</returns>
		public IReadOnlyList<String> Format()
		{
			List<String> lines = new List<String>(this.Entries.Count);
			foreach (MessageEntry entry in this.Entries)
			{
				lines.Add(entry.Format());
			}

			return lines;
		}

		/// <summary>
		/// Finds the first message with the given number.
		/// </summary>
		/// <param name="id">The message number.</param>
		/// <returns>The entry, or <see langword="null"/> if there is none.</returns>
		public MessageEntry? FindMessage(Int32 id)
		{
			foreach (MessageEntry entry in this.Entries)
			{
				if (entry.Kind == MessageLineKind.Message && entry.Id == id)
				{
					return entry;
				}
			}

			return null;
		}

		/// <summary>
		/// Gets the text of a message the way the dialog preview shows it: the first line is trimmed of spaces.
		/// </summary>
		/// <param name="id">The message number.</param>
		/// <param name="text">Receives the text.</param>
		/// <returns><see langword="true"/> if the message exists.</returns>
		public Boolean TryGetText(Int32 id, out String text)
		{
			MessageEntry? entry = this.FindMessage(id);
			text = entry == null ? String.Empty : GetDisplayText(entry);
			return entry != null;
		}

		/// <summary>
		/// Builds the number-to-text lookup used by dialog previews and flowcharts. For duplicate numbers the first message wins.
		/// </summary>
		/// <returns>The lookup.</returns>
		public Dictionary<Int32, String> BuildTextMap()
		{
			Dictionary<Int32, String> map = new Dictionary<Int32, String>();
			foreach (MessageEntry entry in this.Entries)
			{
				if (entry.Kind == MessageLineKind.Message)
				{
					map.TryAdd(entry.Id, GetDisplayText(entry));
				}
			}

			return map;
		}

		/// <summary>
		/// Calculates the number for a new message inserted after the entry at <paramref name="afterIndex"/>.
		/// </summary>
		/// <remarks>
		/// The nearest preceding message number is incremented. If a comment lies between that message and the insertion point,
		/// the number is rounded to tens and <paramref name="commentStep"/> is added instead (so every commented block starts on a round number).
		/// </remarks>
		/// <param name="afterIndex">The index of the entry the new message follows.</param>
		/// <param name="commentStep">The step added after a comment (1, 5, 10, 20, ...).</param>
		/// <param name="id">Receives the new number.</param>
		/// <returns><see langword="false"/> if the calculated number is already used.</returns>
		public Boolean TryGetNextId(Int32 afterIndex, Int32 commentStep, out Int32 id)
		{
			Int32 number = 0;
			Boolean afterComment = false;
			for (Int32 n = Math.Min(afterIndex, this.Entries.Count - 1); n >= 0; n--)
			{
				MessageEntry entry = this.Entries[n];
				if (entry.Kind == MessageLineKind.Message)
				{
					number = entry.Id;
					break;
				}

				if (entry.Text.StartsWith(MessageFileTexts.CommentPrefix, StringComparison.Ordinal))
				{
					afterComment = true;
				}
			}

			if (afterComment)
			{
				number = (Int32)Math.Round((Decimal)number / 10) * 10;
				number += commentStep;
			}
			else
			{
				number++;
			}

			id = number;
			return this.FindMessage(number) == null;
		}

		private static String GetDisplayText(MessageEntry entry)
		{
			String text = entry.Text;
			Int32 firstBreak = text.IndexOf(MessageEntry.LineBreak, StringComparison.Ordinal);
			return firstBreak < 0
				? text.Trim(' ')
				: String.Concat(text.AsSpan(0, firstBreak).Trim(' '), text.AsSpan(firstBreak));
		}

		private static MessageEntry ParseLine(String line, out Boolean continues)
		{
			continues = false;
			if (line.Length == 0)
			{
				return MessageEntry.CreateBlank();
			}

			if (line.TrimStart().StartsWith(MessageFileTexts.CommentPrefix, StringComparison.Ordinal))
			{
				return MessageEntry.CreateComment(line);
			}

			String[] parts = line.Split('}', 4);
			if (parts.Length < 3)
			{
				return new MessageEntry { Kind = MessageLineKind.Raw, Text = line };
			}

			String idPart = parts[0];
			Int32 tabs = 0;
			if (idPart.StartsWith('\t'))
			{
				String trimmed = idPart.TrimStart('\t');
				tabs = idPart.Length - trimmed.Length;
				idPart = trimmed;
			}

			if (!Int32.TryParse(AfterOpeningBrace(idPart), NumberStyles.Integer, CultureInfo.InvariantCulture, out Int32 id))
			{
				return new MessageEntry { Kind = MessageLineKind.Raw, Text = line };
			}

			continues = parts.Length == 3;
			return new MessageEntry
			{
				Kind = MessageLineKind.Message,
				Id = id,
				SoundName = AfterOpeningBrace(parts[1]),
				Text = AfterOpeningBrace(parts[2]),
				Description = parts.Length > 3 && parts[3].Length > 0 ? parts[3].TrimEnd() : String.Empty,
				IsPlayerLine = tabs > 0,
				IndentTabs = tabs > 0 ? tabs : 1,
			};
		}

		private static String AfterOpeningBrace(String part)
		{
			return part.Substring(part.IndexOf('{') + 1);
		}

		private static Boolean AppendContinuation(MessageEntry entry, String line)
		{
			Int32 end = line.IndexOf('}');
			if (end == -1)
			{
				entry.Text += MessageEntry.LineBreak + line;
				return true;
			}

			entry.Text += MessageEntry.LineBreak + line.Remove(end);
			entry.Description = line.Substring(end + 1).TrimEnd();
			return false;
		}
	}
}
