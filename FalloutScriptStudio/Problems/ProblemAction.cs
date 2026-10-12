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

		/// <summary>Gets the folder to open for <see cref="ProblemActionKind.OpenFolder"/>, or the project to switch to for <see cref="ProblemActionKind.SwitchProject"/>.</summary>
		public String? FolderPath { get; private init; }

		/// <summary>Gets the text of the link, in the current language.</summary>
		public String Label => this.Kind switch
		{
			ProblemActionKind.OpenSettings => Loc.Format("Str.Shell.Problem.ActionSettings", Loc.Get("Str.ToolWindows.Page." + this.Page)),
			ProblemActionKind.SwitchProject => Loc.Get("Str.Shell.Problem.ActionSwitchProject"),
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
