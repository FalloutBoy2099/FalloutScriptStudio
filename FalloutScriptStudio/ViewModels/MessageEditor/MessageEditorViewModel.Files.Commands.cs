namespace FalloutScriptStudio.ViewModels.MessageEditor
{
	using System;
	using System.ComponentModel;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.Messages;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Services.MessageEditor;

	/// <summary>The file commands: new, open, recent, save, reload, external editor and open as text.</summary>
	public sealed partial class MessageEditorViewModel
	{
		/// <summary>Ignores the change made by another program and keeps the editor content.</summary>
		[RelayCommand]
		private void IgnoreExternalChange()
		{
			if (this._filePath is not null)
			{
				this._stamp = this._platform.GetFileStamp(this._filePath);
			}

			this.ExternalChange = ExternalChangeKind.None;
		}

		/// <summary>Creates a new document with the standard first two messages (name and description).</summary>
		[RelayCommand]
		private async Task NewAsync()
		{
			this.EndEdit();
			if (!await this.ConfirmDiscardAsync())
			{
				return;
			}

			this.FilePath = null;
			this._isNewDocument = true;
			this._stamp = null;
			this.ExternalChange = ExternalChangeKind.None;
			this.ResetRows(new[]
			{
				MessageEntry.CreateComment("# Look Name"),
				MessageEntry.CreateMessage(100),
				MessageEntry.CreateComment("# Description"),
				MessageEntry.CreateMessage(101),
			});
			this.OnPropertyChanged(nameof(this.DisplayName));
			this.OnPropertyChanged(nameof(this.WindowTitle));
			this.StatusMessage = Loc.Get("Str.MessageEditor.Status.NewFile");
		}

		/// <summary>Lets the user pick a message file and opens it.</summary>
		[RelayCommand]
		private async Task OpenAsync()
		{
			this.EndEdit();
			if (!await this.ConfirmDiscardAsync())
			{
				return;
			}

			String? path = this._dialogs.PickOpenFile(Loc.Get("Str.MessageEditor.Dlg.OpenFile"), FileFilter, this.GetInitialDirectory());
			if (path is not null)
			{
				await this.LoadFileAsync(path);
			}
		}

		/// <summary>Opens a file from the recent files list.</summary>
		/// <param name="path">The file.</param>
		[RelayCommand]
		private async Task OpenRecentAsync(String? path)
		{
			if (String.IsNullOrEmpty(path))
			{
				return;
			}

			this.EndEdit();
			if (!await this.ConfirmDiscardAsync())
			{
				return;
			}

			if (this._platform.FileExists(path))
			{
				await this.LoadFileAsync(path);
				return;
			}

			if (await this._dialogs.ConfirmAsync(ApplicationTitle, Loc.Get("Str.MessageEditor.Msg.RecentNotFound") + Environment.NewLine + Loc.Get("Str.MessageEditor.Msg.RecentRemove"), Loc.Get("Str.Common.Remove"), Loc.Get("Str.MessageEditor.Btn.Keep")))
			{
				this._settings.Recent.RemoveMessage(path);
				this.RefreshRecent();
			}
		}

		/// <summary>Saves the file (asks for a name first if the document was never saved).</summary>
		[RelayCommand(CanExecute = nameof(CanSave))]
		private async Task SaveAsync()
		{
			await this.SaveDocumentAsync();
		}

		/// <summary>Saves the document under a new name.</summary>
		[RelayCommand]
		private async Task SaveAsAsync()
		{
			await this.SaveDocumentAsAsync();
		}

		/// <summary>Reads the file from disk again, discarding the changes made in the editor.</summary>
		[RelayCommand(CanExecute = nameof(HasFile))]
		private async Task ReloadAsync()
		{
			this.EndEdit();
			String? path = this._filePath;
			if (path is null)
			{
				return;
			}

			if (this._isModified
				&& !await this._dialogs.ConfirmAsync(ApplicationTitle, Loc.Get("Str.MessageEditor.Msg.ReloadDiscards"), Loc.Get("Str.MessageEditor.Btn.Reload"), Loc.Get("Str.Common.Cancel")))
			{
				return;
			}

			Int32 index = this.SelectedRow is null ? 0 : this._rows.IndexOf(this.SelectedRow);
			if (await this.LoadFileAsync(path) && this._rows.Count > 0)
			{
				this.RequestFocus(this._rows[Math.Clamp(index, 0, this._rows.Count - 1)], this.CurrentColumn, false);
			}
		}

		/// <summary>Opens the message file in the external text editor chosen in the settings.</summary>
		[RelayCommand(CanExecute = nameof(HasFile))]
		private async Task OpenInExternalEditorAsync()
		{
			this.EndEdit();
			String? file = this._filePath;
			if (file is null)
			{
				return;
			}

			if (this._isModified)
			{
				YesNoCancel answer = await this._dialogs.AskYesNoCancelAsync(
					ApplicationTitle,
					Loc.Get("Str.MessageEditor.Msg.SaveBeforeExternal"),
					Loc.Get("Str.Common.Save"),
					Loc.Get("Str.MessageEditor.Btn.OpenWithoutSaving"));
				if (answer == YesNoCancel.Cancel || (answer == YesNoCancel.Yes && !await this.SaveDocumentAsync()))
				{
					return;
				}
			}

			String? editor = this._settings.Settings.Paths.ExternalEditorPath;
			if (editor is null || !this._platform.FileExists(editor))
			{
				editor = this._dialogs.PickOpenFile(Loc.Get("Str.MessageEditor.Dlg.SelectEditor"), Loc.Get("Str.MessageEditor.Filter.Programs") + "|*.exe|" + Loc.Get("Str.MessageEditor.Filter.AllFiles") + "|*.*", this._settings.Paths.ProgramDirectory);
				if (editor is null)
				{
					return;
				}

				this._settings.Update(settings => settings.Paths.ExternalEditorPath = editor);
			}

			try
			{
				this._platform.StartEditor(editor, file);
			}
			catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
			{
				this._logger.ExternalEditorNotStarted(ex, editor);
				await this._dialogs.ShowErrorAsync(ApplicationTitle, Loc.Get("Str.MessageEditor.Msg.EditorStartFailed") + Environment.NewLine + ex.Message);
			}
		}

		/// <summary>Closes the editor and opens the file as plain text in the script editor.</summary>
		[RelayCommand(CanExecute = nameof(CanOpenAsText))]
		private async Task OpenAsTextAsync()
		{
			this.EndEdit();
			String? file = this._filePath;
			if (file is null || this.Target is null || !await this.ConfirmDiscardAsync())
			{
				return;
			}

			if (this.Target.TryOpenAsText(file))
			{
				this.SuppressClosePrompt = true;
				this.CloseRequested?.Invoke(this, EventArgs.Empty);
			}
			else
			{
				await this._dialogs.ShowInfoAsync(ApplicationTitle, Loc.Get("Str.MessageEditor.Msg.CannotOpenAsText"));
			}
		}
	}
}