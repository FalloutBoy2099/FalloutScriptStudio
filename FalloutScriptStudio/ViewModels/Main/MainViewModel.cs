namespace FalloutScriptStudio.ViewModels.Main
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.ComponentModel;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using CommunityToolkit.Mvvm.ComponentModel;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.Messages;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Core.TextTools;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Problems;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Services.MessageEditor;
	using FalloutScriptStudio.Services.ScriptTools;
	using FalloutScriptStudio.Services.ToolWindows;
	using FalloutScriptStudio.ViewModels.Panels;
	using FalloutScriptStudio.Workspace;

	/// <summary>
	/// The view model of the main window: menus, toolbar, status bar and the layout of the panels. The commands delegate to the
	/// workspace, the build coordinator, the tool windows and the active editor surface.
	/// </summary>
	public sealed partial class MainViewModel : ObservableObject
	{
		/// <summary>The title of the application.</summary>
		public const String ApplicationTitle = "Fallout Script Studio";

		private readonly IWorkspaceManager _workspace;
		private readonly ISettingsService _settings;
		private readonly IDialogService _dialogs;
		private readonly IThemeService _theme;
		private readonly IToolWindowService _toolWindows;
		private readonly IScriptToolService _scriptTools;
		private readonly IMessageEditorService _messageEditor;
		private readonly BuildCoordinator _build;
		private readonly IShellService _shell;
		private readonly IFileAssociationService _association;
		private readonly IMessageStructureChecker _messageChecker;
		private readonly IncludeInserter _includeInserter;
		private readonly ShellLayoutStore _layoutStore;
		private readonly ShellLayout _layout;
		private readonly EditorViewOptions _viewOptions;
		private readonly ActiveEditorTracker _tracker;
		private readonly IProjectService _projects;
		private readonly ProjectSessionService _sessions;
		private DocumentViewModel? _observedDocument;
		private Boolean _loadingLayout;
		private Boolean _loadingDefines;
		private String _noPreprocessorDefine = String.Empty;

		/// <summary>Initializes the view model.</summary>
		public MainViewModel(
			IWorkspaceManager workspace,
			ISettingsService settings,
			IDialogService dialogs,
			IThemeService theme,
			IToolWindowService toolWindows,
			IScriptToolService scriptTools,
			IMessageEditorService messageEditor,
			BuildCoordinator build,
			IShellService shell,
			IFileAssociationService association,
			IMessageStructureChecker messageChecker,
			IncludeInserter includeInserter,
			ShellLayoutStore layoutStore,
			EditorViewOptions viewOptions,
			ActiveEditorTracker tracker,
			OutputPanelViewModel output,
			SearchBarViewModel search,
			ProceduresPanelViewModel procedures,
			VariablesPanelViewModel variables,
			FunctionsPanelViewModel functions,
			ProjectExplorerViewModel project,
			IProjectService projects,
			ProjectSessionService? sessions = null,
			GlobalsPanelViewModel? globals = null,
			HelpPanelViewModel? help = null)
		{
			this._workspace = workspace;
			this._settings = settings;
			this._dialogs = dialogs;
			this._theme = theme;
			this._toolWindows = toolWindows;
			this._scriptTools = scriptTools;
			this._messageEditor = messageEditor;
			this._build = build;
			this._shell = shell;
			this._association = association;
			this._messageChecker = messageChecker;
			this._includeInserter = includeInserter;
			this._layoutStore = layoutStore;
			this._viewOptions = viewOptions;
			this._tracker = tracker;
			this.Output = output;
			this.Output.Attach(workspace);
			this.Search = search;
			this.Procedures = procedures;
			this.Variables = variables;
			this.Functions = functions;
			this.Project = project;
			this.Globals = globals;
			this.Help = help;
			this._projects = projects;
			this._sessions = sessions ?? new ProjectSessionService(workspace, settings, projects, output.Problems);
			this._sessionFolder = projects.Folder;
			this._sessions.BeginVisit();

			this._layout = layoutStore.Load();
			this._loadingLayout = true;
			this.ShowRightBrowser = this._layout.ShowRightBrowser;
			this.ShowFunctionTree = this._layout.ShowFunctionTree;
			this.RightTabIndex = this._layout.RightTabIndex;
			this.IsOutputMinimized = this._layout.LogMinimized;
			this._viewOptions.ShowLineNumbers = this._layout.ShowLineNumbers;
			this._viewOptions.HighlightParseErrors = this._layout.HighlightParseErrors;
			this._loadingLayout = false;

			_ = LanguageChangedSubscription.Subscribe(this, static viewModel => viewModel.OnLanguageChanged());
			this._workspace.ActiveDocumentChanged += (_, _) => this.OnActiveDocumentChanged();
			this._workspace.DocumentOpened += (_, _) => this.RefreshRecentFiles();
			this._workspace.DocumentClosed += (_, _) => this.RefreshRecentFiles();
			this._settings.SettingsChanged += (_, _) => this.RefreshSettingsProperties();
			this._scriptTools.UndoReplaceInFilesStateChanged += (_, _) => this.OnUndoReplaceInFilesStateChanged();
			this._projects.ProjectChanged += (_, _) => this.OnProjectChanged();
			this._projects.FileChangedOnDisk += (_, e) => this.OnProjectFileChangedOnDisk(e);
			this._projects.ProblemRaised += (_, problem) => this.Output.Problems.Report(ProblemFactory.FromSettings(problem));
			this._workspace.DocumentOpened += (_, document) => this.OfferProjectOf(document);
			this._tracker.EditorChanged += (_, _) => this.RefreshEditorState();
			this._tracker.CaretMoved += (_, _) => this.RefreshCaretState();
			this._tracker.ZoomChanged += (_, _) => this.OnPropertyChanged(nameof(this.ZoomText));
			this._viewOptions.PropertyChanged += (_, _) => this.SaveLayout();
			this.Output.ShowRequested += (_, _) => this.ShowOutputPanel();
			this.Output.PropertyChanged += (_, e) =>
			{
				if (e.PropertyName == nameof(OutputPanelViewModel.StatusMessage))
				{
					this.OnPropertyChanged(nameof(this.StatusMessage));
				}
			};

			this.InitializeProblems();
			this.InitializeFormatting();
			this.LoadTemplates();
			this.LoadPreprocessorDefines();
			this.LoadCompileProfiles();
			this.RefreshRecentFiles();
			this.RefreshRecentProjects();
			this.OnActiveDocumentChanged();
		}

		/// <summary>Raised when the user asked to close the main window (File, Exit).</summary>
		public event EventHandler? ExitRequested;

		/// <summary>Gets the output panel.</summary>
		public OutputPanelViewModel Output { get; }

		/// <summary>Gets the search bar.</summary>
		public SearchBarViewModel Search { get; }

		/// <summary>Gets the procedures panel.</summary>
		public ProceduresPanelViewModel Procedures { get; }

		/// <summary>Gets the variables panel.</summary>
		public VariablesPanelViewModel Variables { get; }

		/// <summary>Gets the functions panel.</summary>
		public FunctionsPanelViewModel Functions { get; }

		/// <summary>Gets the project explorer.</summary>
		public ProjectExplorerViewModel Project { get; }

		/// <summary>Gets the open documents (tab order).</summary>
		public ObservableCollection<DocumentViewModel> Documents => this._workspace.Documents;

		/// <summary>Gets the recent files, newest first.</summary>
		public ObservableCollection<String> RecentFiles { get; } = [];

		/// <summary>Gets the script templates of the New menu.</summary>
		public ObservableCollection<TemplateItem> Templates { get; } = [];

		/// <summary>Gets the preprocessor definitions offered in the toolbar (the first entry means "none").</summary>
		public ObservableCollection<PreprocessorDefineOption> PreprocessorDefineOptions { get; } = [];

		/// <summary>Gets the definitions of the toolbar list (the empty string stands for "none").</summary>
		public IEnumerable<String> PreprocessorDefineItems => this.PreprocessorDefineOptions.Select(option => option.Value);

		/// <summary>Gets or sets the selected tab.</summary>
		public DocumentViewModel? ActiveDocument
		{
			get => this._workspace.ActiveDocument;
			set => this._workspace.ActiveDocument = value;
		}

		/// <summary>Gets a value indicating whether a document is open.</summary>
		public Boolean HasDocument => this._workspace.ActiveDocument is not null;

		/// <summary>Gets a value indicating whether the active document is an SSL script.</summary>
		public Boolean IsScript => this._workspace.ActiveDocument is { Kind: DocumentKind.Script };

		/// <summary>Gets a value indicating whether the active document is parsed (script or header).</summary>
		public Boolean IsParsable => this._workspace.ActiveDocument is { IsParsable: true };

		/// <summary>Gets a value indicating whether the active document is a message file.</summary>
		public Boolean IsMessage => this._workspace.ActiveDocument is { Kind: DocumentKind.Message };

		/// <summary>Gets a value indicating whether the active editor can undo.</summary>
		public Boolean CanUndo => this._workspace.ActiveDocument?.CanUndo ?? false;

		/// <summary>Gets a value indicating whether the active editor can redo.</summary>
		public Boolean CanRedo => this._workspace.ActiveDocument?.CanRedo ?? false;

		/// <summary>Gets a value indicating whether Back is possible.</summary>
		public Boolean CanGoBack => this._tracker.Editor?.CanNavigateBack ?? false;

		/// <summary>Gets a value indicating whether Forward is possible.</summary>
		public Boolean CanGoForward => this._tracker.Editor?.CanNavigateForward ?? false;

		/// <summary>Gets a value indicating whether the right browser is visible (it is shown for scripts and headers only).</summary>
		public Boolean IsRightBrowserVisible => this.ShowRightBrowser && this.IsParsable;

		/// <summary>Gets the window title.</summary>
		public String Title
		{
			get
			{
				String application = this._projects.IsOpen ? ApplicationTitle + " (" + this._projects.DisplayName + ")" : ApplicationTitle;
				DocumentViewModel? document = this._workspace.ActiveDocument;
				if (document is null)
				{
					return application;
				}

				String define = this._settings.Effective.Settings.Compiler.PreprocessorDefine is { Length: > 0 } value ? " [" + value + "]" : String.Empty;
				return application + " - " + (document.FilePath ?? document.FileName) + define;
			}
		}

		/// <summary>True when the output panel is shown.</summary>
		public Boolean ShowLog
		{
			get => this._settings.Settings.Window.ShowLog;
			set
			{
				if (this._settings.Settings.Window.ShowLog != value)
				{
					this._settings.Update(settings => settings.Window.ShowLog = value);
					this.OnPropertyChanged();
				}
			}
		}

		/// <summary>True when the right browser panel is enabled in the View menu.</summary>
		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(IsRightBrowserVisible))]
		public partial Boolean ShowRightBrowser { get; set; }

		/// <summary>True when the left function tree is shown.</summary>
		[ObservableProperty]
		public partial Boolean ShowFunctionTree { get; set; }

		/// <summary>The index of the selected tab of the right browser.</summary>
		[ObservableProperty]
		public partial Int32 RightTabIndex { get; set; }

		/// <summary>True when the output panel shows only its tab headers.</summary>
		[ObservableProperty]
		public partial Boolean IsOutputMinimized { get; set; }

		/// <summary>Gets or sets a value indicating whether the line number margin is shown.</summary>
		public Boolean ShowLineNumbers
		{
			get => this._viewOptions.ShowLineNumbers;
			set
			{
				this._viewOptions.ShowLineNumbers = value;
				this.OnPropertyChanged();
			}
		}

		/// <summary>Gets or sets a value indicating whether parser errors are underlined.</summary>
		public Boolean HighlightParseErrors
		{
			get => this._viewOptions.HighlightParseErrors;
			set
			{
				this._viewOptions.HighlightParseErrors = value;
				this.OnPropertyChanged();
			}
		}

		/// <summary>Gets the current theme mode.</summary>
		public AppTheme CurrentTheme => this._theme.Current;

		/// <summary>Asks the user to save or discard all modified documents before the window closes.</summary>
		/// <returns><see langword="true"/> when the window may close.</returns>
		public async Task<Boolean> CanCloseAsync()
		{
			if (this._projects.Folder is { } projectFolder)
			{
				// The open documents are remembered before closing them removes them from the list.
				this._sessions.Capture(projectFolder);
			}

			// The Message Editor windows close between the questions and the saving, so a "Cancel" in either place changes nothing.
			return await this._workspace.CloseAllAsync(null, this._messageEditor.CloseAllAsync);
		}

		private void OnLanguageChanged()
		{
			this.OnPropertyChanged(nameof(this.LineText));
			this.OnPropertyChanged(nameof(this.ColumnText));
			this.OnPropertyChanged(nameof(this.ParserStatusText));
			this.OnPropertyChanged(nameof(this.ProjectText));
		}

		private void OnActiveDocumentChanged()
		{
			if (this._observedDocument is not null)
			{
				this._observedDocument.PropertyChanged -= this.OnObservedDocumentPropertyChanged;
				this._observedDocument.TextDocumentUndoChanged -= this.OnObservedUndoChanged;
			}

			this._observedDocument = this._workspace.ActiveDocument;
			if (this._observedDocument is not null)
			{
				this._observedDocument.PropertyChanged += this.OnObservedDocumentPropertyChanged;
				this._observedDocument.TextDocumentUndoChanged += this.OnObservedUndoChanged;
			}

			this.OnPropertyChanged(nameof(this.ActiveDocument));
			this.RefreshEditorState();
		}

		private void OnObservedDocumentPropertyChanged(Object? sender, PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case nameof(DocumentViewModel.ParserStatus):
					this.OnPropertyChanged(nameof(this.ParserStatusText));
					break;
				case nameof(DocumentViewModel.FilePath):
				case nameof(DocumentViewModel.Kind):
					this.OnPropertyChanged(nameof(this.Title));
					this.RefreshEditorState();
					break;
			}
		}

		private void OnObservedUndoChanged(Object? sender, EventArgs e)
		{
			this.OnPropertyChanged(nameof(this.CanUndo));
			this.OnPropertyChanged(nameof(this.CanRedo));
		}

		private void ShowOutputPanel()
		{
			if (!this.ShowLog)
			{
				this.ShowLog = true;
			}

			if (this.IsOutputMinimized)
			{
				this.IsOutputMinimized = false;
			}
		}

		private void RefreshRecentFiles()
		{
			List<String> recent = this._settings.Recent.Scripts.AsEnumerable().Reverse().ToList();
			if (recent.SequenceEqual(this.RecentFiles))
			{
				return;
			}

			this.RecentFiles.Clear();
			foreach (String path in recent)
			{
				this.RecentFiles.Add(path);
			}
		}

		private void LoadTemplates()
		{
			this.Templates.Clear();
			String directory = this._settings.Paths.TemplatesDirectory;
			if (!Directory.Exists(directory))
			{
				return;
			}

			foreach (String file in Directory.GetFiles(directory, "*.ssl").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
			{
				this.Templates.Add(new TemplateItem(Path.GetFileNameWithoutExtension(file), file));
			}
		}

		private void RefreshSettingsProperties()
		{
			foreach (String name in new[]
			{
				nameof(this.SaveAsUtf8),
				nameof(this.AutoTrimTrailingSpaces),
				nameof(this.FormatCodeOnOpen),
				nameof(this.ShowWhitespace),
				nameof(this.ShowIndentLine),
				nameof(this.DefineToScriptsHeader),
				nameof(this.AutoOpenMessageEditor),
				nameof(this.DecompileAsFallout1),
				nameof(this.UseOldDecompiler),
				nameof(this.CanUseOldDecompiler),
				nameof(this.UseOemMessageEncoding),
				nameof(this.UseWindowsMessageEncoding),
				nameof(this.ShowLog),
				nameof(this.ProjectText),
				nameof(this.ProjectTip),
				nameof(this.SelectedPreprocessorDefine),
				nameof(this.SelectedCompileProfile),
				nameof(this.CanChooseDefine),
				nameof(this.MassCompileOnlyChanged),
				nameof(this.Title),
				nameof(this.ParserStatusText),
			})
			{
				this.OnPropertyChanged(name);
			}
		}
	}
}