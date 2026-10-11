namespace FalloutScriptStudio.Workspace
{
	using System;
	using System.IO;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.IO;
	using FalloutScriptStudio.Core.TextTools;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Services;

	/// <summary>Opening and creating documents.</summary>
	public sealed partial class WorkspaceService
	{
		public Task<DocumentViewModel?> OpenAsync(String filePath, Int32 line = -1, Int32 column = -1, Int32 selectLength = -1)
		{
			return this.OpenAsync(filePath, new OpenDocumentOptions { Line = line, Column = column, SelectLength = selectLength, AddToRecent = false });
		}

		public async Task<DocumentViewModel?> OpenAsync(String filePath, OpenDocumentOptions options)
		{
			ArgumentException.ThrowIfNullOrEmpty(filePath);
			ArgumentNullException.ThrowIfNull(options);

			String path = DocumentPaths.GetFullPath(filePath);
			if (!FileAssociationService.IsOpenable(path))
			{
				await this._dialogs.ShowErrorAsync(Loc.Get("Str.Shell.Dlg.FileNotAllowedTitle"), Loc.Get("Str.Shell.Dlg.FileNotAllowed"));
				return null;
			}

			Boolean exists = File.Exists(path);
			if (options.AddToRecent)
			{
				this.AddToRecent(path);
			}

			if (!exists)
			{
				await this.HandleMissingFileAsync(path, options.FromRecentList);
				return null;
			}

			String extension = Path.GetExtension(path).ToLowerInvariant();
			if (extension == ".int")
			{
				return await this.OpenCompiledAsync(path, options);
			}

			DocumentViewModel? existing = this.FindDocument(path);
			if (existing is not null)
			{
				if (options.Activate)
				{
					this.ActiveDocument = existing;
				}

				if (!options.PromptIfAlreadyOpen
					|| !await this._dialogs.ConfirmAsync(Loc.Get("Str.Shell.Dlg.FileAlreadyOpenTitle"), Loc.Get("Str.Shell.Dlg.FileAlreadyOpen")))
				{
					DocumentNavigation.Navigate(existing, options);
					return existing;
				}

				return await this.OpenCopyAsync(path, options);
			}

			DecodedText decoded;
			try
			{
				decoded = await this._files.ReadAsync(path);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				await this._dialogs.ShowErrorAsync(Loc.Get("Str.Shell.Dlg.CannotOpenTitle"), ex.Message);
				return null;
			}

			DocumentViewModel document = DocumentFactory.Create(path, this.FormatOnOpen(path, decoded.Text), decoded.Encoding);
			document.LastWriteTimeUtc = File.GetLastWriteTimeUtc(path);
			this.AddDocument(document, options.Activate);
			this.FinishOpen(document, options);
			return document;
		}

		public DocumentViewModel CreateNew(String? text = null, DocumentKind kind = DocumentKind.Script)
		{
			String content = text ?? this._files.ReadDefaultTemplate();
			DocumentViewModel document = DocumentFactory.Create(null, DocumentTextCodec.NormalizeNewLines(content), TextEncodings.Ansi);
			document.Kind = kind;
			document.IsModified = true;
			this.AddDocument(document, true);
			if (document.IsParsable)
			{
				_ = this._parser.ParseAsync(document, true, true);
			}

			return document;
		}

		public async Task<DocumentViewModel?> NewFromTemplateAsync(String templatePath)
		{
			ArgumentException.ThrowIfNullOrEmpty(templatePath);
			try
			{
				DecodedText decoded = await this._files.ReadAsync(templatePath);
				return this.CreateNew(decoded.Text);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				await this._dialogs.ShowErrorAsync(Loc.Get("Str.Shell.Dlg.CannotOpenTemplateTitle"), ex.Message);
				return null;
			}
		}

		public async Task<DocumentViewModel?> OpenFromArchiveAsync(String scriptFileName)
		{
			String? extracted = await this._archives.ExtractAsync(scriptFileName);
			return extracted is null ? null : await this.OpenAsync(extracted, new OpenDocumentOptions { AddToRecent = false });
		}

		/// <summary>
		/// Formats the text of a script or header when the "Auto Formatting Code" option is on (legacy <c>formatCodeToolStripMenuItem</c>).
		/// The document is not marked as modified by it, as in the legacy editor.
		/// </summary>
		private String FormatOnOpen(String path, String text)
		{
			if (!this._settings.Settings.Editor.FormatCodeOnOpen)
			{
				return text;
			}

			String extension = Path.GetExtension(path);
			if (!String.Equals(extension, ".ssl", StringComparison.OrdinalIgnoreCase) && !String.Equals(extension, ".h", StringComparison.OrdinalIgnoreCase))
			{
				return text;
			}

			return new CodeFormatter().Format(text);
		}

		private void AddToRecent(String path)
		{
			if (String.Equals(Path.GetExtension(path), ".msg", StringComparison.OrdinalIgnoreCase))
			{
				this._settings.Recent.AddMessage(path);
			}
			else
			{
				this._settings.AddRecentScript(path);
			}
		}

		private async Task HandleMissingFileAsync(String path, Boolean fromRecent)
		{
			if (fromRecent)
			{
				if (await this._dialogs.ConfirmAsync(Loc.Get("Str.Shell.Dlg.OpenFileTitle"), Loc.Format("Str.Shell.Dlg.RecentMissing", path)))
				{
					this._settings.Recent.RemoveScript(path);
					this._settings.Recent.RemoveMessage(path);
				}
			}
			else
			{
				await this._dialogs.ShowErrorAsync(Loc.Get("Str.Shell.Dlg.OpenFileTitle"), Loc.Format("Str.Shell.Dlg.FileNotFound", path));
			}
		}

		private void FinishOpen(DocumentViewModel document, OpenDocumentOptions options)
		{
			if (document.IsParsable)
			{
				_ = this.ParseFirstAsync(document);
			}

			if (options.Line > 0)
			{
				DocumentNavigation.Navigate(document, options);
			}
			else if (this._settings.Settings.Editor.StoreLastPosition && document.Kind == DocumentKind.Script && document.FilePath is not null)
			{
				Int32 remembered = this._settings.ScriptPositions.Get(document.FileName);
				if (remembered > 0)
				{
					document.ViewState.RequestGoTo(Math.Min(remembered, document.TextDocument.LineCount));
				}
			}
		}

		private async Task ParseFirstAsync(DocumentViewModel document)
		{
			await this._parser.ParseAsync(document, true, true);
			MessageSettings messages = this._settings.Settings.Messages;
			if (document.Kind == DocumentKind.Script && document.FilePath is not null && messages.AutoOpenMessageFiles)
			{
				if (messages.OpenEditorAutomatically)
				{
					await this.OpenMessageEditorAsync(document);
				}
				else if (await this.AssociateMessageFileAsync(document, false, false) is { } messagePath)
				{
					await this.OpenAsync(messagePath, new OpenDocumentOptions { AddToRecent = false, Activate = false });
				}
			}
		}

		private async Task<DocumentViewModel?> OpenCopyAsync(String path, OpenDocumentOptions options)
		{
			DecodedText decoded = await this._files.ReadAsync(path);
			DocumentViewModel copy = DocumentFactory.Create(null, this.FormatOnOpen(path, decoded.Text), decoded.Encoding);
			copy.Kind = DocumentViewModel.KindFromPath(path);
			copy.IsModified = true;
			this.AddDocument(copy, options.Activate);
			if (copy.IsParsable)
			{
				_ = this._parser.ParseAsync(copy, true, true);
			}

			return copy;
		}

		private async Task<DocumentViewModel?> OpenCompiledAsync(String path, OpenDocumentOptions options)
		{
			DecompiledScript? script = await this._decompiler.DecompileAsync(path, options);
			if (script is null)
			{
				return null;
			}

			DocumentViewModel document;
			if (options.TemporaryDecompile)
			{
				document = DocumentFactory.Create(script.OutputFilePath, script.Text, script.Encoding);
				document.LastWriteTimeUtc = File.GetLastWriteTimeUtc(script.OutputFilePath);
			}
			else if (script.TargetPath is not null)
			{
				document = DocumentFactory.Create(script.TargetPath, script.Text, script.Encoding);
				document.LastWriteTimeUtc = File.GetLastWriteTimeUtc(script.TargetPath);
				this.AddToRecent(script.TargetPath);
			}
			else
			{
				document = DocumentFactory.Create(null, script.Text, script.Encoding);
				document.Kind = DocumentKind.Script;
				document.IsModified = true;
			}

			this.AddDocument(document, options.Activate);
			if (document.IsParsable)
			{
				_ = this.ParseFirstAsync(document);
			}

			return document;
		}
	}
}
