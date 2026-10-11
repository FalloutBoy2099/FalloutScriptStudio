namespace FalloutScriptStudio.Core.Messages
{
	using System;
	using System.Collections.Generic;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>
	/// Locates, reads and writes <c>.msg</c> message files.
	/// </summary>
	public interface IMessageFileService
	{
		/// <summary>
		/// Looks for the message file that belongs to a script.
		/// </summary>
		/// <param name="scriptPath">The full path of the script.</param>
		/// <param name="scriptNameId">The script number from its <c>NAME</c> macro, -1 if the script has none, or <see langword="null"/> if the script was not parsed.</param>
		/// <param name="options">The lookup options.</param>
		/// <returns>The location; nothing is created or reported to the user.</returns>
		MessageFileLocation LocateForScript(String scriptPath, Int32? scriptNameId, MessageFileOptions options);

		/// <summary>
		/// Looks for a dialog message file by its number (the first argument of the script message calls), using <c>scripts.lst</c>.
		/// </summary>
		/// <param name="scriptPath">The full path of the script that references the file.</param>
		/// <param name="messageFileNumber">The number of the message file in <c>scripts.lst</c>.</param>
		/// <param name="options">The lookup options.</param>
		/// <returns>The location.</returns>
		MessageFileLocation LocateById(String scriptPath, Int32 messageFileNumber, MessageFileOptions options);

		/// <summary>
		/// Creates a new message file containing a single empty message at the default location of <paramref name="location"/>.
		/// </summary>
		/// <param name="location">The location returned by a lookup; its default path must be known.</param>
		/// <param name="cancellationToken">Cancels the operation.</param>
		/// <returns>The path of the created file; a file that already exists there is left untouched.</returns>
		/// <exception cref="InvalidOperationException">The location has no default path.</exception>
		Task<String> CreateFileAsync(MessageFileLocation location, CancellationToken cancellationToken = default);

		/// <summary>
		/// Reads the physical lines of a message file.
		/// </summary>
		/// <param name="path">The file to read.</param>
		/// <param name="encoding">The encoding of the file.</param>
		/// <param name="cancellationToken">Cancels the operation.</param>
		/// <returns>The lines.</returns>
		Task<IReadOnlyList<String>> ReadLinesAsync(String path, Encoding encoding, CancellationToken cancellationToken = default);

		/// <summary>
		/// Reads and parses a message file.
		/// </summary>
		/// <param name="path">The file to read.</param>
		/// <param name="encoding">The encoding of the file.</param>
		/// <param name="cancellationToken">Cancels the operation.</param>
		/// <returns>The parsed document.</returns>
		Task<MessageDocument> ReadAsync(String path, Encoding encoding, CancellationToken cancellationToken = default);

		/// <summary>
		/// Writes a message document to a file (atomically, with CRLF line breaks).
		/// </summary>
		/// <param name="path">The file to write.</param>
		/// <param name="document">The document.</param>
		/// <param name="encoding">The encoding of the file.</param>
		/// <param name="cancellationToken">Cancels the operation.</param>
		/// <returns>A task that completes when the file is written.</returns>
		Task WriteAsync(String path, MessageDocument document, Encoding encoding, CancellationToken cancellationToken = default);

		/// <summary>
		/// Replaces the text of one message and saves the file, keeping all other lines untouched.
		/// </summary>
		/// <param name="path">The message file.</param>
		/// <param name="messageId">The number of the message.</param>
		/// <param name="text">The new text.</param>
		/// <param name="encoding">The encoding of the file.</param>
		/// <param name="cancellationToken">Cancels the operation.</param>
		/// <returns><see langword="false"/> if the message does not exist (the file is not modified).</returns>
		Task<Boolean> UpdateMessageTextAsync(String path, Int32 messageId, String text, Encoding encoding, CancellationToken cancellationToken = default);

		/// <summary>
		/// Replaces the text of one message in already loaded lines and saves them, keeping all other lines untouched.
		/// </summary>
		/// <param name="path">The message file.</param>
		/// <param name="lines">The loaded lines; updated in place on success.</param>
		/// <param name="messageId">The number of the message.</param>
		/// <param name="text">The new text.</param>
		/// <param name="encoding">The encoding of the file.</param>
		/// <param name="cancellationToken">Cancels the operation.</param>
		/// <returns><see langword="false"/> if the message does not exist (nothing is written).</returns>
		Task<Boolean> UpdateMessageTextAsync(String path, List<String> lines, Int32 messageId, String text, Encoding encoding, CancellationToken cancellationToken = default);
	}
}
