namespace FalloutScriptStudio.Workspace
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Localization;

	/// <summary>Saving, reloading and external change detection.</summary>
	public sealed partial class WorkspaceService
	{
		public async Task<Boolean> SaveAsync(DocumentViewModel document)
		{
			ArgumentNullException.ThrowIfNull(document);
			if (document.FilePath is null)
			{
				return await this.SaveAsAsync(document);
			}

			return await this.WriteAsync(document, document.FilePath);
		}

		public async Task<Boolean> SaveAsAsync(DocumentViewModel document)
		{
			ArgumentNullException.ThrowIfNull(document);

			String? initialDirectory = document.FilePath is null ? this._settings.Settings.Paths.SolutionFolder : Path.GetDirectoryName(document.FilePath);
			String? path = this._dialogs.PickSaveFile(Loc.Get("Str.Shell.Dlg.SaveFileTitle"), DocumentFileService.BuildSaveFilter(document.Kind), initialDirectory, document.FilePath is null ? "NewScript.ssl" : document.FileName);
			if (path is null)
			{
				return false;
			}

			DocumentViewModel? other = this.FindDocument(path);
			if (other is not null && !ReferenceEquals(other, document))
			{
				await this._dialogs.ShowWarningAsync(Loc.Get("Str.Shell.Dlg.SaveAsTitle"), Loc.Get("Str.Shell.Dlg.SaveAsAlreadyOpen"));
				return false;
			}

			if (!await this.WriteAsync(document, path))
			{
				return false;
			}

			document.FilePath = path;
			document.Kind = DocumentViewModel.KindFromPath(path);
			this.AddToRecent(path);
			if (document.IsParsable)
			{
				await this._parser.ParseAsync(document, true, true);
			}

			return true;
		}

		public async Task<Boolean> SaveAllAsync()
		{
			Boolean success = true;
			foreach (DocumentViewModel document in this.Documents.Where(d => d.IsModified).ToList())
			{
				success &= await this.SaveAsync(document);
			}

			return success;
		}

		public async Task<Boolean> ReloadAsync(DocumentViewModel document)
		{
			ArgumentNullException.ThrowIfNull(document);
			if (document.FilePath is null || !File.Exists(document.FilePath))
			{
				return false;
			}

			DecodedText decoded;
			try
			{
				decoded = await this._files.ReadAsync(document.FilePath);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				await this._dialogs.ShowErrorAsync(Loc.Get("Str.Shell.Dlg.CannotReloadTitle"), ex.Message);
				return false;
			}

			Int32 caretLine = document.Editor?.CaretLine ?? document.ViewState.CaretLine;
			document.TextDocument.Text = decoded.Text;
			document.TextDocument.UndoStack.ClearAll();
			document.TextDocument.UndoStack.MarkAsOriginalFile();
			document.Encoding = decoded.Encoding;
			document.LastWriteTimeUtc = File.GetLastWriteTimeUtc(document.FilePath);
			document.IsModified = false;
			if (caretLine > 1)
			{
				DocumentNavigation.Navigate(document, new OpenDocumentOptions { Line = Math.Min(caretLine, document.TextDocument.LineCount) });
			}

			if (document.IsParsable)
			{
				await this._parser.ParseAsync(document, true);
			}

			await this.RefreshScriptsOfMessageFileAsync(document.FilePath, document.Kind);
			return true;
		}

		public async Task CheckExternalChangesAsync()
		{
			await this._externalChanges.CheckAsync(this.Documents, this.ReloadAsync);
			if (this.ActiveDocument is { Kind: DocumentKind.Script, MessageFilePath: not null } script)
			{
				await this.RefreshMessageTextsAsync(script);
			}
		}

		public void RememberDocumentState(DocumentViewModel document)
		{
			ArgumentNullException.ThrowIfNull(document);
			if (!this._settings.Settings.Editor.StoreLastPosition || document.FilePath is null || document.Kind != DocumentKind.Script)
			{
				return;
			}

			document.Editor?.CommitViewState();
			Int32 line = document.Editor?.CaretLine ?? document.ViewState.CaretLine;
			this._settings.ScriptPositions.Set(document.FileName, line);
		}

		private async Task<Boolean> WriteAsync(DocumentViewModel document, String path)
		{
			if (!await this._files.WriteAsync(document, path))
			{
				return false;
			}

			if (this._files.IsGlobalHeader(path))
			{
				await this._parser.ReloadGlobalMacrosAsync();
			}

			// The kind of the target path counts, not the one the document had before "Save as".
			await this.RefreshScriptsOfMessageFileAsync(path, DocumentViewModel.KindFromPath(path));
			return true;
		}
	}
}
