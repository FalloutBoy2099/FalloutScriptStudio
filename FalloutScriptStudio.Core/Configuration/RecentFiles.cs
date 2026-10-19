namespace FalloutScriptStudio.Core.Configuration
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	/// <summary>
	/// The most recently opened script files and message files. Both lists are ordered oldest first.
	/// </summary>
	public sealed class RecentFiles
	{
		/// <summary>The maximum number of entries per list.</summary>
		public const Int32 MaxItems = 40;

		/// <summary>The maximum number of recent projects.</summary>
		public const Int32 MaxProjects = 10;

		/// <summary>The maximum number of projects whose open documents are remembered.</summary>
		public const Int32 MaxProjectSessions = 20;

		/// <summary>The maximum number of commands of the command palette that are remembered.</summary>
		public const Int32 MaxCommands = 10;

		/// <summary>Gets or sets the recent script files, oldest first.</summary>
		public List<String> Scripts { get; set; } = [];

		/// <summary>Gets or sets the recent message files, oldest first.</summary>
		public List<String> Messages { get; set; } = [];

		/// <summary>Gets or sets the recent project folders, oldest first.</summary>
		public List<String> Projects { get; set; } = [];

		/// <summary>Gets or sets the identifiers of the commands last run from the command palette, oldest first.</summary>
		public List<String> Commands { get; set; } = [];

		/// <summary>Gets or sets the documents that were open when a project was left, the project left last at the end.</summary>
		public List<ProjectSession> ProjectSessions { get; set; } = [];

		/// <summary>
		/// Moves a script to the end of the list, adding it if necessary. The oldest entry is dropped when the list is full.
		/// </summary>
		/// <param name="path">The script path.</param>
		/// <returns>The path that was dropped to make room, or <see langword="null"/>.</returns>
		public String? AddScript(String path)
		{
			return Add(this.Scripts, path, MaxItems);
		}

		/// <summary>
		/// Moves a message file to the end of the list, adding it if necessary. The oldest entry is dropped when the list is full.
		/// </summary>
		/// <param name="path">The message file path.</param>
		/// <returns>The path that was dropped to make room, or <see langword="null"/>.</returns>
		public String? AddMessage(String path)
		{
			return Add(this.Messages, path, MaxItems);
		}

		/// <summary>
		/// Moves a project folder to the end of the list, adding it if necessary. The oldest entry is dropped when the list is full.
		/// </summary>
		/// <param name="folder">The project folder.</param>
		/// <returns>The folder that was dropped to make room, or <see langword="null"/>.</returns>
		public String? AddProject(String folder)
		{
			this.Projects ??= [];
			return Add(this.Projects, folder, MaxProjects);
		}

		/// <summary>Removes a project folder from the list.</summary>
		/// <param name="folder">The project folder.</param>
		public void RemoveProject(String folder)
		{
			this.Projects ??= [];
			Remove(this.Projects, folder);
		}

		/// <summary>Clears the list of recent projects.</summary>
		public void ClearProjects()
		{
			this.Projects ??= [];
			this.Projects.Clear();
		}

		/// <summary>
		/// Moves a command of the command palette to the end of the list, adding it if necessary. The oldest entry is dropped when
		/// <see cref="MaxCommands"/> commands are remembered.
		/// </summary>
		/// <param name="commandId">The identifier of the command.</param>
		public void AddCommand(String commandId)
		{
			ArgumentException.ThrowIfNullOrEmpty(commandId);
			this.Commands ??= [];
			Remove(this.Commands, commandId);
			this.Commands.Add(commandId);
			while (this.Commands.Count > MaxCommands)
			{
				this.Commands.RemoveAt(0);
			}
		}

		/// <summary>Gets the remembered open documents of a project.</summary>
		/// <param name="folder">The project folder.</param>
		/// <returns>The session, or <see langword="null"/> when none is remembered.</returns>
		public ProjectSession? GetProjectSession(String folder)
		{
			ArgumentException.ThrowIfNullOrEmpty(folder);
			this.ProjectSessions ??= [];
			String key = ProjectLocator.Normalize(folder);
			return this.ProjectSessions.LastOrDefault(session => String.Equals(session.Folder, key, StringComparison.OrdinalIgnoreCase));
		}

		/// <summary>
		/// Remembers the open documents of a project, replacing what was remembered for it. The project becomes the newest; the oldest is
		/// dropped when <see cref="MaxProjectSessions"/> projects are remembered. A session without documents forgets the project.
		/// </summary>
		/// <param name="session">The session.</param>
		public void SetProjectSession(ProjectSession session)
		{
			ArgumentNullException.ThrowIfNull(session);
			ArgumentException.ThrowIfNullOrEmpty(session.Folder);
			this.ProjectSessions ??= [];
			String key = ProjectLocator.Normalize(session.Folder);
			this.ProjectSessions.RemoveAll(existing => String.Equals(existing.Folder, key, StringComparison.OrdinalIgnoreCase));
			if (session.Documents.Count == 0)
			{
				return;
			}

			session.Folder = key;
			this.ProjectSessions.Add(session);
			while (this.ProjectSessions.Count > MaxProjectSessions)
			{
				this.ProjectSessions.RemoveAt(0);
			}
		}

		/// <summary>Forgets the open documents of a project.</summary>
		/// <param name="folder">The project folder.</param>
		public void RemoveProjectSession(String folder)
		{
			ArgumentException.ThrowIfNullOrEmpty(folder);
			this.ProjectSessions ??= [];
			String key = ProjectLocator.Normalize(folder);
			this.ProjectSessions.RemoveAll(existing => String.Equals(existing.Folder, key, StringComparison.OrdinalIgnoreCase));
		}

		/// <summary>Removes a script from the list.</summary>
		/// <param name="path">The script path.</param>
		public void RemoveScript(String path)
		{
			Remove(this.Scripts, path);
		}

		/// <summary>Removes a message file from the list.</summary>
		/// <param name="path">The message file path.</param>
		public void RemoveMessage(String path)
		{
			Remove(this.Messages, path);
		}

		/// <summary>Clears the list of recent scripts.</summary>
		public void ClearScripts()
		{
			this.Scripts.Clear();
		}

		private static void Remove(List<String> list, String path)
		{
			list.RemoveAll(item => String.Equals(item, path, StringComparison.OrdinalIgnoreCase));
		}

		private static String? Add(List<String> list, String path, Int32 maxItems)
		{
			ArgumentException.ThrowIfNullOrEmpty(path);

			Remove(list, path);
			String? dropped = null;
			if (list.Count >= maxItems)
			{
				dropped = list[0];
				list.RemoveAt(0);
			}

			list.Add(path);
			return dropped;
		}
	}
}