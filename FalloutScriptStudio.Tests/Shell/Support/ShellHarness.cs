namespace FalloutScriptStudio.Tests.Shell.Support
{
	using System;
	using System.IO;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.Messages;
	using FalloutScriptStudio.Core.Functions;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Core.Refactoring;
	using FalloutScriptStudio.Core.TextTools;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Problems;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Tests.Flowchart;
	using FalloutScriptStudio.ViewModels.Main;
	using FalloutScriptStudio.ViewModels.Panels;
	using FalloutScriptStudio.Workspace;
	using Microsoft.Extensions.Logging.Abstractions;

	/// <summary>The main view model with all its panels, wired to fake services and a temporary program folder.</summary>
	internal sealed class ShellHarness : IDisposable
	{
		/// <summary>Initializes a new instance of the <see cref="ShellHarness"/> class.</summary>
		/// <param name="watch">How the project file is watched; by default it is not (a test that needs it passes short delays).</param>
		/// <param name="configure">Prepares the settings before the project service reads them (a project that is already open when the program starts, remembered documents).</param>
		public ShellHarness(ProjectFileWatchOptions? watch = null, Action<SettingsService>? configure = null)
		{
			this.Folder = new TempFolder();
			this.Paths = AppPaths.FromBaseDirectory(this.Folder.Combine("app"));
			this.Paths.EnsureCreated();
			File.WriteAllText(Path.Combine(this.Paths.TemplatesDirectory, "newScript.txt"), "// new\r\n");
			File.WriteAllText(Path.Combine(this.Paths.TemplatesDirectory, "dialog.ssl"), "// dialog template\r\n");
			Directory.CreateDirectory(this.Paths.DescriptionsDirectory);
			File.WriteAllText(Path.Combine(this.Paths.DescriptionsDirectory, "functions.ini"), "<m>Group\r\nwait<d>Waits<s>wait({int time});\r\n");
			File.WriteAllText(Path.Combine(this.Paths.DescriptionsDirectory, "userfunctions.ini"), "<m>User\r\nmine<d>Mine<s>mine;\r\n");

			this.Settings = new SettingsService(this.Paths, new LegacySettingsImporter(NullLogger<LegacySettingsImporter>.Instance), NullLogger<SettingsService>.Instance);
			this.Settings.Load();
			configure?.Invoke(this.Settings);
			this.Shell = new FakeShellService();
			this.FileAssociation = new FakeFileAssociationService();
			this.Problems = new ProblemService();
			this.Operations = new OperationTracker();
			this.Output = new OutputPanelViewModel(this.Settings, this.Shell, this.Problems);
			this.Parser = new FakeParser();
			this.Coordinator = new ParseCoordinator(this.Parser, this.Settings, null, TimeSpan.FromMilliseconds(20), this.Problems);
			this.Compiler = new FakeScriptCompiler();
			this.Dialogs = new FakeDialogService();
			this.ScriptTools = new FakeScriptToolService();
			this.MessageEditor = new FakeMessageEditorService();
			this.ToolWindows = new FakeToolWindowService();
			this.Sounds = new FakeSoundService();
			this.Notifications = new ShellNotificationService();
			this.Theme = new FakeThemeService();
			this.DialogueDialogs = new FakeDialogueDialogsService();
			this.ViewOptions = new EditorViewOptions();
			this.Catalog = new FunctionCatalogService(
				new FunctionCatalogPaths(Path.Combine(this.Paths.DescriptionsDirectory, "functions.ini"), Path.Combine(this.Paths.DescriptionsDirectory, "userfunctions.ini")));
			this.Catalog.LoadAsync().GetAwaiter().GetResult();
			this.Workspace = WorkspaceServiceFactory.Create(this.Settings, this.Coordinator, this.Compiler, new FakeArchiveExtractor(), this.Dialogs, this.Output, this.MessageEditor, this.ScriptTools, this.DialogueDialogs, this.Problems, this.Operations);
			this.Build = new BuildCoordinator(this.Settings, this.Compiler, new FakeBatchCompileService(), this.Workspace, this.Coordinator, this.Dialogs, this.Output, this.Notifications, this.Sounds, this.ToolWindows, this.Operations, this.Problems);
			this.Tracker = new ActiveEditorTracker(this.Workspace);
			this.Search = new SearchBarViewModel(this.Workspace, this.Settings, this.Sounds, new TextSearcher());
			this.NodeEditors = new FakeNodeCodeEditorService();
			this.Procedures = new ProceduresPanelViewModel(this.Workspace, this.Tracker, this.Dialogs, this.Settings, this.NodeEditors);
			this.Variables = new VariablesPanelViewModel(this.Workspace, this.Tracker);
			this.Functions = new FunctionsPanelViewModel(this.Catalog, this.Tracker, this.Dialogs, this.DialogueDialogs);
			this.Project = new ProjectExplorerViewModel(this.Settings, this.Workspace, this.Dialogs, this.Shell);
			this.Projects = new ProjectService(this.Settings, NullLogger<ProjectService>.Instance, watch ?? ProjectFileWatchOptions.Disabled);
			this.Main = new MainViewModel(
				this.Workspace,
				this.Settings,
				this.Dialogs,
				this.Theme,
				this.ToolWindows,
				this.ScriptTools,
				this.MessageEditor,
				this.Build,
				this.Shell,
				this.FileAssociation,
				new MessageStructureChecker(),
				new IncludeInserter(),
				new ShellLayoutStore(this.Paths),
				this.ViewOptions,
				this.Tracker,
				this.Output,
				this.Search,
				this.Procedures,
				this.Variables,
				this.Functions,
				this.Project,
				this.Projects);
		}

		public TempFolder Folder { get; }

		public AppPaths Paths { get; }

		public SettingsService Settings { get; }

		public FakeShellService Shell { get; }

		public FakeFileAssociationService FileAssociation { get; }

		public IProblemService Problems { get; }

		public IOperationTracker Operations { get; }

		public OutputPanelViewModel Output { get; }

		public FakeParser Parser { get; }

		public ParseCoordinator Coordinator { get; }

		public FakeScriptCompiler Compiler { get; }

		public FakeDialogService Dialogs { get; }

		public FakeScriptToolService ScriptTools { get; }

		public FakeMessageEditorService MessageEditor { get; }

		public FakeToolWindowService ToolWindows { get; }

		public FakeSoundService Sounds { get; }

		public ShellNotificationService Notifications { get; }

		public FakeThemeService Theme { get; }

		public FakeDialogueDialogsService DialogueDialogs { get; }

		public FunctionCatalogService Catalog { get; }

		public EditorViewOptions ViewOptions { get; }

		public WorkspaceService Workspace { get; }

		public BuildCoordinator Build { get; }

		public ActiveEditorTracker Tracker { get; }

		public SearchBarViewModel Search { get; }

		public FakeNodeCodeEditorService NodeEditors { get; }

		public ProceduresPanelViewModel Procedures { get; }

		public VariablesPanelViewModel Variables { get; }

		public FunctionsPanelViewModel Functions { get; }

		public ProjectExplorerViewModel Project { get; }

		public ProjectService Projects { get; }

		public MainViewModel Main { get; }

		public async Task<DocumentViewModel> OpenFileAsync(String relativePath, String text)
		{
			String path = this.Folder.Write(relativePath, text);
			DocumentViewModel? document = await this.Workspace.OpenAsync(path, new OpenDocumentOptions { AddToRecent = true });
			return document ?? throw new InvalidOperationException("The file was not opened.");
		}

		public FakeEditorSurface AttachEditor(DocumentViewModel document)
		{
			FakeEditorSurface editor = new FakeEditorSurface(document.TextDocument);
			document.Editor = editor;
			document.ViewState.HasBeenShown = true;
			return editor;
		}

		public void Dispose()
		{
			this.Projects.Dispose();
			this.Tracker.Dispose();
			this.Folder.Dispose();
		}
	}
}
