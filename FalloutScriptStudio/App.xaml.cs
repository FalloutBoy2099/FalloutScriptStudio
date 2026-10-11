namespace FalloutScriptStudio
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Threading.Tasks;
	using System.Windows;
	using System.Windows.Threading;
	using FalloutScriptStudio.Core.Archives;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.Decompilation;
	using FalloutScriptStudio.Core.Dialogue;
	using FalloutScriptStudio.Core.Functions;
	using FalloutScriptStudio.Core.Messages;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Core.Parsing.Interop.Host;
	using FalloutScriptStudio.Core.Refactoring;
	using FalloutScriptStudio.Core.TextTools;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Editor;
	using FalloutScriptStudio.Localization;
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

	/// <summary>The application: composition root, start-up flow and shutdown.</summary>
	public partial class App : Application, IDisposable
	{
		private const String ApplicationName = "Fallout Script Studio";

		private ServiceProvider? _services;
		private SingleInstanceService? _singleInstance;
		private AppPaths? _paths;
		private FileLoggerProvider? _log;

		/// <summary>Whether the settings were read: until then the service holds blank defaults that must never reach the files.</summary>
		private Boolean _settingsLoaded;

		/// <summary>Whether this process prepared the scratch folder and so alone may delete it (the stand-alone message editor shares it with a main editor).</summary>
		private Boolean _ownsScratch;

		protected override async void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);
			this.ShutdownMode = ShutdownMode.OnExplicitShutdown;
			Loc.Initialize(Loc.DetectSystemLanguage());

			String baseDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			Directory.SetCurrentDirectory(baseDirectory);
			this._paths = AppPaths.ForInstallation(baseDirectory);
			this._log = new FileLoggerProvider(this._paths.LogFile);
			this.HookUnhandledExceptions();

			try
			{
				await this.StartAsync(e.Args, baseDirectory);
			}
			catch (Exception ex)
			{
				this._log.Write("Start-up failed: " + ex);
				MessageBox.Show(Loc.Format("Str.Shell.App.StartFailed", ex.Message), ApplicationName, MessageBoxButton.OK, MessageBoxImage.Error);
				this.Shutdown(1);
			}
		}

		protected override void OnExit(ExitEventArgs e)
		{
			if (this._settingsLoaded)
			{
				try
				{
					this._services?.GetService<ISettingsService>()?.Save();
				}
				catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
				{
					this._log?.Write("Saving the settings failed: " + ex.Message);
				}
			}
			else
			{
				this._log?.Write("The settings were not loaded; the settings files are left as they are.");
			}

			if (this._ownsScratch && this._paths != null)
			{
				this.LogProblems("Deleting the scratch folder", this._paths.DeleteScratchDirectory());
			}

			this.Dispose();
			base.OnExit(e);
		}

		/// <summary>Releases the single instance lock, the service provider (which disposes the singletons it created) and the log.</summary>
		public void Dispose()
		{
			this._singleInstance?.Dispose();
			this._singleInstance = null;
			this._services?.Dispose();
			this._services = null;
			this._log?.Dispose();
			this._log = null;
			GC.SuppressFinalize(this);
		}

		private void LogProblems(String operation, IReadOnlyList<String> problems)
		{
			foreach (String problem in problems)
			{
				this._log?.Write(operation + ": " + problem);
			}
		}

		private static Boolean IsMessageFile(String[] arguments)
		{
			return arguments.Length > 0 && String.Equals(Path.GetExtension(arguments[0]), ".msg", StringComparison.OrdinalIgnoreCase);
		}

		private static void ConfigureServices(IServiceCollection services, AppPaths paths, FileLoggerProvider log, SingleInstanceService singleInstance)
		{
			services.AddSingleton(paths);
			services.AddSingleton(singleInstance);
			services.AddLogging(builder => builder.AddProvider(log).SetMinimumLevel(LogLevel.Information));

			// Core: settings.
			services.AddSingleton<LegacySettingsImporter>();
			services.AddSingleton<ISettingsService, SettingsService>();

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
			services.AddSingleton<FileAssociationService>();
			services.AddSingleton<IShellService, ShellService>();
			services.AddSingleton<ISoundService, SoundService>();
			services.AddSingleton<IShellNotificationService, ShellNotificationService>();
			services.AddSingleton<EditorViewOptions>();
			services.AddSingleton<ShellLayoutStore>();

			// Core: text tools (search, procedure text, include insertion), refactoring and the functions catalog.
			services.AddTextTools();
			services.AddRefactoring();
			services.AddFunctionCatalog(
				_ => new FunctionCatalogPaths(Path.Combine(paths.DescriptionsDirectory, "functions.ini"), Path.Combine(paths.SettingsDirectory, "userfunctions.ini")),
				_ => Path.Combine(paths.SettingsDirectory, "user-keywords.json"));

			// Workspace.
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
			services.AddSingleton<WorkspaceService>();
			services.AddSingleton<IWorkspace>(provider => provider.GetRequiredService<WorkspaceService>());
			services.AddSingleton<IWorkspaceManager>(provider => provider.GetRequiredService<WorkspaceService>());
			services.AddSingleton<BuildCoordinator>();
			services.AddSingleton<ActiveEditorTracker>();
			services.AddSingleton<CommandLineHandler>();

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

		/// <summary>INTEGRATION SEAM: gives the editor views (created by the DataTemplate) their shared services.</summary>
		private static void WireEditorServices(MainWindow window, IServiceProvider services)
		{
			ScriptEditorView.SetServices(window, services.GetRequiredService<ScriptEditorServices>());
		}

		private async Task StartAsync(String[] arguments, String baseDirectory)
		{
			SingleInstanceService singleInstance = new SingleInstanceService(baseDirectory, diagnostics: this._log!.Write);
			Boolean primary = singleInstance.TryAcquire();
			if (!primary && arguments.Length > 0)
			{
				// The running instance may still be starting: the send waits for it. When it never answers, it has probably ended meanwhile.
				this._log!.Write("Passing the command line to the running instance...");
				if (await singleInstance.SendArgumentsAsync(arguments))
				{
					singleInstance.Dispose();
					this.Shutdown();
					return;
				}

				this._log!.Write("The running instance did not take the command line.");
				primary = singleInstance.TryAcquire();
			}

			if (!primary)
			{
				MessageBox.Show(Loc.Get("Str.Shell.App.AlreadyRunning"), ApplicationName, MessageBoxButton.OK, MessageBoxImage.Information);
				singleInstance.Dispose();
				this.Shutdown();
				return;
			}

			this._singleInstance = singleInstance;
			ServiceCollection collection = new ServiceCollection();
			ConfigureServices(collection, this._paths!, this._log!, singleInstance);
			this._services = collection.BuildServiceProvider();

			// The scratch folder, the settings (the legacy files are imported automatically) and the theme.
			// None of this may stop the settings from being read: a failure here used to end the start-up before Load, and the exit then
			// wrote the blank defaults over the real settings files.
			Boolean standalone = IsMessageFile(arguments);
			this.LogProblems("Preparing the folders", this._paths!.EnsureCreated());
			if (!standalone)
			{
				// The stand-alone message editor runs next to a main editor that owns the scratch folder: it neither purges nor deletes it.
				this.LogProblems("Preparing the scratch folder", this._paths.PrepareScratchDirectory());
				this._ownsScratch = true;
			}

			ISettingsService settings = this._services.GetRequiredService<ISettingsService>();
			settings.Load();
			this._settingsLoaded = true;
			await this._services.GetRequiredService<IUserKeywordStore>().LoadAsync();
			await this._services.GetRequiredService<IFunctionCatalogService>().LoadAsync();
			await this._services.GetRequiredService<IDialogueRulesService>().LoadAsync();
			await this._services.GetRequiredService<INodeCodeSnippetStore>().LoadAsync();
			ShellLayoutStore layoutStore = this._services.GetRequiredService<ShellLayoutStore>();
			this._services.GetRequiredService<IThemeService>().Apply(GetStartupTheme(layoutStore, settings));
			Loc.Initialize(layoutStore.Load().Language ?? Loc.DetectSystemLanguage());

			if (standalone)
			{
				// Stand-alone message editor: the main window is not created, and other editors may start. It shares the settings files with
				// them: the settings service only writes what this process changed (and merges it into the files as they are then).
				singleInstance.Dispose();
				this._singleInstance = null;
				this.RunStandaloneMessageEditor(arguments[0]);
				return;
			}

			this._log!.Write("Run main editor...");
			MainWindow window = this._services.GetRequiredService<MainWindow>();
			this.MainWindow = window;
			WireEditorServices(window, this._services);
			window.Closed += (_, _) => this.Shutdown();
			window.Show();

			CommandLineHandler handler = this._services.GetRequiredService<CommandLineHandler>();
			singleInstance.ArgumentsReceived += (_, forwarded) => this.Dispatcher.BeginInvoke(new Action(async () =>
			{
				if (await handler.HandleAsync(forwarded))
				{
					BringToFront(window);
				}
			}));
			singleInstance.StartListening();
			await handler.HandleAsync(arguments);
		}

		private void RunStandaloneMessageEditor(String path)
		{
			this._log!.Write("Run only message editor...");
			IMessageEditorService editor = this._services!.GetRequiredService<IMessageEditorService>();
			editor.AllWindowsClosed += (_, _) => this.Shutdown();
			if (!editor.Open(new MessageEditorOpenOptions { FilePath = Path.GetFullPath(path), StartMaximized = true }))
			{
				this.Shutdown();
			}
		}

		/// <summary>The stored mode; the legacy light / dark choice after an import of the old settings; otherwise the Windows mode.</summary>
		private static AppTheme GetStartupTheme(ShellLayoutStore store, ISettingsService settings)
		{
			if (store.Exists)
			{
				return store.Load().Theme;
			}

			if (settings.ImportedLegacyData)
			{
				return settings.Settings.Editor.Theme == HighlightTheme.Dark ? AppTheme.Dark : AppTheme.Light;
			}

			return AppTheme.System;
		}

		private static void BringToFront(Window window)
		{
			if (window.WindowState == WindowState.Minimized)
			{
				window.WindowState = WindowState.Normal;
			}

			window.Activate();
		}

		private void HookUnhandledExceptions()
		{
			this.DispatcherUnhandledException += (_, args) =>
			{
				this._log?.Write("Unhandled exception: " + args.Exception);
				MessageBox.Show(
					Loc.Format("Str.Shell.App.UnexpectedError", args.Exception.Message),
					ApplicationName,
					MessageBoxButton.OK,
					MessageBoxImage.Error);
				args.Handled = true;
			};
			AppDomain.CurrentDomain.UnhandledException += (_, args) => this._log?.Write("Fatal exception: " + args.ExceptionObject);
			TaskScheduler.UnobservedTaskException += (_, args) =>
			{
				this._log?.Write("Unobserved task exception: " + args.Exception);
				args.SetObserved();
			};
		}
	}
}
