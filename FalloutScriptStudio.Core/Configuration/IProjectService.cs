namespace FalloutScriptStudio.Core.Configuration
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// The active project: a folder, optionally with a <c>fss-project.json</c> that overrides some of the global options. The folder is the
	/// <see cref="PathSettings.SolutionFolder"/> of the global settings (so the project explorer and every older code path agree); this service
	/// reads the project file of that folder, hands the overrides to <see cref="ISettingsService.SetActiveProject"/> and keeps the list of
	/// recent projects.
	/// </summary>
	public interface IProjectService
	{
		/// <summary>Occurs after the active project, or its settings, changed.</summary>
		event EventHandler? ProjectChanged;

		/// <summary>Occurs when the project file is found damaged or unreadable, or could not be saved.</summary>
		event EventHandler<SettingsProblem>? ProblemRaised;

		/// <summary>Gets the folder of the active project, or <see langword="null"/> when no project is open.</summary>
		String? Folder { get; }

		/// <summary>Gets a value indicating whether a project is open.</summary>
		Boolean IsOpen { get; }

		/// <summary>Gets the name of the project (the name in its settings, otherwise the name of its folder), or an empty text when none is open.</summary>
		String DisplayName { get; }

		/// <summary>Gets what was found when the project file was read.</summary>
		ProjectFileStatus FileStatus { get; }

		/// <summary>Gets the overrides of the project (absolute paths) or <see langword="null"/> when it has no readable settings file. Do not modify them; edit a <see cref="ProjectSettings.Clone"/>.</summary>
		ProjectSettings? Settings { get; }

		/// <summary>Gets a value indicating whether a settings file can be created for the project (there is none, or it is damaged).</summary>
		Boolean CanCreateSettings { get; }

		/// <summary>Gets the recent project folders, newest first.</summary>
		IReadOnlyList<String> RecentProjects { get; }

		/// <summary>
		/// Opens a folder as the active project and reads its settings file if it has one.
		/// </summary>
		/// <param name="folder">The project folder.</param>
		/// <returns><see langword="false"/> when the folder does not exist (the active project does not change).</returns>
		Boolean Open(String folder);

		/// <summary>Closes the active project: the global settings alone are in effect again.</summary>
		void Close();

		/// <summary>Creates the settings file of the active project with the values that are in effect now.</summary>
		/// <returns><see langword="true"/> when the file was written.</returns>
		Boolean CreateSettings();

		/// <summary>Writes the settings file of the active project and makes the settings take effect.</summary>
		/// <param name="settings">The settings (absolute paths).</param>
		/// <returns><see langword="true"/> when the file was written; <see langword="false"/> when there is no project, the existing file cannot be read, or the write failed (the problem is raised).</returns>
		Boolean SaveSettings(ProjectSettings settings);

		/// <summary>Reads the project file again (it may have been edited by hand).</summary>
		void Reload();

		/// <summary>Finds the project a file belongs to: the nearest folder with a project file, at most <see cref="ProjectLocator.MaxLevels"/> levels up.</summary>
		/// <param name="path">The full path of the file.</param>
		/// <returns>The project folder, or <see langword="null"/>.</returns>
		String? FindProjectFolder(String path);

		/// <summary>Removes a folder from the recent projects.</summary>
		/// <param name="folder">The project folder.</param>
		void RemoveRecent(String folder);

		/// <summary>Clears the recent projects.</summary>
		void ClearRecent();
	}
}
