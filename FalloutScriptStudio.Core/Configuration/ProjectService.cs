namespace FalloutScriptStudio.Core.Configuration
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading;
	using Microsoft.Extensions.Logging;

	/// <summary>
	/// <see cref="IProjectService"/> on top of the settings service: the active project is the solution folder of the global settings.
	/// </summary>
	/// <remarks>
	/// The project file of the active project is watched (<see cref="ProjectFileWatcher"/>): a change made by hand is reloaded, an invalid
	/// content is refused, and the editor's own saves are recognised by their content and ignored. The reload runs on the thread the service
	/// was created on (the user interface thread) when it has a synchronization context.
	/// </remarks>
	public sealed class ProjectService : IProjectService, IDisposable
	{
		private readonly ISettingsService _settings;
		private readonly ILogger<ProjectService> _logger;
		private readonly ProjectFileWatchOptions _watchOptions;
		private readonly SynchronizationContext? _context = SynchronizationContext.Current;
		private ProjectFileWatcher? _watcher;
		private String? _folder;
		private ProjectFileStatus _status = ProjectFileStatus.Missing;
		private ProjectSettings? _project;
		private Boolean _initialized;

		/// <summary>
		/// Initializes a new instance of the <see cref="ProjectService"/> class and reads the project file of the current solution folder.
		/// </summary>
		/// <param name="settings">The settings service.</param>
		/// <param name="logger">The logger.</param>
		/// <param name="watchOptions">How the project file is watched; by default <see cref="ProjectFileWatchOptions.Default"/>.</param>
		public ProjectService(ISettingsService settings, ILogger<ProjectService> logger, ProjectFileWatchOptions? watchOptions = null)
		{
			ArgumentNullException.ThrowIfNull(settings);
			ArgumentNullException.ThrowIfNull(logger);
			this._settings = settings;
			this._logger = logger;
			this._watchOptions = watchOptions ?? ProjectFileWatchOptions.Default;
			this._settings.SettingsChanged += (_, _) => this.Synchronize(false, null);
			this.Synchronize(true, null);
		}

		/// <inheritdoc/>
		public event EventHandler? ProjectChanged;

		/// <inheritdoc/>
		public event EventHandler<SettingsProblem>? ProblemRaised;

		/// <inheritdoc/>
		public event EventHandler<ProjectFileChangedEventArgs>? FileChangedOnDisk;

		/// <inheritdoc/>
		public String? Folder => this._folder;

		/// <inheritdoc/>
		public Boolean IsOpen => this._folder is not null;

		/// <inheritdoc/>
		public String DisplayName
		{
			get
			{
				if (this._folder is null)
				{
					return String.Empty;
				}

				if (this._project?.Name is { Length: > 0 } name)
				{
					return name;
				}

				String leaf = Path.GetFileName(this._folder);
				return leaf.Length > 0 ? leaf : this._folder;
			}
		}

		/// <inheritdoc/>
		public ProjectFileStatus FileStatus => this._status;

		/// <inheritdoc/>
		public ProjectSettings? Settings => this._project;

		/// <inheritdoc/>
		public Boolean CanCreateSettings => this._folder is not null && this._project is null && this._status is ProjectFileStatus.Missing or ProjectFileStatus.Damaged;

		/// <inheritdoc/>
		public IReadOnlyList<String> RecentProjects => (this._settings.Recent.Projects ?? new List<String>()).AsEnumerable().Reverse().ToList();

		/// <inheritdoc/>
		public Boolean Open(String folder)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(folder);

			String? normalized = NormalizeOrNull(folder);
			if (normalized is null || !Directory.Exists(normalized))
			{
				return false;
			}

			if (SameFolder(normalized, this._folder))
			{
				this.Synchronize(true, null);
				return true;
			}

			this._settings.Update(settings => settings.Paths.SolutionFolder = normalized);
			return true;
		}

		/// <inheritdoc/>
		public void Close()
		{
			if (this._settings.Settings.Paths.SolutionFolder is not null)
			{
				this._settings.Update(settings => settings.Paths.SolutionFolder = null);
			}
		}

		/// <inheritdoc/>
		public Boolean CreateSettings()
		{
			if (!this.CanCreateSettings)
			{
				return false;
			}

			return this.SaveSettings(ProjectSettings.CreateFrom(this._settings.Effective.Settings, null));
		}

		/// <inheritdoc/>
		public Boolean SaveSettings(ProjectSettings settings)
		{
			ArgumentNullException.ThrowIfNull(settings);

			String? folder = this._folder;
			if (folder is null)
			{
				return false;
			}

			String path = ProjectSettingsFile.GetPath(folder);
			if (this._status == ProjectFileStatus.Unreadable)
			{
				// Whatever is in the file is unknown: it is never replaced blindly.
				this.RaiseProblem(new SettingsProblem(SettingsProblemKind.Unreadable, path));
				return false;
			}

			ProjectSettings saved = settings.Clone();
			saved.Normalize();
			try
			{
				// The watcher must know the content before the file changes: the event of this very write is then recognised as our own.
				Byte[] content = ProjectSettingsFile.Serialize(folder, saved);
				this._watcher?.Acknowledge(content);
				AtomicFile.WriteAllBytes(path, content);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				this._watcher?.Forget();
				this._logger.ProjectFileNotSaved(ex, path);
				this.RaiseProblem(new SettingsProblem(SettingsProblemKind.SaveFailed, path, ex.Message));
				return false;
			}

			this._status = ProjectFileStatus.Loaded;
			this._project = saved;
			this.Publish();
			return true;
		}

		/// <inheritdoc/>
		public void Reload()
		{
			this.Synchronize(true, null);
		}

		/// <summary>Stops watching the project file.</summary>
		public void Dispose()
		{
			this.StopWatching();
		}

		/// <inheritdoc/>
		public String? FindProjectFolder(String path)
		{
			return ProjectLocator.FindProjectFolder(path);
		}

		/// <inheritdoc/>
		public void RemoveRecent(String folder)
		{
			ArgumentException.ThrowIfNullOrEmpty(folder);
			this._settings.Recent.RemoveProject(folder);
		}

		/// <inheritdoc/>
		public void ClearRecent()
		{
			this._settings.Recent.ClearProjects();
		}

		private static Boolean SameFolder(String? left, String? right)
		{
			return String.Equals(left, right, StringComparison.OrdinalIgnoreCase);
		}

		private static String? NormalizeOrNull(String? folder)
		{
			if (String.IsNullOrWhiteSpace(folder))
			{
				return null;
			}

			try
			{
				return ProjectLocator.Normalize(folder.Trim());
			}
			catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
			{
				return null;
			}
		}

		/// <summary>Follows the solution folder of the settings: whoever changed it (this service, the project explorer, a settings import), the project file is read again.</summary>
		/// <param name="force">Read the file even when the folder did not change.</param>
		/// <param name="preRead">The content the file watcher already read and verified, used instead of reading the file again.</param>
		private void Synchronize(Boolean force, ProjectFileReadResult? preRead)
		{
			String? wanted = NormalizeOrNull(this._settings.Settings.Paths.SolutionFolder);
			if (!force && this._initialized && SameFolder(wanted, this._folder))
			{
				return;
			}

			this._initialized = true;
			this._folder = wanted;
			this._project = null;
			this._status = ProjectFileStatus.Missing;
			if (wanted is not null)
			{
				ProjectFileReadResult read = preRead ?? ProjectSettingsFile.Read(wanted);
				this._status = read.Status;
				this._project = read.Settings;
				this.ReportReadProblem(wanted, read);
				if (Directory.Exists(wanted))
				{
					this._settings.Recent.AddProject(wanted);
				}

				this.Watch(wanted, read);
			}
			else
			{
				this.StopWatching();
			}

			this.Publish();
		}

		/// <summary>Watches the project file of a folder, starting from the content that was just read.</summary>
		private void Watch(String folder, ProjectFileReadResult read)
		{
			ProjectFileWatcher? existing = this._watcher;
			if (existing is not null && SameFolder(existing.Folder, folder))
			{
				if (read.Status == ProjectFileStatus.Unreadable)
				{
					existing.Forget();
				}
				else
				{
					existing.Acknowledge(read.Content);
				}

				return;
			}

			this.StopWatching();
			if (!this._watchOptions.Enabled)
			{
				return;
			}

			ProjectFileWatcher? created = null;
			created = new ProjectFileWatcher(folder, this._watchOptions, change => this.OnFileChange(created, change), this._logger);
			this._watcher = created;
			if (read.Status == ProjectFileStatus.Unreadable)
			{
				created.Start(null, true);
			}
			else
			{
				created.Start(read.Content, read.Status != ProjectFileStatus.Missing);
			}
		}

		private void StopWatching()
		{
			ProjectFileWatcher? watcher = this._watcher;
			this._watcher = null;
			watcher?.Dispose();
		}

		/// <summary>A verified change of the project file arrived on a thread-pool thread: it is applied on the thread of the service.</summary>
		private void OnFileChange(ProjectFileWatcher? source, ProjectFileChange change)
		{
			if (this._context is not null)
			{
				this._context.Post(_ => this.ApplyFileChange(source, change), null);
			}
			else
			{
				this.ApplyFileChange(source, change);
			}
		}

		private void ApplyFileChange(ProjectFileWatcher? source, ProjectFileChange change)
		{
			if (source is null || !ReferenceEquals(source, this._watcher) || this._folder is not { } folder)
			{
				return;
			}

			ProjectFileError? error = null;
			switch (change.Kind)
			{
				case ProjectFileChangeKind.Modified:
					this.Synchronize(true, change.Read);
					break;
				case ProjectFileChangeKind.Deleted:
					this.Synchronize(true, new ProjectFileReadResult(ProjectFileStatus.Missing, null));
					break;
				default:
					// The last valid settings stay in effect; there is nothing to reload.
					error = change.Error ?? new ProjectFileError(String.Empty);
					this._logger.ProjectFileInvalid(ProjectSettingsFile.GetPath(folder), error.Message);
					break;
			}

			this.FileChangedOnDisk?.Invoke(this, new ProjectFileChangedEventArgs(change.Kind, ProjectSettingsFile.GetPath(folder), error));
		}

		private void ReportReadProblem(String folder, ProjectFileReadResult read)
		{
			String path = ProjectSettingsFile.GetPath(folder);
			switch (read.Status)
			{
				case ProjectFileStatus.Damaged:
					this._logger.ProjectFileDamaged(path);
					this.RaiseProblem(new SettingsProblem(SettingsProblemKind.Damaged, path));
					break;
				case ProjectFileStatus.Unreadable:
					this._logger.ProjectFileUnreadable(path, read.Detail ?? String.Empty);
					this.RaiseProblem(new SettingsProblem(SettingsProblemKind.Unreadable, path, read.Detail));
					break;
			}
		}

		private void RaiseProblem(SettingsProblem problem)
		{
			this.ProblemRaised?.Invoke(this, problem);
		}

		private void Publish()
		{
			this._settings.SetActiveProject(this._folder, this._project);
			this.ProjectChanged?.Invoke(this, EventArgs.Empty);
		}
	}
}
