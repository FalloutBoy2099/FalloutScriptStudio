namespace FalloutScriptStudio.ViewModels.MessageEditor
{
	using System;
	using System.ComponentModel;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using CommunityToolkit.Mvvm.ComponentModel;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.Messages;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Services.MessageEditor;
	using Microsoft.Extensions.Logging;

	public sealed partial class MessageEditorViewModel
	{
		private const String UnsavedFileName = "unsaved.msg";

		private ExternalChangeKind _externalChange;

		private enum ExternalChangeKind
		{
			None,
			Deleted,
			ChangedWithUnsaved,
			Changed,
		}

		/// <summary>Gets the explanation shown when the file was changed by another program; empty when it was not.</summary>
		public String ExternalChangeMessage => this._externalChange switch
		{
			ExternalChangeKind.Deleted => Loc.Get("Str.MessageEditor.External.Deleted"),
			ExternalChangeKind.ChangedWithUnsaved => Loc.Get("Str.MessageEditor.External.ChangedUnsaved"),
			ExternalChangeKind.Changed => Loc.Get("Str.MessageEditor.External.Changed"),
			_ => String.Empty,
		};

		/// <summary>Gets a value indicating whether the file on disk differs from what was loaded or saved.</summary>
		public Boolean HasExternalChange => this._externalChange != ExternalChangeKind.None;

		private ExternalChangeKind ExternalChange
		{
			get => this._externalChange;
			set
			{
				if (this.SetProperty(ref this._externalChange, value))
				{
					this.OnPropertyChanged(nameof(this.ExternalChangeMessage));
					this.OnPropertyChanged(nameof(this.HasExternalChange));
				}
			}
		}

		/// <summary>
		/// Opens the editor with the given options: connects the target, loads the file and selects a message.
		/// </summary>
		/// <param name="options">The options.</param>
		/// <returns>A task that completes when the file is shown.</returns>
		public async Task InitializeAsync(MessageEditorOpenOptions options)
		{
			ArgumentNullException.ThrowIfNull(options);

			this.AttachTarget(options.Target, options.CloseOnSend);
			this.IsWindowOnTop = options.Target is not null;
			if (options.FilePath is null)
			{
				this.LoadBlank();
				return;
			}

			if (!await this.LoadFileAsync(options.FilePath, options.SelectMessageId))
			{
				this.LoadBlank();
			}
		}

		/// <summary>
		/// Selects the message with the given number, if the file has it.
		/// </summary>
		/// <param name="messageId">The message number.</param>
		/// <returns><see langword="true"/> if the message was found.</returns>
		public Boolean SelectMessage(Int32 messageId)
		{
			MessageRowViewModel? row = this._rows.FirstOrDefault(candidate => candidate.IsMessage && candidate.Entry.Id == messageId);
			if (row is null)
			{
				return false;
			}

			this.RequestFocus(row, MessageGridColumn.Text, false);
			return true;
		}

		/// <summary>
		/// Loads a message file into the grid, replacing the current content. The caller has to deal with unsaved changes first.
		/// </summary>
		/// <param name="path">The file to load.</param>
		/// <param name="selectMessageId">The message to select afterwards, or <see langword="null"/>.</param>
		/// <returns><see langword="true"/> if the file was loaded; the problem was reported to the user otherwise.</returns>
		public async Task<Boolean> LoadFileAsync(String path, Int32? selectMessageId = null)
		{
			ArgumentException.ThrowIfNullOrEmpty(path);

			String full = Path.GetFullPath(path);
			if (!this._platform.FileExists(full))
			{
				await this._dialogs.ShowErrorAsync(ApplicationTitle, Loc.Get("Str.MessageEditor.Msg.FileNotFound") + Environment.NewLine + full);
				return false;
			}

			MessageDocument document;
			this.IsBusy = true;
			try
			{
				document = await this._files.ReadAsync(full, this.CurrentEncoding);
			}
			catch (Exception ex) when (IsIoFailure(ex))
			{
				this._logger.FileNotRead(ex, full);
				await this._dialogs.ShowErrorAsync(ApplicationTitle, Loc.Get("Str.MessageEditor.Msg.ReadFailed") + Environment.NewLine + ex.Message);
				return false;
			}
			finally
			{
				this.IsBusy = false;
			}

			this.FilePath = full;
			this._stamp = this._platform.GetFileStamp(full);
			this.ExternalChange = ExternalChangeKind.None;
			this.ResetRows(document.Entries);
			this.AddRecent(full);
			this.Target?.MessagesUpdated(full, this.BuildDocument());
			this.StatusMessage = Loc.Format("Str.MessageEditor.Status.Loaded", Path.GetFileName(full));
			if (selectMessageId.HasValue)
			{
				this.SelectMessage(selectMessageId.Value);
			}

			return true;
		}

		/// <summary>
		/// Asks what to do with unsaved changes before they would be lost.
		/// </summary>
		/// <returns><see langword="true"/> if the caller may continue (changes were saved or may be discarded).</returns>
		public async Task<Boolean> ConfirmDiscardAsync()
		{
			if (!this._isModified)
			{
				return true;
			}

			YesNoCancel answer = await this._dialogs.AskYesNoCancelAsync(
				ApplicationTitle,
				Loc.Get("Str.MessageEditor.Msg.SaveChanges") + Environment.NewLine + this.DisplayName,
				Loc.Get("Str.Common.Save"),
				Loc.Get("Str.MessageEditor.Btn.DontSave"));
			return answer switch
			{
				YesNoCancel.Yes => await this.SaveDocumentAsync(),
				YesNoCancel.No => true,
				_ => false,
			};
		}

		/// <summary>
		/// Called by the window when it is about to close.
		/// </summary>
		/// <returns><see langword="true"/> if the window may close.</returns>
		public async Task<Boolean> CanCloseAsync()
		{
			return this.SuppressClosePrompt || await this.ConfirmDiscardAsync();
		}

		/// <summary>
		/// Writes the options that changed while the window was open (recent files, font size...) to disk.
		/// </summary>
		public void PersistSettings()
		{
			try
			{
				this._settings.Save();
			}
			catch (Exception ex) when (IsIoFailure(ex))
			{
				this._logger.SettingsNotSaved(ex);
			}
		}

		/// <summary>
		/// Checks whether another program changed or deleted the file since it was loaded or saved; the window calls this when it is activated.
		/// </summary>
		public void CheckExternalChange()
		{
			if (this._filePath is null || this.HasExternalChange)
			{
				return;
			}

			FileStamp? now = this._platform.GetFileStamp(this._filePath);
			if (now == this._stamp)
			{
				return;
			}

			this.ExternalChange = now is null
				? ExternalChangeKind.Deleted
				: this._isModified
					? ExternalChangeKind.ChangedWithUnsaved
					: ExternalChangeKind.Changed;
		}

		/// <summary>
		/// Saves the document; asks for a file name when it has none.
		/// </summary>
		/// <returns><see langword="true"/> if the file was written.</returns>
		public async Task<Boolean> SaveDocumentAsync()
		{
			this.EndEdit();
			return this._filePath is null ? await this.SaveDocumentAsAsync() : await this.WriteFileAsync(this._filePath);
		}

		/// <summary>
		/// Asks for a file name and saves the document there.
		/// </summary>
		/// <returns><see langword="true"/> if the file was written.</returns>
		public async Task<Boolean> SaveDocumentAsAsync()
		{
			this.EndEdit();
			String suggested = this._filePath is not null
				? Path.GetFileName(this._filePath)
				: this.Target?.ScriptPath is { Length: > 0 } script ? Path.ChangeExtension(Path.GetFileName(script), ".msg") : String.Empty;
			String? picked = this._dialogs.PickSaveFile(Loc.Get("Str.MessageEditor.Dlg.SaveFile"), FileFilter, this.GetInitialDirectory(), suggested);
			if (picked is null)
			{
				return false;
			}

			if (!Path.HasExtension(picked))
			{
				picked = Path.ChangeExtension(picked, ".msg");
			}

			String full = Path.GetFullPath(picked);
			if (!await this.WriteFileAsync(full))
			{
				return false;
			}

			this.FilePath = full;
			this.AddRecent(full);
			return true;
		}

		private Boolean CanSave()
		{
			return this._isModified || this._filePath is null;
		}

		private Boolean HasFile()
		{
			return this._filePath is not null;
		}

		private Boolean CanOpenAsText()
		{
			return this._filePath is not null && this.Target is not null;
		}

		private void NotifyFileCommands()
		{
			this.SaveCommand.NotifyCanExecuteChanged();
			this.ReloadCommand.NotifyCanExecuteChanged();
			this.OpenInExternalEditorCommand.NotifyCanExecuteChanged();
			this.OpenAsTextCommand.NotifyCanExecuteChanged();
		}

		private async Task<Boolean> WriteFileAsync(String path)
		{
			Int32 savingRevision = this._revision;
			MessageDocument document = this.BuildDocument();
			this.IsBusy = true;
			try
			{
				await this._files.WriteAsync(path, document, this.CurrentEncoding);
			}
			catch (Exception ex) when (IsIoFailure(ex))
			{
				this._logger.FileNotWritten(ex, path);
				await this._dialogs.ShowErrorAsync(ApplicationTitle, Loc.Get("Str.MessageEditor.Msg.SaveFailed") + Environment.NewLine + ex.Message);
				return false;
			}
			finally
			{
				this.IsBusy = false;
			}

			this._stamp = this._platform.GetFileStamp(path);
			this.ExternalChange = ExternalChangeKind.None;
			this._savedRevision = savingRevision;
			this.IsModified = this._revision != this._savedRevision;
			if (!this._isModified)
			{
				foreach (MessageRowViewModel row in this._rows)
				{
					row.ClearChangedFlags();
				}
			}

			this.StatusMessage = Loc.Format("Str.MessageEditor.Status.Saved", Path.GetFileName(path));
			this.Target?.MessagesUpdated(path, document);
			return true;
		}

		private async Task ReloadForEncodingAsync(Boolean previousValue)
		{
			String? path = this._filePath;
			if (path is null)
			{
				return;
			}

			Boolean proceed = !this._isModified
				|| await this._dialogs.ConfirmAsync(ApplicationTitle, Loc.Get("Str.MessageEditor.Msg.EncodingReload"), Loc.Get("Str.MessageEditor.Btn.Reload"), Loc.Get("Str.Common.Cancel"));
			Int32 index = this.SelectedRow is null ? 0 : this._rows.IndexOf(this.SelectedRow);
			if (proceed && await this.LoadFileAsync(path))
			{
				if (this._rows.Count > 0)
				{
					this.RequestFocus(this._rows[Math.Clamp(index, 0, this._rows.Count - 1)], this.CurrentColumn, false);
				}

				return;
			}

			this._suppressEncodingReload = true;
			try
			{
				this.UseOem866 = previousValue;
			}
			finally
			{
				this._suppressEncodingReload = false;
			}
		}

		private String? GetInitialDirectory()
		{
			if (this._filePath is not null)
			{
				return Path.GetDirectoryName(this._filePath);
			}

			String? dialogDirectory = this._settings.Effective.ToMessageFileOptions().DialogDirectory;
			return dialogDirectory is not null && Directory.Exists(dialogDirectory) ? dialogDirectory : null;
		}

		private void AddRecent(String path)
		{
			this._settings.Recent.AddMessage(path);
			this.RefreshRecent();
		}

		private void RefreshRecent()
		{
			this.RecentFiles.Clear();
			foreach (String path in this._settings.Recent.Messages.AsEnumerable().Reverse())
			{
				this.RecentFiles.Add(path);
			}
		}

		private void LoadBlank()
		{
			this.FilePath = null;
			this._isNewDocument = false;
			this._stamp = null;
			this.ExternalChange = ExternalChangeKind.None;
			this.ResetRows(Array.Empty<MessageEntry>());
			this.OnPropertyChanged(nameof(this.DisplayName));
			this.OnPropertyChanged(nameof(this.WindowTitle));
		}
	}
}
