namespace FalloutScriptStudio.Services.MessageEditor
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.Messages;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Views.MessageEditor;
	using Microsoft.Extensions.DependencyInjection;
	using Microsoft.Extensions.Logging;

	/// <inheritdoc/>
	public sealed class MessageEditorService : IMessageEditorService
	{
		private static String Title => Loc.Get("Str.MessageEditor.Title");

		private readonly IServiceProvider _services;
		private readonly IMessageFileService _files;
		private readonly ISettingsService _settings;
		private readonly IDialogService _dialogs;
		private readonly ILogger<MessageEditorService> _logger;
		private readonly List<MessageEditorWindow> _windows = new List<MessageEditorWindow>();

		/// <summary>
		/// Initializes a new instance of the <see cref="MessageEditorService"/> class.
		/// </summary>
		/// <param name="services">Creates the windows.</param>
		/// <param name="files">Locates and creates message files.</param>
		/// <param name="settings">The user settings.</param>
		/// <param name="dialogs">Shows questions and messages.</param>
		/// <param name="logger">The logger.</param>
		public MessageEditorService(
			IServiceProvider services,
			IMessageFileService files,
			ISettingsService settings,
			IDialogService dialogs,
			ILogger<MessageEditorService> logger)
		{
			ArgumentNullException.ThrowIfNull(services);
			ArgumentNullException.ThrowIfNull(files);
			ArgumentNullException.ThrowIfNull(settings);
			ArgumentNullException.ThrowIfNull(dialogs);
			ArgumentNullException.ThrowIfNull(logger);
			this._services = services;
			this._files = files;
			this._settings = settings;
			this._dialogs = dialogs;
			this._logger = logger;
		}

		/// <inheritdoc/>
		public event EventHandler? AllWindowsClosed;

		/// <inheritdoc/>
		public Int32 OpenWindowCount => this._windows.Count;

		/// <inheritdoc/>
		public Boolean Open(String? msgFilePath, IMessageLineTarget? target = null)
		{
			return this.Open(new MessageEditorOpenOptions { FilePath = msgFilePath, Target = target });
		}

		/// <inheritdoc/>
		public Boolean Open(MessageEditorOpenOptions options)
		{
			ArgumentNullException.ThrowIfNull(options);

			String? fullPath = options.FilePath is null ? null : Path.GetFullPath(options.FilePath);
			MessageEditorWindow? existing = fullPath is null
				? null
				: this._windows.FirstOrDefault(window => String.Equals(window.ViewModel.FilePath, fullPath, StringComparison.OrdinalIgnoreCase));
			if (existing is not null)
			{
				existing.Reveal(options);
				return true;
			}

			MessageEditorWindow created = this._services.GetRequiredService<MessageEditorWindow>();
			created.Closed += this.OnWindowClosed;
			this._windows.Add(created);
			created.Prepare(options with { FilePath = fullPath });
			if (options.Modal)
			{
				created.ShowDialog();
			}
			else
			{
				created.Show();
			}

			return true;
		}

		/// <inheritdoc/>
		public async Task<Boolean> OpenForScriptAsync(String scriptPath, Int32? scriptNameId, IMessageLineTarget target, Boolean createIfMissing = true)
		{
			ArgumentException.ThrowIfNullOrEmpty(scriptPath);
			ArgumentNullException.ThrowIfNull(target);

			MessageFileLocation location = this._files.LocateForScript(scriptPath, scriptNameId, this._settings.Settings.ToMessageFileOptions());
			if (!await this.ReportLookupIssuesAsync(location))
			{
				return false;
			}

			String? path = location.Path;
			if (!location.Found)
			{
				if (!createIfMissing || location.DefaultPath is null)
				{
					return false;
				}

				Boolean create = await this._dialogs.ConfirmAsync(
					Title,
					Loc.Get("Str.MessageEditor.Msg.NoFileForScript") + Environment.NewLine
					+ Loc.Format("Str.MessageEditor.Msg.CreateFileQuestion", location.FileName) + Environment.NewLine + Environment.NewLine
					+ Loc.Format("Str.MessageEditor.Msg.CreatedIn", location.DefaultDirectory),
					Loc.Get("Str.MessageEditor.Btn.Create"),
					Loc.Get("Str.Common.Cancel"));
				if (!create)
				{
					return false;
				}

				try
				{
					path = await this._files.CreateFileAsync(location);
				}
				catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
				{
					this._logger.LogError(ex, "The message file {Path} could not be created", location.DefaultPath);
					await this._dialogs.ShowErrorAsync(Title, Loc.Get("Str.MessageEditor.Msg.CreateFailed") + Environment.NewLine + ex.Message);
					return false;
				}
			}

			return this.Open(new MessageEditorOpenOptions { FilePath = path, Target = target });
		}

		/// <inheritdoc/>
		public async Task<Boolean> CloseAllAsync()
		{
			foreach (MessageEditorWindow window in this._windows.ToList())
			{
				if (!await window.ViewModel.CanCloseAsync())
				{
					return false;
				}

				window.ViewModel.SkipClosePrompt();
			}

			foreach (MessageEditorWindow window in this._windows.ToList())
			{
				window.Close();
			}

			return true;
		}

		/// <summary>
		/// Tells the user about conditions found while locating the file.
		/// </summary>
		/// <returns><see langword="false"/> if the file cannot be located at all.</returns>
		private async Task<Boolean> ReportLookupIssuesAsync(MessageFileLocation location)
		{
			if ((location.Issues & MessageLookupIssues.ScriptsListMissing) != 0)
			{
				await this._dialogs.ShowErrorAsync("Scripts.lst", Loc.Get("Str.MessageEditor.Msg.ScriptsListMissing"));
			}

			if ((location.Issues & MessageLookupIssues.ScriptsListEntryInvalid) != 0)
			{
				await this._dialogs.ShowErrorAsync("Scripts.lst", Loc.Get("Str.MessageEditor.Msg.ScriptsListEntryInvalid"));
				return false;
			}

			if ((location.Issues & MessageLookupIssues.ScriptIdNotFound) != 0)
			{
				await this._dialogs.ShowWarningAsync(
					Title,
					Loc.Get("Str.MessageEditor.Msg.NameMacroMissing") + Environment.NewLine
					+ Loc.Get("Str.MessageEditor.Msg.AssociatedByName"));
			}

			return true;
		}

		private void OnWindowClosed(Object? sender, EventArgs e)
		{
			if (sender is MessageEditorWindow window)
			{
				window.Closed -= this.OnWindowClosed;
				this._windows.Remove(window);
			}

			if (this._windows.Count == 0)
			{
				this.AllWindowsClosed?.Invoke(this, EventArgs.Empty);
			}
		}
	}
}
