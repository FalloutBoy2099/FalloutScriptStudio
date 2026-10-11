namespace FalloutScriptStudio.ViewModels.Main
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.IO;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Workspace;

	/// <summary>File menu commands: new, open, recent, save, close.</summary>
	public sealed partial class MainViewModel
	{
		/// <summary>Gets or sets a value indicating whether scripts are saved as UTF-8.</summary>
		public Boolean SaveAsUtf8
		{
			get => this._settings.Settings.Editor.SaveScriptsAsUtf8;
			set => this.SetSetting(settings => settings.Editor.SaveScriptsAsUtf8 = value, nameof(this.SaveAsUtf8));
		}

		/// <summary>Creates a new script from the default template.</summary>
		[RelayCommand]
		public void NewScript()
		{
			this._workspace.CreateNew();
		}

		/// <summary>Creates a new script from a template.</summary>
		[RelayCommand]
		public async Task NewFromTemplateAsync(TemplateItem? template)
		{
			if (template is not null)
			{
				await this._workspace.NewFromTemplateAsync(template.Path);
			}
		}

		/// <summary>Asks for files and opens them.</summary>
		[RelayCommand]
		public async Task OpenAsync()
		{
			String? initial = this._settings.Settings.Paths.SolutionFolder;
			foreach (String path in this._dialogs.PickOpenFiles(Loc.Get("Str.Shell.Dlg.OpenTitle"), Loc.Get("Str.Shell.Filter.Open"), initial))
			{
				await this._workspace.OpenAsync(path, new OpenDocumentOptions { PromptIfAlreadyOpen = true });
			}

			this.RefreshRecentFiles();
		}

		/// <summary>Opens files by path (drag and drop, command line).</summary>
		public async Task OpenFilesAsync(IEnumerable<String> paths)
		{
			List<String> files = paths.ToList();
			await this._workspace.GuardAsync(() => this._workspace.OpenDroppedFilesAsync(files));
			this.RefreshRecentFiles();
		}

		/// <summary>Opens a file of the recent list.</summary>
		[RelayCommand]
		public async Task OpenRecentAsync(String? path)
		{
			if (!String.IsNullOrEmpty(path))
			{
				await this._workspace.OpenAsync(path, new OpenDocumentOptions { PromptIfAlreadyOpen = true, FromRecentList = true });
				this.RefreshRecentFiles();
			}
		}

		/// <summary>Clears the recent files list after confirmation.</summary>
		[RelayCommand]
		public async Task ClearRecentAsync()
		{
			if (this.RecentFiles.Count == 0)
			{
				return;
			}

			if (await this._dialogs.ConfirmAsync(Loc.Get("Str.Shell.Dlg.RecentTitle"), Loc.Get("Str.Shell.Dlg.ClearRecentPrompt")))
			{
				this._settings.Recent.ClearScripts();
				this.RefreshRecentFiles();
			}
		}

		/// <summary>Extracts a script from a game archive and opens it.</summary>
		[RelayCommand]
		public async Task OpenFromArchiveAsync()
		{
			String? name = await this._dialogs.PromptAsync(Loc.Get("Str.Shell.Dlg.ArchiveTitle"), Loc.Get("Str.Shell.Dlg.ArchivePrompt"));
			if (!String.IsNullOrWhiteSpace(name))
			{
				await this._workspace.OpenFromArchiveAsync(name.Trim());
			}
		}

		/// <summary>Saves the active document.</summary>
		[RelayCommand]
		public async Task SaveAsync()
		{
			if (this._workspace.ActiveDocument is { } document)
			{
				await this._workspace.SaveAsync(document);
			}
		}

		/// <summary>Saves all modified documents.</summary>
		[RelayCommand]
		public async Task SaveAllAsync()
		{
			await this._workspace.SaveAllAsync();
		}

		/// <summary>Saves the active document under a new name.</summary>
		[RelayCommand]
		public async Task SaveAsAsync()
		{
			if (this._workspace.ActiveDocument is { } document)
			{
				await this._workspace.SaveAsAsync(document);
			}
		}

		/// <summary>Saves the active script as a template for the New menu.</summary>
		[RelayCommand]
		public async Task SaveAsTemplateAsync()
		{
			if (this._workspace.ActiveDocument is not { Kind: DocumentKind.Script } document)
			{
				return;
			}

			String directory = this._settings.Paths.TemplatesDirectory;
			Directory.CreateDirectory(directory);
			String? path = this._dialogs.PickSaveFile(Loc.Get("Str.Shell.Dlg.SaveTemplateTitle"), Loc.Get("Str.Shell.Filter.Template"), directory, "template.ssl");
			if (path is null)
			{
				return;
			}

			try
			{
				await File.WriteAllTextAsync(Path.Combine(directory, Path.GetFileName(path)), document.TextDocument.Text, TextEncodings.Ansi);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				await this._dialogs.ShowErrorAsync(Loc.Get("Str.Shell.Dlg.SaveTemplateError"), ex.Message);
				return;
			}

			this.LoadTemplates();
		}

		/// <summary>Closes a tab (the active one when no tab is given).</summary>
		[RelayCommand]
		public async Task CloseDocumentAsync(DocumentViewModel? document)
		{
			DocumentViewModel? target = document ?? this._workspace.ActiveDocument;
			if (target is not null)
			{
				await this._workspace.CloseAsync(target);
			}
		}

		/// <summary>Closes all tabs.</summary>
		[RelayCommand]
		public async Task CloseAllAsync()
		{
			await this._workspace.CloseAllAsync();
		}

		/// <summary>Closes all tabs except one.</summary>
		[RelayCommand]
		public async Task CloseAllButThisAsync(DocumentViewModel? document)
		{
			await this._workspace.CloseAllAsync(document ?? this._workspace.ActiveDocument);
		}

		/// <summary>Shows the file of a tab in Explorer.</summary>
		[RelayCommand]
		public void OpenContainingFolder(DocumentViewModel? document)
		{
			if (document?.FilePath is { } path)
			{
				this._shell.RevealInExplorer(path);
			}
		}

		/// <summary>Copies the full path of a tab's file to the clipboard.</summary>
		[RelayCommand]
		public void CopyDocumentPath(DocumentViewModel? document)
		{
			if (document?.FilePath is { } path)
			{
				this._shell.SetClipboardText(path);
			}
		}

		/// <summary>Opens the file of a tab in the external editor.</summary>
		[RelayCommand]
		public async Task OpenInExternalEditorAsync(DocumentViewModel? document)
		{
			if (document?.FilePath is not { } path)
			{
				return;
			}

			String? editor = this._settings.Settings.Paths.ExternalEditorPath;
			if (editor is null || !File.Exists(editor))
			{
				editor = this._dialogs.PickOpenFile(Loc.Get("Str.Shell.Dlg.SelectEditorTitle"), Loc.Get("Str.Shell.Filter.Executable"), this._settings.Paths.ProgramDirectory);
				if (editor is null)
				{
					return;
				}

				this._settings.Update(settings => settings.Paths.ExternalEditorPath = editor);
			}

			if (!this._shell.StartProgram(editor, "\"" + path + "\""))
			{
				await this._dialogs.ShowErrorAsync(Loc.Get("Str.Shell.Dlg.ExternalEditorTitle"), Loc.Format("Str.Shell.Dlg.ExternalEditorFailed", editor));
			}
		}

		/// <summary>Selects the next tab (wraps around).</summary>
		[RelayCommand]
		public void NextDocument()
		{
			this.SelectRelativeDocument(1);
		}

		/// <summary>Selects the previous tab (wraps around).</summary>
		[RelayCommand]
		public void PreviousDocument()
		{
			this.SelectRelativeDocument(-1);
		}

		/// <summary>Checks the open files for changes made by other programs.</summary>
		public Task CheckExternalChangesAsync()
		{
			return this._workspace.CheckExternalChangesAsync();
		}

		/// <summary>Moves a tab to another position (drag and drop in the tab strip).</summary>
		public void MoveDocument(Int32 oldIndex, Int32 newIndex)
		{
			this._workspace.MoveDocument(oldIndex, newIndex);
		}

		/// <summary>Closes the main window.</summary>
		[RelayCommand]
		public void Exit()
		{
			this.ExitRequested?.Invoke(this, EventArgs.Empty);
		}

		private void SelectRelativeDocument(Int32 direction)
		{
			Int32 count = this._workspace.Documents.Count;
			if (count < 2 || this._workspace.ActiveDocument is not { } active)
			{
				return;
			}

			Int32 index = (this._workspace.Documents.IndexOf(active) + direction + count) % count;
			this._workspace.ActiveDocument = this._workspace.Documents[index];
		}

		private void SetSetting(Action<AppSettings> change, String propertyName)
		{
			this._settings.Update(change);
			this.OnPropertyChanged(propertyName);
		}
	}
}
