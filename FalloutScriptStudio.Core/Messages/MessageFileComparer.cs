namespace FalloutScriptStudio.Core.Messages
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	/// <summary>
	/// Compares a message file with the message file of another language: the messages of the two files are matched by number and each pair gets a
	/// <see cref="MessageComparisonStatus"/> and the issues of <see cref="MessageComparisonIssues"/>. The comparison is pure and linear: a file of
	/// 30 000 lines takes a few tens of milliseconds.
	/// </summary>
	/// <remarks>
	/// <para>Rules, in this order for a number that both files have:</para>
	/// <list type="number">
	/// <item><description>the target text is empty or white space while the reference has a text: <see cref="MessageComparisonStatus.EmptyInTarget"/>;</description></item>
	/// <item><description>the texts are equal (line breaks are not compared) and contain a letter once the placeholders are removed: <see cref="MessageComparisonStatus.SameText"/> (a text without a letter, such as <c>%d</c> or <c>---</c>, needs no translation);</description></item>
	/// <item><description>the texts are equal once the placeholders, tags and variables are removed and the white space is collapsed: <see cref="MessageComparisonStatus.PlaceholdersOnly"/>;</description></item>
	/// <item><description>the player marker (leading tab) or the description (the text after the closing brace) differs: <see cref="MessageComparisonStatus.PlayerOrDescription"/>;</description></item>
	/// <item><description>otherwise <see cref="MessageComparisonStatus.Ok"/>.</description></item>
	/// </list>
	/// <para>The issues are independent of the status: other <c>%s</c> / <c>%d</c> conversions (the order may change), another number of <c>&lt;...&gt;</c> tags or other <c>@...@</c> tags, other <c>$...$</c> variables, a white space only text, trailing spaces that the reference does not have, characters that no code page gives, a different player marker or description. A number that appears twice in a file is compared by its first message.</para>
	/// </remarks>
	public static class MessageFileComparer
	{
		/// <summary>Compares two message files.</summary>
		/// <param name="reference">The reference file (the language the translation is checked against).</param>
		/// <param name="target">The target file (the translation).</param>
		/// <param name="notes">Notes about the files that the caller knows (the encodings); the comparer adds the ones it finds.</param>
		/// <returns>The comparison.</returns>
		public static MessageComparison Compare(MessageDocument reference, MessageDocument target, IEnumerable<MessageComparisonNote>? notes = null)
		{
			ArgumentNullException.ThrowIfNull(reference);
			ArgumentNullException.ThrowIfNull(target);

			List<MessageComparisonNote> found = notes is null ? [] : notes.ToList();
			Dictionary<Int32, Located> referenceIndex = Index(reference, out Int32 referenceDuplicates, out _);
			Dictionary<Int32, Located> targetIndex = Index(target, out Int32 targetDuplicates, out Int32 unrecognized);
			if (referenceDuplicates > 0)
			{
				found.Add(new MessageComparisonNote(MessageComparisonNoteKind.DuplicateInReference, referenceDuplicates));
			}

			if (targetDuplicates > 0)
			{
				found.Add(new MessageComparisonNote(MessageComparisonNoteKind.DuplicateInTarget, targetDuplicates));
			}

			if (unrecognized > 0)
			{
				found.Add(new MessageComparisonNote(MessageComparisonNoteKind.UnrecognizedLines, unrecognized));
			}

			List<Int32> numbers = new(referenceIndex.Count + targetIndex.Count);
			numbers.AddRange(referenceIndex.Keys);
			foreach (Int32 number in targetIndex.Keys)
			{
				if (!referenceIndex.ContainsKey(number))
				{
					numbers.Add(number);
				}
			}

			numbers.Sort();
			List<MessageComparisonRow> rows = new(numbers.Count);
			foreach (Int32 number in numbers)
			{
				referenceIndex.TryGetValue(number, out Located? referenceMessage);
				targetIndex.TryGetValue(number, out Located? targetMessage);
				rows.Add(CreateRow(number, referenceMessage, targetMessage));
			}

			return new MessageComparison(rows, found);
		}

		/// <summary>Gets the status and the issues of a pair of messages.</summary>
		/// <param name="reference">The reference message.</param>
		/// <param name="target">The target message.</param>
		/// <param name="issues">Receives the issues.</param>
		/// <returns>The status.</returns>
		public static MessageComparisonStatus Classify(MessageEntry reference, MessageEntry target, out MessageComparisonIssues issues)
		{
			ArgumentNullException.ThrowIfNull(reference);
			ArgumentNullException.ThrowIfNull(target);

			String referenceText = reference.Text;
			String targetText = target.Text;
			issues = MessageComparisonIssues.None;
			if (reference.IsPlayerLine != target.IsPlayerLine)
			{
				issues |= MessageComparisonIssues.PlayerTabDiffers;
			}

			if (!String.Equals(reference.Description.Trim(), target.Description.Trim(), StringComparison.Ordinal))
			{
				issues |= MessageComparisonIssues.DescriptionDiffers;
			}

			Boolean targetBlank = targetText.AsSpan().Trim().Length == 0;
			Boolean referenceBlank = referenceText.AsSpan().Trim().Length == 0;
			if (targetBlank)
			{
				if (targetText.Length > 0)
				{
					issues |= MessageComparisonIssues.WhitespaceOnly;
				}

				return referenceBlank ? PlayerOrOk(issues) : MessageComparisonStatus.EmptyInTarget;
			}

			if (HasTrailingSpaces(targetText) && !HasTrailingSpaces(referenceText))
			{
				issues |= MessageComparisonIssues.TrailingSpaces;
			}

			if (HasSuspiciousCharacters(targetText))
			{
				issues |= MessageComparisonIssues.SuspiciousCharacters;
			}

			issues |= MessagePlaceholders.Compare(referenceText, targetText);

			if (SameLines(referenceText, targetText))
			{
				return MessagePlaceholders.Strip(referenceText).Any(Char.IsLetter) ? MessageComparisonStatus.SameText : PlayerOrOk(issues);
			}

			if (String.Equals(MessagePlaceholders.Strip(referenceText), MessagePlaceholders.Strip(targetText), StringComparison.Ordinal))
			{
				return MessageComparisonStatus.PlaceholdersOnly;
			}

			return PlayerOrOk(issues);
		}

		private static MessageComparisonStatus PlayerOrOk(MessageComparisonIssues issues)
		{
			return (issues & (MessageComparisonIssues.PlayerTabDiffers | MessageComparisonIssues.DescriptionDiffers)) != 0 ? MessageComparisonStatus.PlayerOrDescription : MessageComparisonStatus.Ok;
		}

		private static MessageComparisonRow CreateRow(Int32 number, Located? reference, Located? target)
		{
			if (target is null)
			{
				return new MessageComparisonRow(number, reference?.Entry, null, reference?.Line ?? -1, -1, MessageComparisonStatus.MissingInTarget, MessageComparisonIssues.None);
			}

			if (reference is null)
			{
				return new MessageComparisonRow(number, null, target.Entry, -1, target.Line, MessageComparisonStatus.ExtraInTarget, MessageComparisonIssues.None);
			}

			MessageComparisonStatus status = Classify(reference.Entry, target.Entry, out MessageComparisonIssues issues);
			return new MessageComparisonRow(number, reference.Entry, target.Entry, reference.Line, target.Line, status, issues);
		}

		private static Dictionary<Int32, Located> Index(MessageDocument document, out Int32 duplicates, out Int32 unrecognized)
		{
			Dictionary<Int32, Located> index = new(document.Entries.Count);
			duplicates = 0;
			unrecognized = 0;
			Int32 line = 1;
			foreach (MessageEntry entry in document.Entries)
			{
				if (entry.Kind == MessageLineKind.Message)
				{
					if (!index.TryAdd(entry.Id, new Located(entry, line)))
					{
						duplicates++;
					}

					line += CountLines(entry.Text);
				}
				else
				{
					if (entry.Kind == MessageLineKind.Raw)
					{
						unrecognized++;
					}

					line++;
				}
			}

			return index;
		}

		private static Int32 CountLines(String text)
		{
			Int32 lines = 1;
			Int32 at = -1;
			while ((at = text.IndexOf('\n', at + 1)) >= 0)
			{
				lines++;
			}

			return lines;
		}

		private static Boolean HasTrailingSpaces(String text)
		{
			return text.Length > 0 && (text[^1] == ' ' || text[^1] == '\t');
		}

		private static Boolean HasSuspiciousCharacters(String text)
		{
			foreach (Char c in text)
			{
				if (c == '�' || (c >= '\u0080' && c <= '\u009F'))
				{
					return true;
				}
			}

			return false;
		}

		private static Boolean SameLines(String first, String second)
		{
			if (String.Equals(first, second, StringComparison.Ordinal))
			{
				return true;
			}

			return first.Contains('\r', StringComparison.Ordinal) || second.Contains('\r', StringComparison.Ordinal)
				? String.Equals(first.Replace("\r\n", "\n", StringComparison.Ordinal), second.Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal)
				: false;
		}

		private sealed record Located(MessageEntry Entry, Int32 Line);
	}
}