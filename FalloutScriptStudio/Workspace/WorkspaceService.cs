namespace FalloutScriptStudio.Workspace
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Services.ScriptTools;

	/// <summary>
	/// The set of open documents and the application-level operations on them: opening (including decompilation of compiled
	/// scripts), saving, closing, external change detection, navigation, message file association and procedure operations.
	/// This file holds the document collection and the active document; the operations are split by responsibility into
	/// <c>WorkspaceService.*.cs</c> and delegate to the collaborators injected here.
	/// </summary>
	public sealed partial class WorkspaceService : IWorkspaceManager
	{
		private readonly ISettingsService _settings;
		private readonly ParseCoordinator _parser;
		private readonly IDialogService _dialogs;
		private readonly IOutputSink _output;
		private readonly ISymbolRenameService _renamer;
		private readonly DocumentFileService _files;
		private readonly ScriptDecompilationService _decompiler;
		private readonly ArchiveScriptExtractor _archives;
		private readonly ExternalChangeDetector _externalChanges;
		private readonly MessageAssociationService _messages;
		private readonly ProcedureOperations _procedures;
		private readonly ReferenceResultsPresenter _references;
		private readonly IScriptToolService _scriptTools;

		private DocumentViewModel? _active;
		private DocumentViewModel? _previousActive;

		/// <summary>Initializes the workspace.</summary>
		public WorkspaceService(
			ISettingsService settings,
			ParseCoordinator parser,
			IDialogService dialogs,
			IOutputSink output,
			ISymbolRenameService renamer,
			DocumentFileService files,
			ScriptDecompilationService decompiler,
			ArchiveScriptExtractor archives,
			ExternalChangeDetector externalChanges,
			MessageAssociationService messages,
			ProcedureOperations procedures,
			ReferenceResultsPresenter references,
			IScriptToolService scriptTools)
		{
			ArgumentNullException.ThrowIfNull(settings);
			ArgumentNullException.ThrowIfNull(parser);
			ArgumentNullException.ThrowIfNull(dialogs);
			ArgumentNullException.ThrowIfNull(output);
			ArgumentNullException.ThrowIfNull(renamer);
			ArgumentNullException.ThrowIfNull(files);
			ArgumentNullException.ThrowIfNull(decompiler);
			ArgumentNullException.ThrowIfNull(archives);
			ArgumentNullException.ThrowIfNull(externalChanges);
			ArgumentNullException.ThrowIfNull(messages);
			ArgumentNullException.ThrowIfNull(procedures);
			ArgumentNullException.ThrowIfNull(references);
			ArgumentNullException.ThrowIfNull(scriptTools);
			this._settings = settings;
			this._parser = parser;
			this._dialogs = dialogs;
			this._output = output;
			this._renamer = renamer;
			this._files = files;
			this._decompiler = decompiler;
			this._archives = archives;
			this._externalChanges = externalChanges;
			this._messages = messages;
			this._procedures = procedures;
			this._references = references;
			this._scriptTools = scriptTools;
			this._parser.Parsed += (_, document) => this.DocumentParsed?.Invoke(this, document);
		}

		public event EventHandler? ActiveDocumentChanged;

		public event EventHandler<DocumentViewModel>? DocumentParsed;

		public event EventHandler<DocumentViewModel>? DocumentOpened;

		public event EventHandler<DocumentViewModel>? DocumentClosed;

		public ObservableCollection<DocumentViewModel> Documents { get; } = new ObservableCollection<DocumentViewModel>();

		public DocumentViewModel? ActiveDocument
		{
			get => this._active;
			set
			{
				if (ReferenceEquals(this._active, value))
				{
					return;
				}

				if (this._active is not null)
				{
					this._active.IsActive = false;
					this._previousActive = this._active;
				}

				this._active = value;
				if (value is not null)
				{
					value.IsActive = true;
					if (value.NeedsParse && !this._parser.IsPending(value))
					{
						_ = this._parser.ParseAsync(value, false);
					}

					if (value.Kind == DocumentKind.Script && value.MessageFilePath is not null)
					{
						_ = this.RefreshMessageTextsAsync(value);
					}
				}

				this.ActiveDocumentChanged?.Invoke(this, EventArgs.Empty);
			}
		}

		public DocumentViewModel? FindDocument(String filePath)
		{
			if (String.IsNullOrEmpty(filePath))
			{
				return null;
			}

			String full = DocumentPaths.GetFullPath(filePath);
			return this.Documents.FirstOrDefault(d => d.FilePath is not null && String.Equals(DocumentPaths.GetFullPath(d.FilePath), full, StringComparison.OrdinalIgnoreCase));
		}

		public async Task<Boolean> CloseAsync(DocumentViewModel document)
		{
			ArgumentNullException.ThrowIfNull(document);
			if (!this.Documents.Contains(document))
			{
				return true;
			}

			// Like the legacy editor, the flowchart and the node code editors of a script close with its tab (before the save question,
			// so the node code that is applied is saved too).
			await this._scriptTools.CloseToolWindowsAsync(document);

			if (document.IsModified)
			{
				YesNoCancel answer = await this._dialogs.AskYesNoCancelAsync(Loc.Get("Str.Shell.Dlg.UnsavedTitle"), Loc.Format("Str.Shell.Dlg.UnsavedText", document.FileName));
				if (answer == YesNoCancel.Cancel)
				{
					return false;
				}

				if (answer == YesNoCancel.Yes && (!await this.SaveAsync(document) || document.IsModified))
				{
					return false;
				}
			}

			this.RememberDocumentState(document);
			this._parser.Detach(document);

			Int32 index = this.Documents.IndexOf(document);
			if (ReferenceEquals(this._active, document))
			{
				DocumentViewModel? next = this._previousActive is not null && !ReferenceEquals(this._previousActive, document) && this.Documents.Contains(this._previousActive)
					? this._previousActive
					: null;
				if (next is null && this.Documents.Count > 1)
				{
					next = this.Documents[index < this.Documents.Count - 1 ? index + 1 : index - 1];
				}

				this.Documents.Remove(document);
				this.ActiveDocument = next;
			}
			else
			{
				this.Documents.Remove(document);
			}

			document.IsActive = false;
			this._messages.Forget(document);
			if (ReferenceEquals(this._previousActive, document))
			{
				this._previousActive = null;
			}

			this.DocumentClosed?.Invoke(this, document);
			return true;
		}

		public async Task<Boolean> CloseAllAsync(DocumentViewModel? except = null)
		{
			foreach (DocumentViewModel document in this.Documents.Reverse().ToList())
			{
				if (ReferenceEquals(document, except))
				{
					continue;
				}

				if (!await this.CloseAsync(document))
				{
					return false;
				}
			}

			return true;
		}

		public void MoveDocument(Int32 oldIndex, Int32 newIndex)
		{
			if (oldIndex < 0 || newIndex < 0 || oldIndex >= this.Documents.Count || newIndex >= this.Documents.Count || oldIndex == newIndex)
			{
				return;
			}

			this.Documents.Move(oldIndex, newIndex);
		}

		public Task RequestParseAsync(DocumentViewModel document, Boolean immediate = false)
		{
			return this._parser.ParseAsync(document, immediate);
		}

		public Task ReloadGlobalMacrosAsync()
		{
			return this._parser.ReloadGlobalMacrosAsync();
		}

		public Task RenameSymbolAsync(DocumentViewModel document, IScriptSymbol symbol)
		{
			return this._renamer.RenameAsync(this, document, symbol);
		}

		public IReadOnlyDictionary<String, String> GetOpenDocumentTexts()
		{
			Dictionary<String, String> texts = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
			foreach (DocumentViewModel document in this.Documents)
			{
				if (document.FilePath is not null)
				{
					texts[DocumentPaths.GetFullPath(document.FilePath)] = document.TextDocument.Text;
				}
			}

			return texts;
		}

		public void ReportStatus(String message)
		{
			this._output.ReportStatus(message);
		}

		private void AddDocument(DocumentViewModel document, Boolean activate)
		{
			this.Documents.Add(document);
			this._parser.Attach(document);
			this.DocumentOpened?.Invoke(this, document);
			if (activate)
			{
				this.ActiveDocument = document;
			}
		}
	}
}
