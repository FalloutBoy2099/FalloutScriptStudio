namespace FalloutScriptStudio
{
	using System;
	using System.IO;
	using System.Threading.Tasks;
	using System.Windows;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Startup;
	using Microsoft.Extensions.DependencyInjection;

	/// <summary>The application: a thin WPF shell around the start-up and exit logic of <c>FalloutScriptStudio.Startup</c>.</summary>
	public partial class App : Application, IDisposable
	{
		private const String ApplicationName = "Fallout Script Studio";

		private readonly StartupSession _session = new StartupSession();
		private ShutdownCoordinator? _shutdown;
		private FileLoggerProvider? _log;

		protected override async void OnStartup(StartupEventArgs e)
		{
			base.OnStartup(e);
			this.ShutdownMode = ShutdownMode.OnExplicitShutdown;
			ApplicationStartupHost host = new ApplicationStartupHost(this, ApplicationName);
			host.InitializeLanguage(null);

			String baseDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			Directory.SetCurrentDirectory(baseDirectory);
			AppPaths paths = AppPaths.ForInstallation(baseDirectory);
			FileLoggerProvider log = new FileLoggerProvider(paths.LogFile);
			this._log = log;
			this.HookUnhandledExceptions();

			AppPathsFolders folders = new AppPathsFolders(paths);
			this._shutdown = new ShutdownCoordinator(this._session, folders, log.Write);
			StartupCoordinator coordinator = new StartupCoordinator(
				host,
				folders,
				() => new SingleInstanceService(baseDirectory, diagnostics: log.Write),
				singleInstance =>
				{
					ServiceCollection collection = new ServiceCollection();
					AppServices.ConfigureServices(collection, paths, log, singleInstance);
					return new ContainerStartupServices(collection.BuildServiceProvider());
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
			this._log?.Dispose();
			this._log = null;
			GC.SuppressFinalize(this);
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
