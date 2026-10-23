namespace FalloutScriptStudio.Core.Messages
{
	using System;
	using System.Collections.Generic;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>The files side of the comparison of message files: reading both files with the right encodings, finding the other languages of a file, adding the missing lines and writing a report.</summary>
	public interface IMessageComparisonService
	{
		/// <summary>Reads a message file. The reference is read with the encoding of the settings, a target is detected (byte order mark, UTF-8, then the code page of its language).</summary>
		/// <param name="path">The file.</param>
		/// <param name="configured">The encoding of the settings.</param>
		/// <param name="isReference"><see langword="true"/> for the reference file.</param>
		/// <param name="cancellationToken">Cancels the read.</param>
		/// <returns>The file.</returns>
		Task<LoadedMessageFile> LoadAsync(String path, Encoding configured, Boolean isReference, CancellationToken cancellationToken = default);

		/// <summary>Compares a target with a reference, adding the notes about their encodings.</summary>
		/// <param name="reference">The reference file.</param>
		/// <param name="target">The target file.</param>
		/// <returns>The comparison.</returns>
		MessageComparison Compare(LoadedMessageFile reference, LoadedMessageFile target);

		/// <summary>Finds the files of the other languages that belong to a message file.</summary>
		/// <param name="path">The message file.</param>
		/// <returns>The languages and paths.</returns>
		IReadOnlyList<MessageLanguageFile> FindSiblings(String path);

		/// <summary>Appends the messages of the reference that the target lacks, after the last line that is not empty. Every other line is kept, and the file keeps its encoding, byte order mark and line breaks; it is written atomically.</summary>
		/// <param name="target">The target as it was read.</param>
		/// <param name="messages">The messages to add (the reference entries of the missing rows).</param>
		/// <param name="marker">The text written after the closing brace of each added message (a comment that tells it is not translated), or <see langword="null"/>.</param>
		/// <param name="cancellationToken">Cancels the write.</param>
		/// <returns>The numbers that were added and the ones that could not be written.</returns>
		Task<MessageAppendResult> AddMissingAsync(LoadedMessageFile target, IEnumerable<MessageEntry> messages, String? marker, CancellationToken cancellationToken = default);

		/// <summary>Writes a report (CSV or text) to a file.</summary>
		/// <param name="path">The file.</param>
		/// <param name="content">The text.</param>
		/// <param name="cancellationToken">Cancels the write.</param>
		/// <returns>A task that completes when the file is written.</returns>
		Task WriteReportAsync(String path, String content, CancellationToken cancellationToken = default);
	}
}