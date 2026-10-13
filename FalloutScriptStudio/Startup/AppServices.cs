namespace FalloutScriptStudio.Startup
{
	using System;
	using System.IO;
	using FalloutScriptStudio.Core.Archives;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.Decompilation;
	using FalloutScriptStudio.Core.Dialogue;
	using FalloutScriptStudio.Core.Functions;
	using FalloutScriptStudio.Core.Indexing;
	using FalloutScriptStudio.Core.Messages;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Core.Parsing.Interop.Host;
	using FalloutScriptStudio.Core.Refactoring;
	using FalloutScriptStudio.Core.TextTools;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Editor;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Problems;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Services.MessageEditor;
	using FalloutScriptStudio.Services.ScriptTools;
	using FalloutScriptStudio.Services.ScriptTools.Dialogs;
	using FalloutScriptStudio.Services.ScriptTools.Flowchart;
	using FalloutScriptStudio.Services.ToolWindows;
	using FalloutScriptStudio.ViewModels.Main;
	using FalloutScriptStudio.ViewModels.Panels;
	using FalloutScriptStudio.Views.Editor;
	using FalloutScriptStudio.Views.Panels;
	using FalloutScriptStudio.Views.Shell;
	using FalloutScriptStudio.Workspace;
	using FalloutScriptStudio.Workspace.Functions;
	using Microsoft.Extensions.DependencyInjection;
	using Microsoft.Extensions.Logging;

	/// <summary>The registrations of the services, view models and windows of the application (the composition root).</summary>
	public static class AppServices
	{
		/// <summary>Registers everything the application needs.</summary>
		/// <param name="services">The collection to fill.</param>
		/// <param name="paths">The paths of the installation.</param>
		/// <param name="log">The log provider.</param>
		/// <param name="singleInstance">The single-instance lock of this process.</param>
		public static void ConfigureServices(IServiceCollection services, AppPaths paths, ILoggerProvider log, ISingleInstance singleInstance)
		{
			services.AddSingleton(paths);
			services.AddSingleton(singleInstance);
			services.AddLogging(builder => builder.AddProvider(log).SetMinimumLevel(LogLevel.Information));

			// Core: settings.
			services.AddSingleton<LegacySettingsImporter>();
			services.AddSingleton<ISettingsService, SettingsService>();
			services.AddSingleton<IProjectService, ProjectService>();

			// Core: parsing. The persisted fold store replaces the in-memory default.
			services.AddSingleton<ICollapsedProcedureStore, SettingsCollapsedProcedureStore>();
			// parser.dll (32-bit) never loads into this process: FalloutScriptStudio.ParserHost.exe, next to the executable, hosts it.
			services.AddScriptParsing(
				provider => ParserOptionsFactory.Create(provider.GetRequiredService<ISettingsService>()),
				NativeParserHostOptions.ForDirectory(paths.ProgramDirectory));
			services.AddSingleton(provider =>
			{
				ISettingsService settings = provider.GetRequiredService<ISettingsService>();
				return OpcodeCatalog.LoadFromFile(Path.Combine(settings.Paths.DescriptionsDirectory, "opcodes.txt"), settings.Settings.Editor.ShortDescriptions);
			});

			// Core: compilation, messages, archives.
			services.AddSingleton<IProcessRunner, ProcessRunner>();
			services.AddSingleton<IScriptCompiler, ScriptCompiler>();
			services.AddSingleton<IBatchCompileService, BatchCompileService>();
			services.AddSingleton<IMessageFileService, MessageFileService>();
			services.AddSingleton<IMessageStructureChecker, MessageStructureChecker>();
			services.AddArchives();
			services.AddDecompilation();

			// Application services.
			services.AddSingleton<IThemeService, ThemeService>();
			services.AddSingleton<IDialogService, DialogService>();
			services.AddSingleton<IFileAssociationService, FileAssociationService>();
			services.AddSingleton<IShellService, ShellService>();
			services.AddSingleton<ISoundService, SoundService>();
			services.AddSingleton<IShellNotificationService, ShellNotificationService>();
			services.AddSingleton<EditorViewOptions>();
			services.AddSingleton<ShellLayoutStore>();

			// Core: text tools (search, procedure text, include insertion), refactoring and the functions catalog.
			services.AddTextTools();
			services.AddRefactoring();
			services.AddProjectIndex(provider => new ProjectIndexOptions { Encoding = provider.GetRequiredService<ISettingsService>().Settings.Editor.FileEncoding.ToEncoding() });
			services.AddFunctionCatalog(
				_ => new FunctionCatalogPaths(Path.Combine(paths.DescriptionsDirectory, "functions.ini"), Path.Combine(paths.SettingsDirectory, "userfunctions.ini")),
				_ => Path.Combine(paths.SettingsDirectory, "user-keywords.json"));

			// Workspace.
			services.AddSingleton<IProblemService, ProblemService>();
			services.AddSingleton<IOperationTracker, OperationTracker>();
			services.AddSingleton<ParseCoordinator>();
			services.AddSingleton<ISymbolRenameService, SymbolRenameService>();
			services.AddSingleton<OutputPanelViewModel>();
			services.AddSingleton<IOutputSink>(provider => provider.GetRequiredService<OutputPanelViewModel>());
			services.AddSingleton<DocumentFileService>();
			services.AddSingleton<ScriptDecompilationService>();
			services.AddSingleton<ArchiveScriptExtractor>();
			services.AddSingleton<ExternalChangeDetector>();
			services.AddSingleton<MessageAssociationService>();
			services.AddSingleton<ProcedureOperations>();
			services.AddSingleton<ReferenceResultsPresenter>();
			services.AddSingleton<ProjectSymbolService>();
			services.AddSingleton<CodeIntelligenceOperations>();
			services.AddSingleton<WorkspaceService>();
			services.AddSingleton<IWorkspace>(provider => provider.GetRequiredService<WorkspaceService>());
			services.AddSingleton<IWorkspaceManager>(provider => provider.GetRequiredService<WorkspaceService>());
			services.AddSingleton<BuildCoordinator>();
			services.AddSingleton<ActiveEditorTracker>();
			services.AddSingleton<CommandLineHandler>();
			services.AddSingleton<ProjectSessionService>();

			// View models, dialogs and windows.
			services.AddSingleton<SearchBarViewModel>();
			services.AddSingleton<ProceduresPanelViewModel>();
			services.AddSingleton<VariablesPanelViewModel>();
			services.AddSingleton<FunctionsPanelViewModel>();
			services.AddSingleton<IUserMacroSource, UserKeywordMacroSource>();
			services.AddSingleton<ProjectExplorerViewModel>();
			services.AddSingleton<MainViewModel>();
			services.AddSingleton<MainWindow>();

			ConfigureIntegrations(services);
		}

		/// <summary>
		/// INTEGRATION SEAMS: the modules written separately plug in here. Everything they need from the shell
		/// (<c>ISettingsService</c>, <c>IDialogService</c>, <c>IThemeService</c>, <c>IBatchCompileService</c>, <c>IMessageFileService</c>,
		/// <c>IWorkspace</c>…) is registered above.
		/// </summary>
		private static void ConfigureIntegrations(IServiceCollection services)
		{
			// Tool windows: settings, About, Go to line, batch compiler, headers picker (IToolWindowService).
			services.AddToolWindows();

			// Message editor window (IMessageEditorService).
			services.AddMessageEditor();

			// Script tools: procedure name prompt, scripts.lst, advanced search, flowchart, dialogue preview, function rules.
			services.AddScriptTools();

			// Dialogue tools (Core): node parser, flowchart builder/store, preview, rules and custom node snippets.
			services.AddDialogueTools(
				provider => Path.Combine(provider.GetRequiredService<AppPaths>().SettingsDirectory, "DialogFunc.cfg"),
				provider => Path.Combine(provider.GetRequiredService<AppPaths>().SettingsDirectory, "CustomCode.ini"));

			// Flowchart, node code editor and dialogue preview windows.
			services.AddFlowchartTools();

			// Dialogue function rules, user function and macro rename preview dialogs (IDialogueDialogsService).
			services.AddDialogueDialogs();

			// Script editor: shared services of the editor views. Resources/EditorTemplates.xaml maps DocumentViewModel to
			// Views.Editor.ScriptEditorView with an implicit DataTemplate; the services reach the views through an inherited
			// attached property that WireEditorServices sets on the main window.
			services.AddScriptEditor();
		}
	}
}
