namespace FalloutScriptStudio.ViewModels.Main
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.ComponentModel;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using CommunityToolkit.Mvvm.ComponentModel;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Problems;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Services.ToolWindows;
	using FalloutScriptStudio.ViewModels.ToolWindows;
	using FalloutScriptStudio.Workspace;

	/// <summary>
	/// The output panel at the bottom of the main window: parser log, build log and the list of problems. The Errors tab lists the parser and
	/// build entries of the active script and every problem reported to the <see cref="IProblemService"/> (missing tools, files that cannot be
	/// opened or saved, damaged settings...), grouped by source and filterable by severity and source.
	/// </summary>
	public sealed partial class OutputPanelViewModel : ObservableObject, IOutputSink
	{
		private IWorkspace? _workspace;
		private readonly ISettingsService _settings;
		private readonly IShellService _shell;
		private readonly IProblemService _problems;
		private readonly IToolWindowService? _toolWindows;
		private readonly SynchronizationContext? _context = SynchronizationContext.Current;
		private readonly Int32 _uiThreadId = Environment.CurrentManagedThreadId;
		private DocumentViewModel? _document;
		private IReadOnlyList<BuildError>? _aggregatedErrors;
		private Boolean _hideDocumentRows;
		private Boolean _navigating;

		/// <summary>Initializes the panel.</summary>
		public OutputPanelViewModel(ISettingsService settings, IShellService shell, IProblemService? problems = null, IToolWindowService? toolWindows = null)
		{
			ArgumentNullException.ThrowIfNull(settings);
			ArgumentNullException.ThrowIfNull(shell);
			this._settings = settings;
			this._shell = shell;
			this._problems = problems ?? new ProblemService();
			this._toolWindows = toolWindows;
			this.Tabs = new ObservableCollection<OutputTabViewModel>
			{
				new ParserLogTabViewModel(this),
				new BuildLogTabViewModel(this),
				new ErrorsTabViewModel(this),
			};
			_ = LanguageChangedSubscription.Subscribe(this, static panel => panel.RebuildErrors());
			this._problems.Changed += (_, _) => this.RunOnUi(this.RebuildErrors);

			foreach (SettingsProblem problem in settings.Problems)
			{
				this._problems.Report(ProblemFactory.FromSettings(problem));
			}

			settings.ProblemRaised += (_, problem) => this.RunOnUi(() => this._problems.Report(ProblemFactory.FromSettings(problem)));
		}

		/// <summary>Connects the panel to the workspace whose active document it shows. Called once by the main view model.</summary>
		public void Attach(IWorkspace workspace)
		{
			ArgumentNullException.ThrowIfNull(workspace);
			this._workspace = workspace;
			this._workspace.ActiveDocumentChanged += (_, _) => this.Follow(this._workspace.ActiveDocument);
			this.Follow(this._workspace.ActiveDocument);
		}

		/// <summary>Raised when the panel should be made visible (an operation put something on it).</summary>
		public event EventHandler? ShowRequested;

		/// <summary>Gets the problems that are not part of the parse or build results of a script.</summary>
		public IProblemService Problems => this._problems;

		/// <summary>
		/// Gets or sets what runs a problem action that opens the settings (the main window, which also reloads what the settings change);
		/// without it the settings window is shown directly.
		/// </summary>
		public Func<SettingsPage, Task>? OpenSettingsHandler { get; set; }

		/// <summary>Gets or sets the function that makes a folder the active project (the link of the hint about the project of an opened script).</summary>
		public Func<String, Task>? SwitchProjectHandler { get; set; }

		/// <summary>Gets or sets the function that opens again the documents a project had open (the link of the hint offered when a project is opened).</summary>
		public Func<String, Task>? RestoreDocumentsHandler { get; set; }

		/// <summary>
		/// Gets the tabs: Parser, Build and Errors (at the indexes of <see cref="OutputTab"/>), then one closable tab for every search
		/// and every "find references".
		/// </summary>
		public ObservableCollection<OutputTabViewModel> Tabs { get; }

		/// <summary>Gets the rows of the Errors tab.</summary>
		public ObservableCollection<ErrorItemViewModel> Errors { get; } = new ObservableCollection<ErrorItemViewModel>();

		/// <summary>The text of the Parser tab.</summary>
		[ObservableProperty]
		public partial String ParserText { get; set; } = String.Empty;

		/// <summary>The text of the Build tab.</summary>
		[ObservableProperty]
		public partial String BuildText { get; set; } = String.Empty;

		/// <summary>The index of the selected tab (see <see cref="OutputTab"/>).</summary>
		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(IsErrorsTabSelected))]
		public partial Int32 SelectedTab { get; set; }

		/// <summary>The selected row of the Errors tab.</summary>
		[ObservableProperty]
		public partial ErrorItemViewModel? SelectedError { get; set; }

		/// <summary>The last short status message.</summary>
		[ObservableProperty]
		public partial String StatusMessage { get; set; } = String.Empty;

		/// <summary>True when the Errors tab refreshes itself whenever the active document or its logs change.</summary>
		[ObservableProperty]
		public partial Boolean AutoRefresh { get; set; } = true;

		/// <summary>True when parser entries are listed in the Errors tab.</summary>
		[ObservableProperty]
		public partial Boolean ShowParserEntries { get; set; } = true;

		/// <summary>True when build entries are listed in the Errors tab.</summary>
		[ObservableProperty]
		public partial Boolean ShowBuildEntries { get; set; } = true;

		/// <summary>True when problems of the external tools (compiler, preprocessor, parser.dll, decompiler, editor) are listed.</summary>
		[ObservableProperty]
		public partial Boolean ShowToolEntries { get; set; } = true;

		/// <summary>True when problems of files (open, save, includes, archives) are listed.</summary>
		[ObservableProperty]
		public partial Boolean ShowFileEntries { get; set; } = true;

		/// <summary>True when problems of the settings are listed.</summary>
		[ObservableProperty]
		public partial Boolean ShowSettingsEntries { get; set; } = true;

		/// <summary>True when problems of the message files are listed.</summary>
		[ObservableProperty]
		public partial Boolean ShowMessageEntries { get; set; } = true;

		/// <summary>True when errors are listed.</summary>
		[ObservableProperty]
		public partial Boolean ShowErrors { get; set; } = true;

		/// <summary>True when warnings are listed.</summary>
		[ObservableProperty]
		public partial Boolean ShowWarnings { get; set; } = true;

		/// <summary>True when informations (notes, messages of the tools) are listed.</summary>
		[ObservableProperty]
		public partial Boolean ShowInfos { get; set; } = true;

		/// <summary>Gets the number of errors, whatever the filters (for the toolbar and the status bar).</summary>
		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(HasNoProblems))]
		public partial Int32 ProblemErrorCount { get; private set; }

		/// <summary>Gets the number of warnings, whatever the filters.</summary>
		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(HasNoProblems))]
		public partial Int32 ProblemWarningCount { get; private set; }

		/// <summary>Gets the number of informations, whatever the filters.</summary>
		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(HasNoProblems))]
		public partial Int32 ProblemInfoCount { get; private set; }

		/// <summary>
		/// Gets a value indicating whether there is nothing to list at all (no parser or build entry, no problem), whatever the filters: the
		/// Errors tab then shows a friendly "No problems" text instead of an empty grid. When the filters only hide the entries the grid stays.
		/// </summary>
		public Boolean HasNoProblems => this.ProblemErrorCount + this.ProblemWarningCount + this.ProblemInfoCount == 0;

		/// <summary>Gets a value indicating whether the Errors tab is the selected tab.</summary>
		public Boolean IsErrorsTabSelected => this.SelectedTab == (Int32)OutputTab.Errors;

		/// <summary>The number of rows in the Errors tab, for the tab header.</summary>
		public Int32 ErrorCount => this.Errors.Count;

		/// <summary>Rebuilds the logs and the Errors tab from the active document.</summary>
		[RelayCommand]
		public void Refresh()
		{
			this._aggregatedErrors = null;
			this._hideDocumentRows = false;
			this.LoadTexts();
			this.RebuildErrors();
		}

		/// <summary>Clears the logs of the active document, the Errors tab and the problems.</summary>
		[RelayCommand]
		public void ClearAll()
		{
			this._aggregatedErrors = null;
			this._hideDocumentRows = false;
			this._problems.Clear();
			if (this._document is not null)
			{
				this._document.BuildErrors = Array.Empty<BuildError>();
				this._document.ParserErrors = Array.Empty<BuildError>();
			}

			this.RebuildErrors();
		}

		/// <summary>Copies the message of the selected row (and its hint) to the clipboard.</summary>
		[RelayCommand]
		public void CopySelected()
		{
			if (this.SelectedError is not null)
			{
				this._shell.SetClipboardText(this.SelectedError.ClipboardText);
			}
		}

		/// <summary>Copies every listed row, one per line with its file and position, to the clipboard.</summary>
		[RelayCommand]
		public void CopyAll()
		{
			if (this.Errors.Count > 0)
			{
				this._shell.SetClipboardText(String.Join("\r\n", this.Errors.Select(item => item.ReportLine)));
			}
		}

		/// <summary>Copies the message of a row to the clipboard.</summary>
		/// <param name="item">The row.</param>
		public void CopyError(ErrorItemViewModel? item)
		{
			if (item is not null)
			{
				this._shell.SetClipboardText(item.ClipboardText);
			}
		}

		/// <summary>Jumps to the place an Errors row points to, opening the file when needed.</summary>
		[RelayCommand]
		public async Task GoToErrorAsync(ErrorItemViewModel? item)
		{
			ErrorItemViewModel? target = item ?? this.SelectedError;
			if (target is null || !target.CanNavigate)
			{
				return;
			}

			if (this._workspace is null)
			{
				return;
			}

			// Opening the file of a row changes the active document; the list of the other files must not be replaced under the user's hand.
			this._navigating = true;
			try
			{
				await this._workspace.GoToAsync(target.FullPath.Length == 0 ? null : target.FullPath, target.Line, target.Error.Column, target.Error.Length);
			}
			finally
			{
				this._navigating = false;
			}
		}

		/// <summary>Selects the next error or warning of the list (after the last one comes the first) and goes to it.</summary>
		[RelayCommand]
		public Task NextProblemAsync()
		{
			return this.MoveToProblemAsync(1);
		}

		/// <summary>Selects the previous error or warning of the list (before the first one comes the last) and goes to it.</summary>
		[RelayCommand]
		public Task PreviousProblemAsync()
		{
			return this.MoveToProblemAsync(-1);
		}

		/// <summary>Runs the fix a problem offers (the link of its row).</summary>
		/// <param name="item">The row; the selected row when <see langword="null"/>.</param>
		/// <returns>A task that completes when the settings window was closed or the folder opened.</returns>
		[RelayCommand]
		public async Task RunProblemActionAsync(ErrorItemViewModel? item)
		{
			ProblemAction? action = (item ?? this.SelectedError)?.Action;
			if (action is null)
			{
				return;
			}

			switch (action.Kind)
			{
				case ProblemActionKind.OpenSettings:
					if (this.OpenSettingsHandler is not null)
					{
						await this.OpenSettingsHandler(action.Page);
					}
					else if (this._toolWindows is not null)
					{
						await this._toolWindows.ShowSettingsAsync(action.Page);
					}

					break;
				case ProblemActionKind.OpenFolder:
					if (action.FolderPath is not null)
					{
						this._shell.OpenFolder(action.FolderPath);
					}

					break;
				case ProblemActionKind.SwitchProject:
					if (action.FolderPath is not null && this.SwitchProjectHandler is not null)
					{
						await this.SwitchProjectHandler(action.FolderPath);
					}

					break;
				case ProblemActionKind.OpenFile:
					if (action.FilePath is not null && this._workspace is not null)
					{
						ErrorItemViewModel? row = item ?? this.SelectedError;
						await this._workspace.GoToAsync(action.FilePath, row?.Line ?? -1, row?.Error.Column ?? -1, row?.Error.Length ?? -1);
					}

					break;
				case ProblemActionKind.RestoreDocuments:
					if (action.FolderPath is not null && this.RestoreDocumentsHandler is not null)
					{
						await this.RestoreDocumentsHandler(action.FolderPath);
					}

					break;
				case ProblemActionKind.RemoveUnusedVariable:
					if (action.FilePath is not null && this._workspace is not null)
					{
						await this._workspace.RemoveUnusedVariableAsync(action.FilePath, action.Line, action.Column);
					}

					break;
			}
		}

		public void ClearErrors()
		{
			// The rows of the script and of the last "compile all" go; the problems of tools and files stay until their cause is gone.
			this._aggregatedErrors = null;
			this._hideDocumentRows = true;
			this.RebuildErrors();
		}

		private void RunOnUi(Action action)
		{
			if (this._context is not null && Environment.CurrentManagedThreadId != this._uiThreadId)
			{
				this._context.Post(static state => ((Action)state!)(), action);
			}
			else
			{
				action();
			}
		}

		private async Task MoveToProblemAsync(Int32 direction)
		{
			List<ErrorItemViewModel> candidates = this.Errors.Where(item => item.Severity != ProblemSeverity.Info).ToList();
			if (candidates.Count == 0)
			{
				this.StatusMessage = Loc.Get("Str.Shell.Problems.NoProblems");
				return;
			}

			Int32 current = this.SelectedError is null ? -1 : candidates.IndexOf(this.SelectedError);
			Int32 next = current < 0
				? (direction > 0 ? 0 : candidates.Count - 1)
				: (current + direction + candidates.Count) % candidates.Count;
			this.SelectedError = candidates[next];
			this.ShowTab(OutputTab.Errors);
			await this.GoToErrorAsync(this.SelectedError);
		}

		private void Follow(DocumentViewModel? document)
		{
			if (this._document is not null)
			{
				this._document.PropertyChanged -= this.OnDocumentPropertyChanged;
			}

			this._document = document;
			if (document is not null)
			{
				document.PropertyChanged += this.OnDocumentPropertyChanged;
			}

			this.LoadTexts();
			if (this._navigating)
			{
				return;
			}

			if (this.AutoRefresh && document is not null && (document.ParserErrors.Count > 0 || document.BuildErrors.Count > 0))
			{
				this._aggregatedErrors = null;
				this._hideDocumentRows = false;
				this.RebuildErrors();
			}
		}

		private void OnDocumentPropertyChanged(Object? sender, PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case nameof(DocumentViewModel.ParserLog):
				case nameof(DocumentViewModel.BuildLog):
					this.LoadTexts();
					break;
				case nameof(DocumentViewModel.ParserErrors):
				case nameof(DocumentViewModel.BuildErrors):
					this._hideDocumentRows = false;
					if (this._aggregatedErrors is null)
					{
						this.RebuildErrors();
					}

					break;
			}
		}

		private void LoadTexts()
		{
			DocumentViewModel? document = this._document;
			this.ParserText = document?.ParserLog ?? String.Empty;
			if (document is not null && document.BuildLog.Length > 0)
			{
				this.BuildText = document.BuildLog;
			}
		}
	}
}
