namespace FalloutScriptStudio.Core.Configuration
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// The most recently opened script files and message files. Both lists are ordered oldest first.
	/// </summary>
	public sealed class RecentFiles
	{
		/// <summary>The maximum number of entries per list.</summary>
		public const Int32 MaxItems = 40;

		/// <summary>The maximum number of recent projects.</summary>
		public const Int32 MaxProjects = 10;

		/// <summary>Gets or sets the recent script files, oldest first.</summary>
		public List<String> Scripts { get; set; } = new List<String>();

		/// <summary>Gets or sets the recent message files, oldest first.</summary>
		public List<String> Messages { get; set; } = new List<String>();

		/// <summary>Gets or sets the recent project folders, oldest first.</summary>
		public List<String> Projects { get; set; } = new List<String>();

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
			this.Projects ??= new List<String>();
			return Add(this.Projects, folder, MaxProjects);
		}

		/// <summary>Removes a project folder from the list.</summary>
		/// <param name="folder">The project folder.</param>
		public void RemoveProject(String folder)
		{
			this.Projects ??= new List<String>();
			Remove(this.Projects, folder);
		}

		/// <summary>Clears the list of recent projects.</summary>
		public void ClearProjects()
		{
			this.Projects ??= new List<String>();
			this.Projects.Clear();
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
