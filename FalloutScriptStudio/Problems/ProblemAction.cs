namespace FalloutScriptStudio.Problems
{
	using System;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.ViewModels.ToolWindows;

	/// <summary>The obvious fix of a <see cref="Problem"/>, offered as a link in the Errors tab.</summary>
	public sealed record ProblemAction
	{
		private ProblemAction(ProblemActionKind kind)
		{
			this.Kind = kind;
		}

		/// <summary>Gets what the action does.</summary>
		public ProblemActionKind Kind { get; }

		/// <summary>Gets the settings page to open for <see cref="ProblemActionKind.OpenSettings"/>.</summary>
		public SettingsPage Page { get; private init; }

		/// <summary>Gets the folder to open for <see cref="ProblemActionKind.OpenFolder"/>, or the project to switch to for <see cref="ProblemActionKind.SwitchProject"/>, or the project whose documents are restored for <see cref="ProblemActionKind.RestoreDocuments"/>.</summary>
		public String? FolderPath { get; private init; }

		/// <summary>Gets the 1-based line of the variable to remove for <see cref="ProblemActionKind.RemoveUnusedVariable"/>.</summary>
		public Int32 Line { get; private init; }

		/// <summary>Gets the 1-based column of the variable to remove for <see cref="ProblemActionKind.RemoveUnusedVariable"/>.</summary>
		public Int32 Column { get; private init; }

		/// <summary>Gets the file to open for <see cref="ProblemActionKind.OpenFile"/>, or the script whose variable is removed for <see cref="ProblemActionKind.RemoveUnusedVariable"/>.</summary>
		public String? FilePath { get; private init; }

		/// <summary>Gets the text of the link, in the current language.</summary>
		public String Label => this.Kind switch
		{
			ProblemActionKind.OpenSettings => Loc.Format("Str.Shell.Problem.ActionSettings", Loc.Get("Str.ToolWindows.Page." + this.Page)),
			ProblemActionKind.SwitchProject => Loc.Get("Str.Shell.Problem.ActionSwitchProject"),
			ProblemActionKind.OpenFile => Loc.Get("Str.Shell.Problem.ActionOpenFile"),
			ProblemActionKind.RestoreDocuments => Loc.Get("Str.Shell.Problem.ActionRestoreFiles"),
			ProblemActionKind.RemoveUnusedVariable => Loc.Get("Str.Shell.Problem.ActionRemoveDeclaration"),
			_ => Loc.Get("Str.Shell.Problem.ActionFolder"),
		};

		/// <summary>Creates the action that opens the settings window on a page.</summary>
		/// <param name="page">The page.</param>
		/// <returns>The action.</returns>
		public static ProblemAction OpenSettings(SettingsPage page)
		{
			return new ProblemAction(ProblemActionKind.OpenSettings) { Page = page };
		}

		/// <summary>Creates the action that makes a folder the active project.</summary>
		/// <param name="projectFolder">The project folder.</param>
		/// <returns>The action.</returns>
		public static ProblemAction SwitchProject(String projectFolder)
		{
			ArgumentException.ThrowIfNullOrEmpty(projectFolder);
			return new ProblemAction(ProblemActionKind.SwitchProject) { FolderPath = projectFolder };
		}

		/// <summary>Creates the action that opens a file in the editor.</summary>
		/// <param name="filePath">The file.</param>
		/// <returns>The action.</returns>
		public static ProblemAction OpenFile(String filePath)
		{
			ArgumentException.ThrowIfNullOrEmpty(filePath);
			return new ProblemAction(ProblemActionKind.OpenFile) { FilePath = filePath };
		}

		/// <summary>Creates the action that opens again the documents a project had open.</summary>
		/// <param name="projectFolder">The project folder.</param>
		/// <returns>The action.</returns>
		public static ProblemAction RestoreDocuments(String projectFolder)
		{
			ArgumentException.ThrowIfNullOrEmpty(projectFolder);
			return new ProblemAction(ProblemActionKind.RestoreDocuments) { FolderPath = projectFolder };
		}

		/// <summary>Creates the action that removes the declaration of an unused variable.</summary>
		/// <param name="filePath">The script.</param>
		/// <param name="line">The 1-based line of the name of the variable.</param>
		/// <param name="column">The 1-based column of the name of the variable.</param>
		/// <returns>The action.</returns>
		public static ProblemAction RemoveUnusedVariable(String filePath, Int32 line, Int32 column)
		{
			ArgumentException.ThrowIfNullOrEmpty(filePath);
			return new ProblemAction(ProblemActionKind.RemoveUnusedVariable) { FilePath = filePath, Line = line, Column = column };
		}

		/// <summary>Creates the action that opens a folder in Explorer.</summary>
		/// <param name="folderPath">The folder.</param>
		/// <returns>The action.</returns>
		public static ProblemAction OpenFolder(String folderPath)
		{
			ArgumentException.ThrowIfNullOrEmpty(folderPath);
			return new ProblemAction(ProblemActionKind.OpenFolder) { FolderPath = folderPath };
		}
	}
}
