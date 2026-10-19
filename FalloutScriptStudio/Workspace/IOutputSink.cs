namespace FalloutScriptStudio.Workspace
{
	using System;
	using System.Collections.Generic;
	using Core.Compilation;

	/// <summary>What the workspace and the build services need from the output panel at the bottom of the main window.</summary>
	public interface IOutputSink
	{
		/// <summary>
		/// Shows search results (or the references of a symbol) in a new closable tab and brings it to the front; the Errors tab is not touched,
		/// so the next refresh, compilation or clear does not overwrite the results.
		/// </summary>
		/// <param name="results">The results (type <see cref="BuildErrorType.Search"/>).</param>
		/// <param name="titleKey">The localization key of the tab title.</param>
		/// <param name="titleArgument">The argument of the title, or <see langword="null"/> when the title has none.</param>
		void ShowSearchResults(IReadOnlyList<BuildError> results, String titleKey, String? titleArgument = null);

		/// <summary>
		/// Replaces the Errors tab with the build errors of several scripts (the result of "Compile All Open") until the next refresh,
		/// compilation or change of the active script.
		/// </summary>
		/// <param name="errors">The errors, in display order.</param>
		void ShowBuildErrors(IReadOnlyList<BuildError> errors);

		/// <summary>Writes a short message to the status bar.</summary>
		void ReportStatus(String message);

		/// <summary>Replaces the text of the Build tab (used while a tool is running).</summary>
		void SetBuildText(String text);

		/// <summary>Appends a line to the Build tab.</summary>
		void AppendBuildLine(String line);

		/// <summary>Brings a tab of the output panel to the front and makes sure the panel is visible.</summary>
		void ShowTab(OutputTab tab);

		/// <summary>Clears the Errors tab.</summary>
		void ClearErrors();
	}
}