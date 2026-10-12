namespace FalloutScriptStudio.ViewModels.Main
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Linq;
	using System.Threading.Tasks;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Problems;

	/// <summary>Projects: open, recent, close, create the settings file, open the project settings, and the hint about the project of an opened script.</summary>
	public sealed partial class MainViewModel
	{
		private readonly HashSet<String> _offeredProjects = new HashSet<String>(StringComparer.OrdinalIgnoreCase);

		/// <summary>Gets the recent projects, newest first.</summary>
		public ObservableCollection<String> RecentProjects { get; } = new ObservableCollection<String>();

		/// <summary>Gets a value indicating whether a project is open.</summary>
		public Boolean HasProject => this._projects.IsOpen;

		/// <summary>Gets a value indicating whether the open project has a settings file that is in effect.</summary>
		public Boolean HasProjectSettings => this._projects.Settings is not null;

		/// <summary>Gets the tool tip of the project in the status bar: the folder of the project, or what clicking does when there is none.</summary>
		public String ProjectTip => this._projects.Folder is { } folder ? Loc.Format("Str.Shell.Tip.ProjectOpen", folder) : Loc.Get("Str.Shell.Tip.Project");

		/// <summary>Asks for a project folder and opens it.</summary>
		[RelayCommand]
		public async Task OpenProjectAsync()
		{
			String? folder = this._dialogs.PickFolder(Loc.Get("Str.Panels.MsgSelectProjectFolder"), this._projects.Folder);
			if (folder is not null)
			{
				await this.OpenProjectAtAsync(folder);
			}
		}

		/// <summary>Opens a project of the recent list.</summary>
		/// <param name="folder">The project folder.</param>
		[RelayCommand]
		public async Task OpenRecentProjectAsync(String? folder)
		{
			if (!String.IsNullOrEmpty(folder))
			{
				await this.OpenProjectAtAsync(folder);
			}
		}

		/// <summary>Clears the list of recent projects after confirmation.</summary>
		[RelayCommand]
		public async Task ClearRecentProjectsAsync()
		{
			if (this.RecentProjects.Count == 0)
			{
				return;
			}

			if (await this._dialogs.ConfirmAsync(Loc.Get("Str.Shell.Dlg.RecentProjectsTitle"), Loc.Get("Str.Shell.Dlg.ClearRecentProjectsPrompt")))
			{
				this._projects.ClearRecent();
				this.RefreshRecentProjects();
			}
		}

		/// <summary>Closes the active project: the global settings alone are in effect again.</summary>
		[RelayCommand(CanExecute = nameof(HasProject))]
		public void CloseProject()
		{
			this._projects.Close();
		}

		/// <summary>Creates the settings file of the active project with the values in effect now, and shows the project settings.</summary>
		[RelayCommand(CanExecute = nameof(CanCreateProjectSettings))]
		public async Task CreateProjectSettingsAsync()
		{
			if (this._projects.CreateSettings())
			{
				this.Output.ReportStatus(Loc.Format("Str.Shell.Project.SettingsCreated", ProjectSettings.FileName, this._projects.DisplayName));
				await this._toolWindows.ShowProjectSettingsAsync();
			}
		}

		/// <summary>Shows the settings of the active project; without a project it asks for one first.</summary>
		[RelayCommand]
		public async Task ProjectSettingsAsync()
		{
			if (!this._projects.IsOpen)
			{
				await this.OpenProjectAsync();
				return;
			}

			await this._toolWindows.ShowProjectSettingsAsync();
		}

		/// <summary>Makes a folder the active project (the link of the hint about the project of an opened script).</summary>
		/// <param name="folder">The project folder.</param>
		/// <returns>A task that completes when the project is open.</returns>
		public Task SwitchProjectAsync(String folder)
		{
			return this.OpenProjectAtAsync(folder);
		}

		/// <summary>Gets a value indicating whether a settings file can be created for the open project.</summary>
		private Boolean CanCreateProjectSettings()
		{
			return this._projects.CanCreateSettings;
		}

		private async Task OpenProjectAtAsync(String folder)
		{
			if (this._projects.Open(folder))
			{
				return;
			}

			this._projects.RemoveRecent(folder);
			this.RefreshRecentProjects();
			await this._dialogs.ShowErrorAsync(Loc.Get("Str.Shell.Dlg.ProjectTitle"), Loc.Format("Str.Shell.Dlg.ProjectFolderNotFound", folder));
		}

		private void RefreshRecentProjects()
		{
			List<String> recent = this._projects.RecentProjects.ToList();
			if (recent.SequenceEqual(this.RecentProjects, StringComparer.OrdinalIgnoreCase))
			{
				return;
			}

			this.RecentProjects.Clear();
			foreach (String folder in recent)
			{
				this.RecentProjects.Add(folder);
			}
		}

		/// <summary>The project, or its settings, changed: the texts, the toolbar list of definitions and everything parsed with the old options follow.</summary>
		private void OnProjectChanged()
		{
			_ = this.Output.Problems.ClearOrigin(ProblemOrigins.ProjectDetection);
			this.RefreshRecentProjects();
			foreach (String name in new[] { nameof(this.HasProject), nameof(this.HasProjectSettings), nameof(this.ProjectText), nameof(this.ProjectTip), nameof(this.Title) })
			{
				this.OnPropertyChanged(name);
			}

			this.CloseProjectCommand.NotifyCanExecuteChanged();
			this.CreateProjectSettingsCommand.NotifyCanExecuteChanged();
			_ = this._workspace.GuardAsync(this.AfterSettingsChangedAsync);
		}

		/// <summary>A script was opened: when it belongs to a project other than the active one, a hint (not a dialog) offers to switch, once per project and session.</summary>
		private void OfferProjectOf(DocumentViewModel document)
		{
			if (document.FilePath is not { } path || !document.IsParsable)
			{
				return;
			}

			String? folder = this._projects.FindProjectFolder(path);
			if (folder is null || String.Equals(folder, this._projects.Folder, StringComparison.OrdinalIgnoreCase) || !this._offeredProjects.Add(folder))
			{
				return;
			}

			this.Output.Problems.Report(ProblemFactory.ProjectDetected(folder));
			this.Output.ReportStatus(Loc.Format("Str.Shell.Project.Detected", folder));
		}
	}
}
