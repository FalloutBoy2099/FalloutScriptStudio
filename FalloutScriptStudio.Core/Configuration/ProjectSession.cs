namespace FalloutScriptStudio.Core.Configuration
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	/// <summary>
	/// The documents that were open in a project when it was left: the paths relative to the project folder (forward slashes) and the one
	/// that was active. The caret line and the folded procedures are not kept here, they are already remembered per script
	/// (<see cref="ScriptPositions"/>, <see cref="FoldedProcedures"/>) and come back when the document is opened.
	/// </summary>
	public sealed class ProjectSession
	{
		/// <summary>Gets or sets the normalized folder of the project.</summary>
		public String Folder { get; set; } = String.Empty;

		/// <summary>Gets or sets the open documents in tab order, relative to the project folder.</summary>
		public List<String> Documents { get; set; } = new List<String>();

		/// <summary>Gets or sets the active document (one of <see cref="Documents"/>), or <see langword="null"/>.</summary>
		public String? ActiveDocument { get; set; }

		/// <summary>
		/// Creates the session of a project from the paths of its open documents; documents outside the project folder are not remembered.
		/// </summary>
		/// <param name="projectFolder">The project folder.</param>
		/// <param name="openDocuments">The full paths of the open documents in tab order.</param>
		/// <param name="activeDocument">The full path of the active document, or <see langword="null"/>.</param>
		/// <returns>The session.</returns>
		public static ProjectSession Create(String projectFolder, IEnumerable<String> openDocuments, String? activeDocument)
		{
			ArgumentException.ThrowIfNullOrEmpty(projectFolder);
			ArgumentNullException.ThrowIfNull(openDocuments);

			String folder = ProjectLocator.Normalize(projectFolder);
			List<String> documents = new List<String>();
			foreach (String path in openDocuments)
			{
				if (IsInside(folder, path))
				{
					String stored = ProjectSettingsFile.ToStoredPath(folder, path);
					if (!documents.Contains(stored, StringComparer.OrdinalIgnoreCase))
					{
						documents.Add(stored);
					}
				}
			}

			String? active = activeDocument is not null && IsInside(folder, activeDocument) ? ProjectSettingsFile.ToStoredPath(folder, activeDocument) : null;
			return new ProjectSession
			{
				Folder = folder,
				Documents = documents,
				ActiveDocument = active is not null && documents.Contains(active, StringComparer.OrdinalIgnoreCase) ? active : null,
			};
		}

		/// <summary>Gets the full paths of the remembered documents.</summary>
		/// <returns>The paths in tab order.</returns>
		public IReadOnlyList<String> GetDocumentPaths()
		{
			return (this.Documents ?? new List<String>()).Select(this.Resolve).ToList();
		}

		/// <summary>Gets the full path of the remembered active document.</summary>
		/// <returns>The path, or <see langword="null"/>.</returns>
		public String? GetActiveDocumentPath()
		{
			return String.IsNullOrEmpty(this.ActiveDocument) ? null : this.Resolve(this.ActiveDocument);
		}

		private static Boolean IsInside(String folder, String path)
		{
			if (String.IsNullOrWhiteSpace(path) || !System.IO.Path.IsPathRooted(path))
			{
				return false;
			}

			String relative = System.IO.Path.GetRelativePath(folder, path);
			return !System.IO.Path.IsPathRooted(relative) && !relative.StartsWith("..", StringComparison.Ordinal);
		}

		private String Resolve(String stored)
		{
			return ProjectSettingsFile.ResolvePath(this.Folder, stored);
		}
	}
}
