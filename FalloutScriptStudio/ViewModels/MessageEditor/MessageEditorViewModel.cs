namespace FalloutScriptStudio.ViewModels.MessageEditor
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.ComponentModel;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Windows;
	using System.Windows.Data;
	using CommunityToolkit.Mvvm.ComponentModel;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.IO;
	using FalloutScriptStudio.Core.Messages;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Services.MessageEditor;
	using Microsoft.Extensions.Logging;

	/// <summary>
	/// The view model of one Message Editor window: the grid of rows of a <c>.msg</c> file and every command that works on it.
	/// </summary>
	public sealed partial class MessageEditorViewModel : ObservableObject, IMessageRowOwner, IDisposable
	{
		/// <summary>The title used by dialogs and the window caption.</summary>
		public static String ApplicationTitle => Loc.Get("Str.MessageEditor.Title");

		/// <summary>The file dialog filter for message files.</summary>
		public static String FileFilter => Loc.Get("Str.MessageEditor.Filter.MessageFiles") + "|*.msg|" + Loc.Get("Str.MessageEditor.Filter.AllFiles") + "|*.*";

		private readonly IMessageFileService _files;
		private readonly IMessageStructureChecker _checker;
		private readonly ISettingsService _settings;
		private readonly IDialogService _dialogs;
		private readonly IMessageEditorPlatform _platform;
		private readonly ILogger<MessageEditorViewModel> _logger;
		private readonly UndoHistory<EditorSnapshot> _history = new();
		private readonly RangeObservableCollection<MessageRowViewModel> _rows = [];

		private IReadOnlyList<MessageRowViewModel> _selectedRows = Array.Empty<MessageRowViewModel>();
		private Int32 _revision;
		private Int32 _revisionCounter;
		private Int32 _savedRevision;
		private Int32 _suspendUndo;
		private Boolean _isModified;
		private Boolean _applyingSettings;
		private String? _filePath;
		private Boolean _isNewDocument;
		private FileStamp? _stamp;
		private readonly LanguageChangedSubscription _languageSubscription;
		private System.Windows.Threading.DispatcherTimer? _structureCheckTimer;
		private Boolean _disposed;

		/// <summary>
		/// Initializes a new instance of the <see cref="MessageEditorViewModel"/> class.
		/// </summary>
		/// <param name="files">Reads and writes message files.</param>
		/// <param name="checker">Checks the structure of the file text.</param>
		/// <param name="settings">The user settings.</param>
		/// <param name="dialogs">Shows confirmations and file pickers.</param>
		/// <param name="platform">Operating system facilities.</param>
		/// <param name="logger">The logger.</param>
		public MessageEditorViewModel(
			IMessageFileService files,
			IMessageStructureChecker checker,
			ISettingsService settings,
			IDialogService dialogs,
			IMessageEditorPlatform platform,
			ILogger<MessageEditorViewModel> logger)
		{
			ArgumentNullException.ThrowIfNull(files);
			ArgumentNullException.ThrowIfNull(checker);
			ArgumentNullException.ThrowIfNull(settings);
			ArgumentNullException.ThrowIfNull(dialogs);
			ArgumentNullException.ThrowIfNull(platform);
			ArgumentNullException.ThrowIfNull(logger);
			this._files = files;
			this._checker = checker;
			this._settings = settings;
			this._dialogs = dialogs;
			this._platform = platform;
			this._logger = logger;

			this.RowsView = CollectionViewSource.GetDefaultView(this._rows);
			this.RowsView.Filter = this.FilterRow;
			this.ReadSettings();
			this._settings.SettingsChanged += this.OnSettingsChanged;
			this.RefreshRecent();
			this.LoadBlank();
			this._languageSubscription = LanguageChangedSubscription.Subscribe(this, static viewModel => viewModel.OnLanguageChanged());
		}

		/// <summary>Occurs when the view should select rows and maybe start editing a cell.</summary>
		public event EventHandler<RowFocusRequestedEventArgs>? RowFocusRequested;

		/// <summary>Occurs when the view must finish the edit of the current cell before a command changes the rows.</summary>
		public event EventHandler? EditCommitRequested;

		/// <summary>Occurs when the window should close.</summary>
		public event EventHandler? CloseRequested;

		/// <summary>Gets the rows of the grid.</summary>
		public ObservableCollection<MessageRowViewModel> Rows => this._rows;

		/// <summary>Gets the filtered view of <see cref="Rows"/> the grid is bound to.</summary>
		public ICollectionView RowsView { get; }

		/// <summary>Gets the recent message files, newest first.</summary>
		public ObservableCollection<String> RecentFiles { get; } = [];

		/// <summary>Gets the step values that can be added after a comment when numbering a new message.</summary>
		public IReadOnlyList<Int32> CommentSteps { get; } = new[] { 1, 5, 10, 20, 30, 40, 50 };

		/// <summary>Gets the owner the sent lines go to, or <see langword="null"/> for a stand-alone editor.</summary>
		public IMessageLineTarget? Target { get; private set; }

		/// <summary>Gets a value indicating whether the window closes right after a line was sent.</summary>
		public Boolean CloseOnSend { get; private set; }

		/// <summary>Gets a value indicating whether closing must not ask about unsaved changes (the user already decided).</summary>
		public Boolean SuppressClosePrompt { get; private set; }

		/// <summary>Gets the full path of the file being edited, or <see langword="null"/> for an unsaved document.</summary>
		public String? FilePath
		{
			get => this._filePath;
			private set
			{
				if (this.SetProperty(ref this._filePath, value))
				{
					this.OnPropertyChanged(nameof(this.DisplayName));
					this.OnPropertyChanged(nameof(this.WindowTitle));
					this.OnPropertyChanged(nameof(this.PathCaption));
					this.NotifyFileCommands();
				}
			}
		}

		/// <summary>Gets the name of the document shown in the title.</summary>
		public String DisplayName => this._filePath is null ? (this._isNewDocument ? UnsavedFileName : Loc.Get("Str.MessageEditor.DocEmpty")) : Path.GetFileName(this._filePath);

		/// <summary>Gets the text above the grid: the full path of the file, or a generic caption for an unsaved document.</summary>
		public String PathCaption => this._filePath ?? Loc.Get("Str.MessageEditor.PathCaptionUnsaved");

		/// <summary>Gets the caption of the window.</summary>
		public String WindowTitle => this.DisplayName + (this._isModified ? "*" : String.Empty) + " - " + ApplicationTitle;

		/// <summary>Gets a value indicating whether the document has changes that were not saved.</summary>
		public Boolean IsModified
		{
			get => this._isModified;
			private set
			{
				if (this.SetProperty(ref this._isModified, value))
				{
					this.OnPropertyChanged(nameof(this.WindowTitle));
					this.SaveCommand.NotifyCanExecuteChanged();
				}
			}
		}

		/// <summary>Gets a value indicating whether a file operation is running.</summary>
		[ObservableProperty]
		private Boolean _isBusy;

		/// <summary>Gets the current row of the grid.</summary>
		[ObservableProperty]
		private MessageRowViewModel? _selectedRow;

		/// <summary>Gets or sets the column of the current cell.</summary>
		[ObservableProperty]
		private MessageGridColumn _currentColumn = MessageGridColumn.Text;

		/// <summary>Gets or sets a value indicating whether the details pane with the text editor is shown.</summary>
		[ObservableProperty]
		private Boolean _isDetailsVisible;

		/// <summary>Gets or sets a value indicating whether a cell of the grid is in edit mode (set by the view).</summary>
		[ObservableProperty]
		private Boolean _isCellEditing;

		/// <summary>Gets or sets the message shown in the notification bar above the grid (a refused edit); empty when there is none.</summary>
		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(HasNotification))]
		private String _notificationMessage = String.Empty;

		/// <summary>Gets the message of the last completed action, shown in the status bar.</summary>
		[ObservableProperty]
		private String _statusMessage = String.Empty;

		/// <summary>Gets the summary of the structure problems, shown in the status bar.</summary>
		[ObservableProperty]
		private String _issueSummary = Loc.Get("Str.MessageEditor.Issue.None");

		/// <summary>Gets the number of structure errors.</summary>
		[ObservableProperty]
		private Int32 _errorCount;

		/// <summary>Gets the number of structure warnings.</summary>
		[ObservableProperty]
		private Int32 _warningCount;

		/// <summary>Gets the number of structure hints.</summary>
		[ObservableProperty]
		private Int32 _hintCount;

		/// <summary>Gets a value indicating whether the notification bar has a message.</summary>
		public Boolean HasNotification => this.NotificationMessage.Length > 0;

		/// <summary>Gets the rows that are currently selected.</summary>
		public IReadOnlyList<MessageRowViewModel> SelectedRows => this._selectedRows;

		/// <summary>Gets the number of rows that hold a message.</summary>
		public Int32 MessageCount => this._rows.Count(row => row.IsMessage);

		/// <summary>Gets the localized message count for the status bar.</summary>
		public String MessageCountCaption => Loc.Format("Str.MessageEditor.Status.MessageCount", this.MessageCount);

		/// <summary>Gets a value indicating whether the editor is connected to an owner that can receive lines.</summary>
		public Boolean HasTarget => this.Target is not null;

		/// <summary>Gets the name of the script that owns the file, for the window hint; empty when there is none.</summary>
		public String ScriptCaption => this.Target?.ScriptPath is { Length: > 0 } script ? Loc.Format("Str.MessageEditor.ScriptCaption", Path.GetFileName(script)) : String.Empty;

		/// <summary>
		/// Connects the editor to the part of the application that opened it.
		/// </summary>
		/// <param name="target">The owner of the sent lines, or <see langword="null"/>.</param>
		/// <param name="closeOnSend">Whether the window closes right after a line was sent.</param>
		public void AttachTarget(IMessageLineTarget? target, Boolean closeOnSend)
		{
			this.Target = target;
			this.CloseOnSend = closeOnSend;
			this.OnPropertyChanged(nameof(this.Target));
			this.OnPropertyChanged(nameof(this.HasTarget));
			this.OnPropertyChanged(nameof(this.ScriptCaption));
			this.RefreshTargetState();
		}

		/// <summary>Gets a value indicating whether closing the window has to ask what to do with unsaved changes.</summary>
		public Boolean NeedsClosePrompt => this._isModified && !this.SuppressClosePrompt;

		/// <summary>Finishes the edit that is in progress in the grid so its value is part of the document.</summary>
		public void CommitPendingEdit()
		{
			this.EndEdit();
		}

		/// <summary>Makes the window close without asking about unsaved changes (the caller already asked).</summary>
		public void SkipClosePrompt()
		{
			this.SuppressClosePrompt = true;
		}

		/// <summary>Re-reads whether the owner can accept lines; called when the window is activated.</summary>
		public void RefreshTargetState()
		{
			this.SendLineCommand.NotifyCanExecuteChanged();
			this.OpenAsTextCommand.NotifyCanExecuteChanged();
		}

		/// <summary>
		/// Called by the view whenever the selection or the current cell of the grid changes.
		/// </summary>
		/// <param name="current">The current row, or <see langword="null"/>.</param>
		/// <param name="selected">All selected rows.</param>
		/// <param name="column">The column of the current cell.</param>
		public void UpdateSelection(MessageRowViewModel? current, IReadOnlyList<MessageRowViewModel> selected, MessageGridColumn column)
		{
			ArgumentNullException.ThrowIfNull(selected);
			this._selectedRows = selected;
			this.CurrentColumn = column;
			this.SelectedRow = current;
			this.OnPropertyChanged(nameof(this.SelectedRows));
			this.SendLineCommand.NotifyCanExecuteChanged();
			this.DuplicateRowsCommand.NotifyCanExecuteChanged();
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			if (this._disposed)
			{
				return;
			}

			this._disposed = true;
			this._structureCheckTimer?.Stop();
			this._structureCheckTimer = null;
			this._languageSubscription.Dispose();
			this._settings.SettingsChanged -= this.OnSettingsChanged;
		}

		void IMessageRowOwner.RowEditing(MessageRowViewModel row)
		{
			if (this._suspendUndo == 0)
			{
				this.RecordUndo();
			}
		}

		void IMessageRowOwner.RowEdited(MessageRowViewModel row)
		{
			if (this._suspendUndo == 0)
			{
				this.NotifyDocumentChanged();
				this.NotificationMessage = String.Empty;
			}
		}

		void IMessageRowOwner.RowRejected(String message)
		{
			this.NotificationMessage = message;
		}

		/// <summary>Hides the notification bar.</summary>
		[RelayCommand]
		private void DismissNotification()
		{
			this.NotificationMessage = String.Empty;
		}

		private void OnLanguageChanged()
		{
			if (Application.Current is null || this._disposed)
			{
				return;
			}

			this.OnPropertyChanged(nameof(this.DisplayName));
			this.OnPropertyChanged(nameof(this.WindowTitle));
			this.OnPropertyChanged(nameof(this.PathCaption));
			this.OnPropertyChanged(nameof(this.ScriptCaption));
			this.OnPropertyChanged(nameof(this.MessageCountCaption));
			this.OnPropertyChanged(nameof(this.ExternalChangeMessage));
			this.RunStructureCheck();
			this.UpdateSearchMatches();
		}

		private static Boolean IsIoFailure(Exception ex)
		{
			return ex is IOException or UnauthorizedAccessException or NotSupportedException or System.Security.SecurityException;
		}

		/// <summary>
		/// Replaces all rows of the grid and forgets the undo history.
		/// </summary>
		/// <param name="entries">The new content.</param>
		private void ResetRows(IEnumerable<MessageEntry> entries)
		{
			List<MessageRowViewModel> rows = entries.Select(entry => new MessageRowViewModel(entry, this)).ToList();
			if (rows.Count == 0)
			{
				rows.Add(this.CreateBlankRow());
			}

			this._history.Clear();
			this._revision = 0;
			this._revisionCounter = 0;
			this._savedRevision = 0;
			this.IsModified = false;
			this.SelectedRow = null;
			this._rows.ReplaceAll(rows);
			this.AfterRowsChanged();
			this.RequestFocus(rows[0], this.CurrentColumn, false);
		}

		private MessageRowViewModel CreateBlankRow()
		{
			return new MessageRowViewModel(MessageEntry.CreateBlank(), this) { IsPinnedVisible = true };
		}

		private MessageRowViewModel CreateRow(MessageEntry entry)
		{
			return new MessageRowViewModel(entry, this) { IsPinnedVisible = true };
		}

		private void RequestFocus(MessageRowViewModel row, MessageGridColumn column, Boolean beginEdit)
		{
			this.RequestFocus(new[] { row }, column, beginEdit);
		}

		private void RequestFocus(IReadOnlyList<MessageRowViewModel> rows, MessageGridColumn column, Boolean beginEdit)
		{
			this.RowFocusRequested?.Invoke(this, new RowFocusRequestedEventArgs(rows, column, beginEdit));
		}

		/// <summary>
		/// Creates a document that shares the entries of the rows (no copies); for read-only calculations.
		/// </summary>
		private MessageDocument BuildLiveDocument()
		{
			MessageDocument document = new();
			foreach (MessageRowViewModel row in this._rows)
			{
				document.Entries.Add(row.Entry);
			}

			return document;
		}

		/// <summary>
		/// Creates a document with private copies of all entries; safe to hand to other components.
		/// </summary>
		/// <returns>The document.</returns>
		public MessageDocument BuildDocument()
		{
			MessageDocument document = new();
			foreach (MessageRowViewModel row in this._rows)
			{
				document.Entries.Add(MessageRowViewModel.CloneEntry(row.Entry));
			}

			return document;
		}
	}
}