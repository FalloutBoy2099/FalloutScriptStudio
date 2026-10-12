namespace FalloutScriptStudio.Startup
{
	using System;
	using System.IO;

	/// <summary>
	/// What the exit does: saves the settings (only when they were read), deletes the scratch folder (only when this process owns it) and
	/// releases the single-instance lock and the services. Nothing here may stop the exit.
	/// </summary>
	public sealed class ShutdownCoordinator
	{
		private readonly StartupSession _session;
		private readonly IStartupFolders _folders;
		private readonly Action<String> _log;

		/// <summary>Initializes a new instance of the <see cref="ShutdownCoordinator"/> class.</summary>
		/// <param name="session">What the start-up produced.</param>
		/// <param name="folders">The folders of the installation.</param>
		/// <param name="log">Writes a line to the log.</param>
		public ShutdownCoordinator(StartupSession session, IStartupFolders folders, Action<String> log)
		{
			ArgumentNullException.ThrowIfNull(session);
			ArgumentNullException.ThrowIfNull(folders);
			ArgumentNullException.ThrowIfNull(log);
			this._session = session;
			this._folders = folders;
			this._log = log;
		}

		/// <summary>Runs the exit.</summary>
		public void Run()
		{
			if (this._session.SettingsLoaded)
			{
				try
				{
					this._session.Services?.SaveSettings();
				}
				catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
				{
					this._log("Saving the settings failed: " + ex.Message);
				}
			}
			else
			{
				this._log("The settings were not loaded; the settings files are left as they are.");
			}

			if (this._session.OwnsScratch)
			{
				StartupLogging.LogProblems(this._log, "Deleting the scratch folder", this._folders.DeleteScratch());
			}

			this._session.Dispose();
		}
	}
}
