namespace FalloutScriptStudio.Core.Configuration
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using Microsoft.Extensions.Logging;

	/// <summary>
	/// <see cref="IProjectService"/> on top of the settings service: the active project is the solution folder of the global settings.
	/// </summary>
	public sealed class ProjectService : IProjectService
	{
		private readonly ISettingsService _settings;
		private readonly ILogger<ProjectService> _logger;
		private String? _folder;
		private ProjectFileStatus _status = ProjectFileStatus.Missing;
		private ProjectSettings? _project;
		private Boolean _initialized;

		/// <summary>
		/// Initializes a new instance of the <see cref="ProjectService"/> class and reads the project file of the current solution folder.
		/// </summary>
		/// <param name="settings">The settings service.</param>
		/// <param name="logger">The logger.</param>
		public ProjectService(ISettingsService settings, ILogger<ProjectService> logger)
		{
			ArgumentNullException.ThrowIfNull(settings);
			ArgumentNullException.ThrowIfNull(logger);
			this._settings = settings;
			this._logger = logger;
			this._settings.SettingsChanged += (_, _) => this.Synchronize(false);
			this.Synchronize(true);
		}

		/// <inheritdoc/>
		public event EventHandler? ProjectChanged;

		/// <inheritdoc/>
		public event EventHandler<SettingsProblem>? ProblemRaised;

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
				this.Synchronize(true);
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
				ProjectSettingsFile.Write(folder, saved);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
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
			this.Synchronize(true);
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
		private void Synchronize(Boolean force)
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
				ProjectFileReadResult read = ProjectSettingsFile.Read(wanted);
				this._status = read.Status;
				this._project = read.Settings;
				this.ReportReadProblem(wanted, read);
				if (Directory.Exists(wanted))
				{
					this._settings.Recent.AddProject(wanted);
				}
			}

			this.Publish();
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
