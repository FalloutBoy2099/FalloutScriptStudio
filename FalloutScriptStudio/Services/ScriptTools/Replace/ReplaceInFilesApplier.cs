namespace FalloutScriptStudio.Services.ScriptTools.Replace
{
	using System;
	using FalloutScriptStudio.Core.IO;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Security.Cryptography;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using Core.TextTools;
	using Documents;
	using Search;
	using Workspace;

	/// <summary>
	/// The second phase of "Replace in Files": makes the replacements the user ticked. Nothing is trusted from the preview: every file is read
	/// again and must have the hash it had then (an open document must have the same text), otherwise it is left alone and reported. An open
	/// document is edited as one undoable step; any other file is rewritten in its own encoding, with its byte order mark and its line breaks,
	/// through a temporary file, so a failure never leaves a half-written file. A file that cannot be changed does not stop the others.
	/// </summary>
	public sealed class ReplaceInFilesApplier
	{
		private readonly IWorkspace _workspace;
		private readonly IDocumentFileWriter _writer;

		/// <summary>Initializes a new instance of the <see cref="ReplaceInFilesApplier"/> class.</summary>
		/// <param name="workspace">The open documents.</param>
		/// <param name="writer">Writes the files that are not open; <see langword="null"/> for the atomic writer of the editor.</param>
		public ReplaceInFilesApplier(IWorkspace workspace, IDocumentFileWriter? writer = null)
		{
			ArgumentNullException.ThrowIfNull(workspace);
			this._workspace = workspace;
			this._writer = writer ?? new AtomicDocumentFileWriter();
		}

		/// <summary>Makes the ticked replacements.</summary>
		/// <param name="plan">The plan the selection comes from.</param>
		/// <param name="selection">The ticked replacements per file.</param>
		/// <param name="progress">Receives a report after each file; may be <see langword="null"/>.</param>
		/// <param name="cancellationToken">Stops the run between two files; what was done stays done and is reported.</param>
		/// <returns>What was replaced and what was left alone.</returns>
		public async Task<ReplaceResult> ApplyAsync(ReplacePlan plan, IReadOnlyList<ReplaceSelection> selection, IProgress<SearchProgress>? progress, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(plan);
			ArgumentNullException.ThrowIfNull(selection);

			List<ReplaceFailure> failures = [];
			List<ReplaceUndoFile> undo = [];
			List<DocumentViewModel> changedDocuments = [];
			Int32 replacements = 0;
			Boolean cancelled = false;
			Int32 done = 0;
			progress?.Report(new SearchProgress(0, selection.Count, 0));

			foreach (ReplaceSelection item in selection.Where(item => item.Matches.Count > 0))
			{
				if (cancellationToken.IsCancellationRequested)
				{
					cancelled = true;
					break;
				}

				FileOutcome outcome = item.File.Owner is null
					? await this.ApplyToFileAsync(item, plan.FallbackEncoding, cancellationToken)
					: this.ApplyToDocument(item);
				if (outcome.Failure is not null)
				{
					failures.Add(outcome.Failure);
				}
				else if (outcome.Undo is not null)
				{
					undo.Add(outcome.Undo);
					replacements += item.Matches.Count;
					if (item.File.Owner is DocumentViewModel document)
					{
						changedDocuments.Add(document);
					}
				}

				done++;
				progress?.Report(new SearchProgress(done, selection.Count, replacements));
			}

			foreach (DocumentViewModel document in changedDocuments)
			{
				await this._workspace.RequestParseAsync(document, true);
			}

			ReplaceUndoRecord? record = undo.Count == 0 ? null : new ReplaceUndoRecord(undo, plan.FallbackEncoding, replacements);
			return new ReplaceResult(replacements, undo.Count, failures, record, cancelled);
		}

		/// <summary>Puts back what a run replaced, in every file that was not touched since.</summary>
		/// <param name="record">What the run changed.</param>
		/// <param name="cancellationToken">Stops between two files.</param>
		/// <returns>How many files were restored, and the ones that were left as they are.</returns>
		public async Task<ReplaceUndoResult> UndoAsync(ReplaceUndoRecord record, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(record);

			List<ReplaceFailure> failures = [];
			List<DocumentViewModel> changedDocuments = [];
			Int32 restored = 0;
			foreach (ReplaceUndoFile file in record.Files)
			{
				cancellationToken.ThrowIfCancellationRequested();
				ReplaceFailure? failure = file.Owner is DocumentViewModel document
					? this.UndoDocument(document, file, changedDocuments)
					: await this.UndoFileAsync(file, record.FallbackEncoding, cancellationToken);
				if (failure is null)
				{
					restored++;
				}
				else
				{
					failures.Add(failure);
				}
			}

			foreach (DocumentViewModel document in changedDocuments)
			{
				await this._workspace.RequestParseAsync(document, true);
			}

			return new ReplaceUndoResult(restored, failures);
		}

		private static List<TextEdit> ToEdits(IReadOnlyList<ReplaceMatch> matches)
		{
			return matches.Select(match => new TextEdit(match.Offset, match.Length, match.Replacement)).ToList();
		}

		private static List<ReplaceReverseEdit> ToReverseEdits(IReadOnlyList<ReplaceMatch> matches)
		{
			List<ReplaceReverseEdit> reverse = new(matches.Count);
			Int32 shift = 0;
			foreach (ReplaceMatch match in matches.OrderBy(m => m.Offset))
			{
				reverse.Add(new ReplaceReverseEdit(match.Offset + shift, match.Replacement.Length, match.Matched));
				shift += match.Replacement.Length - match.Length;
			}

			return reverse;
		}

		private static Boolean Matches(String text, IReadOnlyList<ReplaceMatch> matches)
		{
			foreach (ReplaceMatch match in matches)
			{
				if (match.Offset < 0 || match.Offset + match.Length > text.Length || String.CompareOrdinal(text, match.Offset, match.Matched, 0, match.Length) != 0)
				{
					return false;
				}
			}

			return true;
		}

		private static Byte[] Encode(Encoding encoding, String text)
		{
			// An encoding that refuses what it cannot hold: the default ones turn it into '?' and the replacement would silently damage the file.
			Encoding strict = Encoding.GetEncoding(encoding.CodePage, EncoderFallback.ExceptionFallback, DecoderFallback.ReplacementFallback);
			Byte[] preamble = encoding.GetPreamble();
			Byte[] body = strict.GetBytes(text);
			if (preamble.Length == 0)
			{
				return body;
			}

			Byte[] result = new Byte[preamble.Length + body.Length];
			preamble.CopyTo(result, 0);
			body.CopyTo(result, preamble.Length);
			return result;
		}

		private static ReplaceFailure Fail(String path, ReplaceFailureReason reason, Int32 matches, String detail = "")
		{
			return new ReplaceFailure(path, reason, detail, matches);
		}

		private static Boolean IsReadOnly(String path)
		{
			return (File.GetAttributes(path) & FileAttributes.ReadOnly) != 0;
		}

		/// <summary>Tells why access was denied: the read-only attribute, or something else that holds the file (a program that has it open).</summary>
		private static ReplaceFailureReason DeniedReason(String path)
		{
			try
			{
				return IsReadOnly(path) ? ReplaceFailureReason.ReadOnly : ReplaceFailureReason.WriteFailed;
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				return ReplaceFailureReason.WriteFailed;
			}
		}

		private FileOutcome ApplyToDocument(ReplaceSelection item)
		{
			String path = item.File.Path;
			if (item.File.Owner is not DocumentViewModel document || !this._workspace.Documents.Contains(document))
			{
				return new FileOutcome(null, Fail(path, ReplaceFailureReason.Closed, item.Matches.Count));
			}

			String text = document.TextDocument.Text;
			if (!ReplaceInFilesPlanner.Hash(text).AsSpan().SequenceEqual(item.File.Hash) || !Matches(text, item.Matches))
			{
				return new FileOutcome(null, Fail(path, ReplaceFailureReason.Changed, item.Matches.Count));
			}

			document.TextDocument.ApplyEdits(ToEdits(item.Matches));
			return new FileOutcome(new ReplaceUndoFile(path, document, ReplaceInFilesPlanner.Hash(document.TextDocument.Text), ToReverseEdits(item.Matches)), null);
		}

		private async Task<FileOutcome> ApplyToFileAsync(ReplaceSelection item, Encoding fallback, CancellationToken cancellationToken)
		{
			String path = item.File.Path;
			Int32 count = item.Matches.Count;
			Byte[] bytes;
			try
			{
				if (IsReadOnly(path))
				{
					return new FileOutcome(null, Fail(path, ReplaceFailureReason.ReadOnly, count));
				}

				bytes = await SharedFile.ReadAllBytesAsync(path, cancellationToken);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				return new FileOutcome(null, Fail(path, ReplaceFailureReason.Unreadable, count, ex.Message));
			}

			if (!SHA256.HashData(bytes).AsSpan().SequenceEqual(item.File.Hash))
			{
				return new FileOutcome(null, Fail(path, ReplaceFailureReason.Changed, count));
			}

			DecodedText decoded = DocumentTextCodec.Decode(bytes, fallback);
			if (!Matches(decoded.Text, item.Matches))
			{
				return new FileOutcome(null, Fail(path, ReplaceFailureReason.Changed, count));
			}

			String result = TextEditApplier.Apply(decoded.Text, ToEdits(item.Matches));
			if (String.Equals(result, decoded.Text, StringComparison.Ordinal))
			{
				return new FileOutcome(null, null);
			}

			Byte[] output;
			try
			{
				output = Encode(decoded.Encoding, result);
			}
			catch (EncoderFallbackException ex)
			{
				return new FileOutcome(null, Fail(path, ReplaceFailureReason.NotEncodable, count, ex.Message));
			}

			try
			{
				await this._writer.WriteAsync(path, output);
			}
			catch (UnauthorizedAccessException ex)
			{
				return new FileOutcome(null, Fail(path, DeniedReason(path), count, ex.Message));
			}
			catch (IOException ex)
			{
				return new FileOutcome(null, Fail(path, ReplaceFailureReason.WriteFailed, count, ex.Message));
			}

			return new FileOutcome(new ReplaceUndoFile(path, null, SHA256.HashData(output), ToReverseEdits(item.Matches)), null);
		}

		private ReplaceFailure? UndoDocument(DocumentViewModel document, ReplaceUndoFile file, List<DocumentViewModel> changed)
		{
			if (!this._workspace.Documents.Contains(document))
			{
				return Fail(file.Path, ReplaceFailureReason.Closed, file.Edits.Count);
			}

			if (!ReplaceInFilesPlanner.Hash(document.TextDocument.Text).AsSpan().SequenceEqual(file.ResultHash))
			{
				return Fail(file.Path, ReplaceFailureReason.Changed, file.Edits.Count);
			}

			document.TextDocument.ApplyEdits(file.Edits.Select(edit => new TextEdit(edit.Offset, edit.Length, edit.OldText)).ToList());
			changed.Add(document);
			return null;
		}

		private async Task<ReplaceFailure?> UndoFileAsync(ReplaceUndoFile file, Encoding fallback, CancellationToken cancellationToken)
		{
			Int32 count = file.Edits.Count;
			Byte[] bytes;
			try
			{
				if (IsReadOnly(file.Path))
				{
					return Fail(file.Path, ReplaceFailureReason.ReadOnly, count);
				}

				bytes = await SharedFile.ReadAllBytesAsync(file.Path, cancellationToken);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				return Fail(file.Path, ReplaceFailureReason.Unreadable, count, ex.Message);
			}

			if (!SHA256.HashData(bytes).AsSpan().SequenceEqual(file.ResultHash))
			{
				return Fail(file.Path, ReplaceFailureReason.Changed, count);
			}

			DecodedText decoded = DocumentTextCodec.Decode(bytes, fallback);
			String restored = TextEditApplier.Apply(decoded.Text, file.Edits.Select(edit => new TextEdit(edit.Offset, edit.Length, edit.OldText)));
			try
			{
				await this._writer.WriteAsync(file.Path, Encode(decoded.Encoding, restored));
			}
			catch (EncoderFallbackException ex)
			{
				return Fail(file.Path, ReplaceFailureReason.NotEncodable, count, ex.Message);
			}
			catch (UnauthorizedAccessException ex)
			{
				return Fail(file.Path, DeniedReason(file.Path), count, ex.Message);
			}
			catch (IOException ex)
			{
				return Fail(file.Path, ReplaceFailureReason.WriteFailed, count, ex.Message);
			}

			return null;
		}

		private sealed record FileOutcome(ReplaceUndoFile? Undo, ReplaceFailure? Failure);
	}
}