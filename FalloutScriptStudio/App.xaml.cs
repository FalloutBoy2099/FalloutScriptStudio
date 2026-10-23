namespace FalloutScriptStudio
{
	using System;
	using System.IO;
	using System.Threading.Tasks;
	using System.Windows;
	using Microsoft.Extensions.DependencyInjection;
	using Core.Configuration;
	using Localization;
	using Services;
	using Startup;
	using UiTesting;

	/// <summary>The application: a thin WPF shell around the start-up and exit logic of <c>FalloutScriptStudio.Startup</c>.</summary>
	public partial class App : Application, IDisposable
	{
		private const String ApplicationName = "Fallout Script Studio";

		private readonly StartupSession _session = new();
		private ShutdownCoordinator? _shutdown;
		private FileLoggerProvider? _log;
		private UiTestWindowGuard? _windowGuard;
		private UiTestMode _uiTest = UiTestMode.Off;

		protected override async void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);
			this.ShutdownMode = ShutdownMode.OnExplicitShutdown;
			String baseDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			Directory.SetCurrentDirectory(baseDirectory);

			// The opt-in mode of the UI smoke tests (FSS_UI_TEST=1), read here and nowhere else; without it nothing below changes.
			UiTestMode uiTest = UiTestMode.Read(baseDirectory);
			this._uiTest = uiTest;
			if (uiTest.IsEnabled)
			{
				this._windowGuard = UiTestWindowGuard.Install();
			}

			AppPaths paths = uiTest.CreatePaths(baseDirectory);
			if (uiTest.IsEnabled)
			{
				Directory.CreateDirectory(paths.UserDataDirectory ?? baseDirectory);
			}

			FileLoggerProvider log = new(paths.LogFile);
			this._log = log;
			if (uiTest.IsEnabled)
			{
				log.Write("UI test mode: data folder " + uiTest.DataDirectory + ", window guard " + (this._windowGuard?.IsInstalled == true ? "installed" : "NOT installed"));
			}

			ApplicationStartupHost host = new(this, ApplicationName, uiTest, log.Write);
			host.InitializeLanguage(uiTest.OverridesLayout ? uiTest.Language : null);
			this.HookUnhandledExceptions();

			AppPathsFolders folders = new(paths);
			this._shutdown = new ShutdownCoordinator(this._session, folders, log.Write);
			StartupCoordinator coordinator = new(
				host,
				folders,
				() => new SingleInstanceService(uiTest.GetInstanceKey(baseDirectory), diagnostics: log.Write),
				singleInstance =>
				{
					ServiceCollection collection = new();
					AppServices.ConfigureServices(collection, paths, log, singleInstance, uiTest);
					return new ContainerStartupServices(collection.BuildServiceProvider(), uiTest);
				},
				this._session,
				log.Write);
			await coordinator.RunAsync(e.Args);
		}

		protected override void OnExit(ExitEventArgs e)
		{
			this._shutdown?.Run();
			this.Dispose();
			base.OnExit(e);
		}

		/// <summary>Releases the single instance lock, the service provider (which disposes the singletons it created) and the log.</summary>
		public void Dispose()
		{
			this._session.Dispose();
			this._windowGuard?.Dispose();
			this._windowGuard = null;
			this._log?.Dispose();
			this._log = null;
			GC.SuppressFinalize(this);
		}

		private void HookUnhandledExceptions()
		{
			this.DispatcherUnhandledException += (_, args) =>
			{
				this._log?.Write("Unhandled exception: " + args.Exception);
				if (this._uiTest.ActivatesWindows)
				{
					MessageBox.Show(
						Loc.Format("Str.Shell.App.UnexpectedError", args.Exception.Message),
						ApplicationName,
						MessageBoxButton.OK,
						MessageBoxImage.Error);
				}

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