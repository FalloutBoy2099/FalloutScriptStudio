namespace FalloutScriptStudio.Core.Messages
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using Configuration;
	using IO;

	/// <summary><see cref="IMessageComparisonService"/> working on the file system.</summary>
	public sealed class MessageComparisonService : IMessageComparisonService
	{
		/// <inheritdoc />
		public async Task<LoadedMessageFile> LoadAsync(String path, Encoding configured, Boolean isReference, CancellationToken cancellationToken = default)
		{
			ArgumentException.ThrowIfNullOrEmpty(path);
			ArgumentNullException.ThrowIfNull(configured);

			String full = Path.GetFullPath(path);
			Byte[] bytes = await SharedFile.ReadAllBytesAsync(full, cancellationToken).ConfigureAwait(false);
			String? language = MessageLanguages.DetectLanguage(full);
			Encoding fallback = isReference ? configured : MessageLanguages.GetEncoding(language, configured);
			DecodedMessageFile decoded = MessageFileDecoder.Decode(bytes, fallback, !isReference);
			return new LoadedMessageFile(full, language, decoded, MessageDocument.Parse(decoded.Lines));
		}

		/// <inheritdoc />
		public MessageComparison Compare(LoadedMessageFile reference, LoadedMessageFile target)
		{
			ArgumentNullException.ThrowIfNull(reference);
			ArgumentNullException.ThrowIfNull(target);

			List<MessageComparisonNote> notes = [];
			if (reference.Decoded.Encoding.CodePage != target.Decoded.Encoding.CodePage || reference.Decoded.HasByteOrderMark != target.Decoded.HasByteOrderMark)
			{
				notes.Add(new MessageComparisonNote(MessageComparisonNoteKind.EncodingDiffers, 0, reference.EncodingName + " / " + target.EncodingName));
			}

			notes.AddRange(reference.GetNotes());
			notes.AddRange(target.GetNotes());
			return MessageFileComparer.Compare(reference.Document, target.Document, notes);
		}

		/// <inheritdoc />
		public IReadOnlyList<MessageLanguageFile> FindSiblings(String path)
		{
			return MessageLanguages.FindSiblings(path);
		}

		/// <inheritdoc />
		public async Task<MessageAppendResult> AddMissingAsync(LoadedMessageFile target, IEnumerable<MessageEntry> messages, String? marker, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(target);
			ArgumentNullException.ThrowIfNull(messages);

			Byte[] bytes = await SharedFile.ReadAllBytesAsync(target.Path, cancellationToken).ConfigureAwait(false);
			DecodedMessageFile file = MessageFileDecoder.Decode(bytes, target.Decoded.Encoding, false);
			HashSet<Int32> present = MessageDocument.Parse(file.Lines).Entries.Where(entry => entry.Kind == MessageLineKind.Message).Select(entry => entry.Id).ToHashSet();
			Encoding strict = (Encoding)file.Encoding.Clone();
			strict.EncoderFallback = EncoderFallback.ExceptionFallback;

			List<Int32> added = [];
			List<Int32> notEncodable = [];
			List<String> newLines = [];
			foreach (MessageEntry entry in messages.Where(entry => entry.Kind == MessageLineKind.Message).DistinctBy(entry => entry.Id).OrderBy(entry => entry.Id))
			{
				if (present.Contains(entry.Id))
				{
					continue;
				}

				MessageEntry copy = new()
				{
					Kind = MessageLineKind.Message,
					Id = entry.Id,
					Text = entry.Text,
					Description = ComposeDescription(entry.Description, marker),
					IsPlayerLine = entry.IsPlayerLine,
					IndentTabs = entry.IndentTabs,
				};
				String formatted = copy.Format();
				if (file.Encoding.CodePage == MessageLanguages.DosCyrillicCodePage)
				{
					// The Cyrillic capital Ha looks like the Latin X; the game does not understand it in cp866 files (as MessageFileService writes them).
					formatted = formatted.Replace('Х', 'X');
				}

				if (!CanEncode(strict, formatted))
				{
					notEncodable.Add(entry.Id);
					continue;
				}

				added.Add(entry.Id);
				newLines.AddRange(formatted.Split(MessageEntry.LineBreak));
			}

			if (newLines.Count == 0)
			{
				return new MessageAppendResult(added, notEncodable);
			}

			List<String> lines = file.Lines.ToList();
			Int32 insertAt = lines.Count;
			while (insertAt > 0 && lines[insertAt - 1].Trim().Length == 0)
			{
				insertAt--;
			}

			lines.InsertRange(insertAt, newLines);
			Boolean endsWithNewLine = file.EndsWithNewLine || file.Lines.Count == 0;
			String text = String.Join(file.NewLine, lines) + (endsWithNewLine ? file.NewLine : String.Empty);
			Byte[] preamble = file.HasByteOrderMark ? GetPreamble(file.Encoding) : Array.Empty<Byte>();
			Byte[] body = file.Encoding.GetBytes(text);
			Byte[] content = new Byte[preamble.Length + body.Length];
			preamble.CopyTo(content, 0);
			body.CopyTo(content, preamble.Length);
			await AtomicFile.WriteAllBytesAsync(target.Path, content, cancellationToken).ConfigureAwait(false);
			return new MessageAppendResult(added, notEncodable);
		}

		/// <inheritdoc />
		public async Task WriteReportAsync(String path, String content, CancellationToken cancellationToken = default)
		{
			ArgumentException.ThrowIfNullOrEmpty(path);
			ArgumentNullException.ThrowIfNull(content);

			UTF8Encoding utf8 = new(true);
			Byte[] body = utf8.GetBytes(content);
			Byte[] preamble = utf8.GetPreamble();
			Byte[] bytes = new Byte[preamble.Length + body.Length];
			preamble.CopyTo(bytes, 0);
			body.CopyTo(bytes, preamble.Length);
			await AtomicFile.WriteAllBytesAsync(path, bytes, cancellationToken).ConfigureAwait(false);
		}

		private static String ComposeDescription(String description, String? marker)
		{
			String kept = description.TrimEnd();
			if (String.IsNullOrWhiteSpace(marker))
			{
				return kept;
			}

			return kept.Length > 0 ? kept + " " + marker.Trim() : marker.Trim();
		}

		private static Boolean CanEncode(Encoding strict, String text)
		{
			try
			{
				_ = strict.GetByteCount(text);
				return true;
			}
			catch (EncoderFallbackException)
			{
				return false;
			}
		}

		private static Byte[] GetPreamble(Encoding encoding)
		{
			return encoding.CodePage switch
			{
				65001 => new Byte[] { 0xEF, 0xBB, 0xBF },
				1200 => new Byte[] { 0xFF, 0xFE },
				1201 => new Byte[] { 0xFE, 0xFF },
				_ => Array.Empty<Byte>(),
			};
		}
	}
}