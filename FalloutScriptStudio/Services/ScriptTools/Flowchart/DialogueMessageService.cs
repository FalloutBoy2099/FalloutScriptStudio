namespace FalloutScriptStudio.Services.ScriptTools.Flowchart
{
	using System;
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.Dialogue;
	using FalloutScriptStudio.Core.Messages;
	using FalloutScriptStudio.Documents;

	/// <inheritdoc />
	public sealed class DialogueMessageService : IDialogueMessageService
	{
		private readonly IMessageFileService _files;
		private readonly ISettingsService _settings;

		/// <summary>
		/// Initializes a new instance of the <see cref="DialogueMessageService"/> class.
		/// </summary>
		/// <param name="files">Locates, reads and writes message files.</param>
		/// <param name="settings">Provides the message file options.</param>
		public DialogueMessageService(IMessageFileService files, ISettingsService settings)
		{
			ArgumentNullException.ThrowIfNull(files);
			ArgumentNullException.ThrowIfNull(settings);
			this._files = files;
			this._settings = settings;
		}

		/// <inheritdoc />
		public async Task<IDialogueMessageSource> CreateSourceAsync(DocumentViewModel script, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(script);

			if (String.IsNullOrEmpty(script.FilePath))
			{
				return new DialogueMessageSource(null, null);
			}

			MessageFileOptions options = this._settings.Settings.ToMessageFileOptions();
			try
			{
				return await DialogueMessageSource.CreateAsync(this._files, script.FilePath, script.ProgramInfo?.ScriptNameId, options, cancellationToken);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				return new DialogueMessageSource(null, null, this._files, script.FilePath, options);
			}
		}

		/// <inheritdoc />
		public async Task<Boolean> UpdateMessageTextAsync(String messageFilePath, Int32 messageId, String text, CancellationToken cancellationToken = default)
		{
			ArgumentException.ThrowIfNullOrEmpty(messageFilePath);
			ArgumentNullException.ThrowIfNull(text);

			try
			{
				return await this._files.UpdateMessageTextAsync(messageFilePath, messageId, text, this._settings.Settings.ToMessageFileOptions().Encoding, cancellationToken);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				return false;
			}
		}
	}
}
