namespace FalloutScriptStudio.Core.Messages
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.IO;
	using Microsoft.Extensions.Logging;

	/// <summary>
	/// <see cref="IMessageFileService"/> implementation working on the file system.
	/// </summary>
	public sealed class MessageFileService : IMessageFileService
	{
		private const String LineBreak = "\r\n";

		private readonly ILogger<MessageFileService> _logger;

		/// <summary>
		/// Initializes a new instance of the <see cref="MessageFileService"/> class.
		/// </summary>
		/// <param name="logger">The logger.</param>
		public MessageFileService(ILogger<MessageFileService> logger)
		{
			ArgumentNullException.ThrowIfNull(logger);
			this._logger = logger;
		}

		/// <inheritdoc/>
		public MessageFileLocation LocateForScript(String scriptPath, Int32? scriptNameId, MessageFileOptions options)
		{
			ArgumentException.ThrowIfNullOrEmpty(scriptPath);
			ArgumentNullException.ThrowIfNull(options);

			String? fileName = null;
			MessageLookupIssues issues = MessageLookupIssues.None;

			if (options.AssociateByScriptId && options.OutputDirectory != null && scriptNameId.HasValue)
			{
				if (scriptNameId.Value != -1)
				{
					fileName = GetMessageFileNameById(options.OutputDirectory, scriptNameId.Value, out MessageLookupIssues listIssues);
					if ((listIssues & MessageLookupIssues.ScriptsListEntryInvalid) != 0)
					{
						return new MessageFileLocation(false, null, null, null, listIssues);
					}

					issues |= listIssues;
				}
				else
				{
					issues |= MessageLookupIssues.ScriptIdNotFound;
				}
			}

			fileName ??= Path.ChangeExtension(Path.GetFileName(scriptPath), ".msg");
			return this.Check(scriptPath, fileName, options, issues);
		}

		/// <inheritdoc/>
		public MessageFileLocation LocateById(String scriptPath, Int32 messageFileNumber, MessageFileOptions options)
		{
			ArgumentException.ThrowIfNullOrEmpty(scriptPath);
			ArgumentNullException.ThrowIfNull(options);

			MessageLookupIssues issues = MessageLookupIssues.None;
			String? lookupDirectory = options.OutputDirectory;
			if (lookupDirectory == null)
			{
				lookupDirectory = Path.GetDirectoryName(scriptPath) ?? String.Empty;
				issues |= MessageLookupIssues.OutputDirectoryNotSet;
			}

			String? fileName = GetMessageFileNameById(lookupDirectory, messageFileNumber, out MessageLookupIssues listIssues);
			if (fileName == null)
			{
				return new MessageFileLocation(false, null, null, null, issues | listIssues);
			}

			return this.Check(scriptPath, fileName, options, issues);
		}

		/// <inheritdoc/>
		public async Task<String> CreateFileAsync(MessageFileLocation location, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(location);
			String path = location.DefaultPath
				?? throw new InvalidOperationException("The location does not define where the message file would be created.");

			if (File.Exists(path))
			{
				// The file appeared since it was located: it must not be replaced by the empty template.
				return path;
			}

			Directory.CreateDirectory(Path.GetDirectoryName(path)!);
			await AtomicFile.WriteAllBytesAsync(path, TextEncodings.Utf8.GetBytes(MessageFileTexts.NewFileContent), cancellationToken).ConfigureAwait(false);
			if (this._logger.IsEnabled(LogLevel.Information))
			{
				this._logger.FileCreated(path);
			}

			return path;
		}

		/// <inheritdoc/>
		public async Task<IReadOnlyList<String>> ReadLinesAsync(String path, Encoding encoding, CancellationToken cancellationToken = default)
		{
			ArgumentException.ThrowIfNullOrEmpty(path);
			ArgumentNullException.ThrowIfNull(encoding);
			return await File.ReadAllLinesAsync(path, encoding, cancellationToken).ConfigureAwait(false);
		}

		/// <inheritdoc/>
		public async Task<MessageDocument> ReadAsync(String path, Encoding encoding, CancellationToken cancellationToken = default)
		{
			IReadOnlyList<String> lines = await this.ReadLinesAsync(path, encoding, cancellationToken).ConfigureAwait(false);
			return MessageDocument.Parse(lines);
		}

		/// <inheritdoc/>
		public Task WriteAsync(String path, MessageDocument document, Encoding encoding, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(document);
			return WriteLinesAsync(path, document.Format(), encoding, cancellationToken);
		}

		/// <inheritdoc/>
		public async Task<Boolean> UpdateMessageTextAsync(String path, Int32 messageId, String text, Encoding encoding, CancellationToken cancellationToken = default)
		{
			IReadOnlyList<String> lines = await this.ReadLinesAsync(path, encoding, cancellationToken).ConfigureAwait(false);
			return await this.UpdateMessageTextAsync(path, lines.ToList(), messageId, text, encoding, cancellationToken).ConfigureAwait(false);
		}

		/// <inheritdoc/>
		public async Task<Boolean> UpdateMessageTextAsync(String path, List<String> lines, Int32 messageId, String text, Encoding encoding, CancellationToken cancellationToken = default)
		{
			if (!MessageTextEditor.TryReplaceText(lines, messageId, text))
			{
				this._logger.MessageNotFound(messageId, path);
				return false;
			}

			await WriteLinesAsync(path, lines, encoding, cancellationToken).ConfigureAwait(false);
			return true;
		}

		private static async Task WriteLinesAsync(String path, IReadOnlyList<String> lines, Encoding encoding, CancellationToken cancellationToken)
		{
			ArgumentException.ThrowIfNullOrEmpty(path);
			ArgumentNullException.ThrowIfNull(encoding);

			// The Cyrillic capital Ha looks like the Latin X; the game does not understand it in cp866 files.
			Boolean replaceCyrillicHa = encoding.CodePage == 866;
			StringBuilder text = new StringBuilder();
			foreach (String line in lines)
			{
				text.Append(replaceCyrillicHa ? line.Replace('Х', 'X') : line).Append(LineBreak);
			}

			await AtomicFile.WriteAllBytesAsync(path, encoding.GetBytes(text.ToString()), cancellationToken).ConfigureAwait(false);
		}

		/// <summary>
		/// Resolves a message file name from <c>scripts.lst</c>. The name is the entry up to and including the dot, followed by <c>msg</c>.
		/// </summary>
		private static String? GetMessageFileNameById(String directory, Int32 nameId, out MessageLookupIssues issues)
		{
			issues = MessageLookupIssues.None;
			String listPath = Path.Combine(directory, "scripts.lst");
			if (!File.Exists(listPath))
			{
				issues = MessageLookupIssues.ScriptsListMissing;
				return null;
			}

			try
			{
				String[] entries = File.ReadAllLines(listPath);
				String scriptId = entries[nameId - 1].TrimStart();
				return scriptId.Remove(scriptId.IndexOf('.') + 1) + "msg";
			}
			catch (Exception ex) when (ex is IndexOutOfRangeException or IOException or UnauthorizedAccessException)
			{
				issues = MessageLookupIssues.ScriptsListEntryInvalid;
				return null;
			}
		}

		private MessageFileLocation Check(String scriptPath, String fileName, MessageFileOptions options, MessageLookupIssues issues)
		{
			String scriptDirectory = Path.GetDirectoryName(scriptPath) ?? String.Empty;
			String defaultDirectory = options.DialogDirectory ?? scriptDirectory;

			// Primary check in the dialog folder of the output directory.
			String path = Path.Combine(defaultDirectory, fileName);
			if (File.Exists(path))
			{
				return new MessageFileLocation(true, path, fileName, defaultDirectory, issues);
			}

			// Second check in the configured message folders and their direct sub folders.
			foreach (String directory in options.SearchDirectories)
			{
				String? found = this.FindInDirectory(directory, fileName);
				if (found != null)
				{
					return new MessageFileLocation(true, found, fileName, defaultDirectory, issues);
				}
			}

			// Finally the folder of the script itself.
			if (options.OutputDirectory != null)
			{
				path = Path.Combine(scriptDirectory, fileName);
				if (File.Exists(path))
				{
					return new MessageFileLocation(true, path, fileName, defaultDirectory, issues);
				}
			}

			return new MessageFileLocation(false, null, fileName, defaultDirectory, issues);
		}

		private String? FindInDirectory(String directory, String fileName)
		{
			if (!Directory.Exists(directory))
			{
				return null;
			}

			String candidate = Path.Combine(directory, fileName);
			if (File.Exists(candidate))
			{
				return candidate;
			}

			try
			{
				foreach (String subFolder in Directory.GetDirectories(directory))
				{
					candidate = Path.Combine(subFolder, fileName);
					if (File.Exists(candidate))
					{
						return candidate;
					}
				}
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				this._logger.FolderNotSearched(ex, directory);
			}

			return null;
		}
	}
}
