namespace FalloutScriptStudio.Core.Dialogue
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using Messages;

	/// <summary>
	/// <see cref="IDialogueMessageSource"/> backed by <see cref="IMessageFileService"/>: the message file of the script is loaded when
	/// the source is created, other files (numbers of <c>scripts.lst</c>) on demand.
	/// </summary>
	public sealed class DialogueMessageSource : IDialogueMessageSource
	{
		private static readonly CompositeFormat _fileNotFound = CompositeFormat.Parse(MessageFileTexts.FileNotFoundFormat);

		private readonly IMessageFileService? _service;
		private readonly String _scriptPath;
		private readonly MessageFileOptions? _options;
		private readonly Dictionary<Int32, LoadedFile> _files = [];
		private readonly Object _sync = new Object();
		private LoadedFile _own;

		/// <summary>
		/// Initializes a new instance of the <see cref="DialogueMessageSource"/> class from messages that are already loaded.
		/// </summary>
		/// <param name="ownDocument">The message file of the script, or <c>null</c> when it does not exist.</param>
		/// <param name="ownPath">The path of that file.</param>
		/// <param name="service">Used to find and load other message files; <c>null</c> when only registered files are available.</param>
		/// <param name="scriptPath">The full path of the script; needed to look files up by number.</param>
		/// <param name="options">The message file lookup options; needed to look files up by number.</param>
		public DialogueMessageSource(MessageDocument? ownDocument, String? ownPath, IMessageFileService? service = null, String? scriptPath = null, MessageFileOptions? options = null)
		{
			this._own = new LoadedFile(ownDocument, ownPath);
			this._service = service;
			this._scriptPath = scriptPath ?? String.Empty;
			this._options = options;
		}

		/// <summary>
		/// Finds and loads the message file of a script.
		/// </summary>
		/// <param name="service">The message file service.</param>
		/// <param name="scriptPath">The full path of the script.</param>
		/// <param name="scriptNameId">The script number from its <c>NAME</c> macro, -1 if it has none, or <c>null</c> if the script was not parsed.</param>
		/// <param name="options">The lookup options.</param>
		/// <param name="cancellationToken">Cancels the operation.</param>
		/// <returns>The source; when the script has no message file lookups of its own file report <see cref="DialogueMessageStatus.FileNotFound"/>.</returns>
		public static async Task<DialogueMessageSource> CreateAsync(IMessageFileService service, String scriptPath, Int32? scriptNameId, MessageFileOptions options, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(service);
			ArgumentException.ThrowIfNullOrEmpty(scriptPath);
			ArgumentNullException.ThrowIfNull(options);

			MessageFileLocation location = service.LocateForScript(scriptPath, scriptNameId, options);
			MessageDocument? document = null;
			if (location.Found && location.Path is not null)
			{
				document = await service.ReadAsync(location.Path, options.Encoding, cancellationToken).ConfigureAwait(false);
			}

			return new DialogueMessageSource(document, location.Found ? location.Path : null, service, scriptPath, options);
		}

		/// <summary>Gets a value indicating whether the message file of the script itself is available.</summary>
		public Boolean HasOwnFile
		{
			get
			{
				lock (this._sync)
				{
					return this._own.Document is not null;
				}
			}
		}

		/// <summary>Registers an already loaded message file under a file number (or replaces the script's own file when the number is not positive).</summary>
		/// <param name="messageFileNumber">The file number.</param>
		/// <param name="document">The messages.</param>
		/// <param name="path">The path of the file.</param>
		public void SetFile(Int32 messageFileNumber, MessageDocument document, String? path)
		{
			ArgumentNullException.ThrowIfNull(document);

			lock (this._sync)
			{
				var file = new LoadedFile(document, path);
				if (messageFileNumber > 0)
				{
					this._files[messageFileNumber] = file;
				}
				else
				{
					this._own = file;
				}
			}
		}

		/// <inheritdoc />
		public async Task EnsureLoadedAsync(IEnumerable<Int32> messageFileNumbers, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(messageFileNumbers);

			if (this._service is null || this._options is null)
			{
				return;
			}

			List<Int32> missing;
			lock (this._sync)
			{
				missing = messageFileNumbers.Where(n => n > 0 && !this._files.ContainsKey(n)).Distinct().ToList();
			}

			foreach (Int32 number in missing)
			{
				MessageFileLocation location = this._service.LocateById(this._scriptPath, number, this._options);
				MessageDocument? document = null;
				if (location.Found && location.Path is not null)
				{
					try
					{
						document = await this._service.ReadAsync(location.Path, this._options.Encoding, cancellationToken).ConfigureAwait(false);
					}
					catch (System.IO.IOException)
					{
						document = null;
					}
				}

				lock (this._sync)
				{
					this._files[number] = new LoadedFile(document, location.Found ? location.Path : location.DefaultPath);
				}
			}
		}

		/// <inheritdoc />
		public DialogueMessageLookup GetMessage(Int32 messageFileNumber, Int32 messageNumber)
		{
			if (messageNumber <= 0)
			{
				return new DialogueMessageLookup(DialogueMessageStatus.NoMessageNumber, null, this.GetMessageFilePath(messageFileNumber));
			}

			LoadedFile? file = this.Find(messageFileNumber);
			if (file?.Document is null)
			{
				return new DialogueMessageLookup(
					DialogueMessageStatus.FileNotFound,
					String.Format(CultureInfo.InvariantCulture, _fileNotFound, messageFileNumber),
					file?.Path);
			}

			return file.Document.TryGetText(messageNumber, out String text)
				? new DialogueMessageLookup(DialogueMessageStatus.Found, text, file.Path)
				: new DialogueMessageLookup(DialogueMessageStatus.MessageNotFound, MessageFileTexts.TextNotFound, file.Path);
		}

		/// <inheritdoc />
		public String? GetMessageFilePath(Int32 messageFileNumber)
		{
			return this.Find(messageFileNumber)?.Path;
		}

		private LoadedFile? Find(Int32 messageFileNumber)
		{
			lock (this._sync)
			{
				if (messageFileNumber <= 0)
				{
					return this._own;
				}

				return this._files.TryGetValue(messageFileNumber, out LoadedFile? file) ? file : null;
			}
		}

		private sealed record LoadedFile(MessageDocument? Document, String? Path);
	}
}