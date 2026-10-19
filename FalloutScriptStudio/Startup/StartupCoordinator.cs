namespace FalloutScriptStudio.Startup
{
	using System;
	using System.Threading.Tasks;
	using Localization;

	/// <summary>
	/// The start-up sequence: single-instance handling, folder preparation, loading of the settings and the stores, theme and language, then
	/// the main editor or the stand-alone message editor. The window system is reached through <see cref="IStartupHost"/> only.
	/// </summary>
	public sealed class StartupCoordinator
	{
		private readonly IStartupHost _host;
		private readonly IStartupFolders _folders;
		private readonly Func<ISingleInstance> _createSingleInstance;
		private readonly Func<ISingleInstance, IStartupServices> _createServices;
		private readonly StartupSession _session;
		private readonly Action<String> _log;

		/// <summary>Initializes a new instance of the <see cref="StartupCoordinator"/> class.</summary>
		/// <param name="host">The user interface.</param>
		/// <param name="folders">The folders of the installation.</param>
		/// <param name="createSingleInstance">Creates the single-instance lock.</param>
		/// <param name="createServices">Creates the services once this process is the first instance.</param>
		/// <param name="session">Receives what the start-up produced.</param>
		/// <param name="log">Writes a line to the log.</param>
		public StartupCoordinator(
			IStartupHost host,
			IStartupFolders folders,
			Func<ISingleInstance> createSingleInstance,
			Func<ISingleInstance, IStartupServices> createServices,
			StartupSession session,
			Action<String> log)
		{
			ArgumentNullException.ThrowIfNull(host);
			ArgumentNullException.ThrowIfNull(folders);
			ArgumentNullException.ThrowIfNull(createSingleInstance);
			ArgumentNullException.ThrowIfNull(createServices);
			ArgumentNullException.ThrowIfNull(session);
			ArgumentNullException.ThrowIfNull(log);
			this._host = host;
			this._folders = folders;
			this._createSingleInstance = createSingleInstance;
			this._createServices = createServices;
			this._session = session;
			this._log = log;
		}

		/// <summary>
		/// Runs the start-up. A failure is logged, shown to the user and ends the application with the exit code 1; it never escapes.
		/// </summary>
		/// <param name="arguments">The command-line arguments.</param>
		/// <returns>A task that completes when the start-up is over.</returns>
		public async Task RunAsync(String[] arguments)
		{
			ArgumentNullException.ThrowIfNull(arguments);
			try
			{
				await this.StartAsync(arguments);
			}
			catch (Exception ex)
			{
				this._log("Start-up failed: " + ex);
				this._host.ShowError(Loc.Format("Str.Shell.App.StartFailed", ex.Message));
				this._host.Shutdown(1);
			}
		}

		private async Task StartAsync(String[] arguments)
		{
			ISingleInstance singleInstance = this._createSingleInstance();
			StartupPlan plan = StartupPlan.Create(arguments, singleInstance.TryAcquire());
			if (plan.Mode == StartupMode.ForwardToRunningInstance)
			{
				// The running instance may still be starting: the send waits for it. When it never answers, it has probably ended meanwhile.
				this._log("Passing the command line to the running instance...");
				if (await singleInstance.SendArgumentsAsync(arguments))
				{
					singleInstance.Dispose();
					this._host.Shutdown(0);
					return;
				}

				this._log("The running instance did not take the command line.");
				plan = StartupPlan.Create(arguments, singleInstance.TryAcquire());
			}

			if (!plan.IsPrimary)
			{
				this._host.ShowInformation(Loc.Get("Str.Shell.App.AlreadyRunning"));
				singleInstance.Dispose();
				this._host.Shutdown(0);
				return;
			}

			this._session.SingleInstance = singleInstance;
			IStartupServices services = this._createServices(singleInstance);
			this._session.Services = services;

			// The scratch folder, the settings (the legacy files are imported automatically) and the theme.
			// None of this may stop the settings from being read: a failure here used to end the start-up before Load, and the exit then
			// wrote the blank defaults over the real settings files.
			StartupLogging.LogProblems(this._log, "Preparing the folders", this._folders.EnsureCreated());
			if (plan.OwnsScratch)
			{
				StartupLogging.LogProblems(this._log, "Preparing the scratch folder", this._folders.PrepareScratch());
				this._session.OwnsScratch = true;
			}

			services.LoadSettings();
			this._session.SettingsLoaded = true;

			// The project of the solution folder is read before any document is parsed or compiled.
			services.ActivateProject();
			await services.LoadStoresAsync();
			services.ApplyStartupTheme();
			this._host.InitializeLanguage(services.GetStoredLanguage());

			if (plan.Mode == StartupMode.StandaloneMessageEditor)
			{
				// Stand-alone message editor: the main window is not created, and other editors may start. It shares the settings files with
				// them: the settings service only writes what this process changed (and merges it into the files as they are then).
				singleInstance.Dispose();
				this._session.SingleInstance = null;
				this._log("Run only message editor...");
				this._host.RunStandaloneMessageEditor(services, plan.MessageFilePath!);
				return;
			}

			this._log("Run main editor...");
			await this._host.RunMainEditorAsync(services, singleInstance, arguments);
		}
	}
}