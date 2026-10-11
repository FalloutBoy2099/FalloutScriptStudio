namespace FalloutScriptStudio.Services.ToolWindows
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;

	/// <summary>
	/// Opens the small Fluent tool windows of the application: settings, About, Go to line, the batch compiler and the headers picker.
	/// </summary>
	public interface IToolWindowService
	{
		/// <summary>
		/// Shows the settings dialog modally. The edited settings are applied (and saved) only when the user confirms.
		/// </summary>
		/// <returns><see langword="true"/> when the settings were applied; <see langword="false"/> when the dialog was cancelled.</returns>
		Task<Boolean> ShowSettingsAsync();

		/// <summary>
		/// Shows the About window modally.
		/// </summary>
		void ShowAbout();

		/// <summary>
		/// Asks the user for a line number.
		/// </summary>
		/// <param name="currentLine">The line the caret is on (1-based); it is the initial value.</param>
		/// <param name="lineCount">The number of lines of the document; larger numbers are limited to it.</param>
		/// <returns>The chosen line (1-based), or <see langword="null"/> when the prompt was cancelled.</returns>
		Task<Int32?> PromptGoToLineAsync(Int32 currentLine, Int32 lineCount);

		/// <summary>
		/// Compiles scripts in a batch while showing a progress window with the failed scripts and their output.
		/// The compiler options come from the current settings; as in the old editor a mass compilation always writes
		/// to the configured output folder (the "compile next to the source" option is ignored) and is refused when that folder is not set.
		/// The window stays open after the run so the user can read the output; the returned task completes when the run is over.
		/// </summary>
		/// <param name="scriptPaths">The scripts to compile.</param>
		/// <returns>The summary of the run; an empty summary when nothing was compiled because there was nothing to do or the output folder is not set.</returns>
		Task<BatchCompileResult> RunBatchCompileAsync(IReadOnlyList<String> scriptPaths);

		/// <summary>
		/// Lets the user pick one of the header files of the configured headers folder.
		/// </summary>
		/// <param name="chooseAndKeepOpen">
		/// Called with the header path when the user chooses a header while holding Ctrl; the window then stays open so that several headers
		/// can be chosen one after another (as in the old editor). Ctrl is ignored when <see langword="null"/>.
		/// </param>
		/// <returns>The full path of the chosen header, or <see langword="null"/> when cancelled, when only headers chosen with Ctrl were handled, or when the headers folder is not available (the user is told).</returns>
		Task<String?> PickHeaderAsync(Func<String, Task>? chooseAndKeepOpen = null);
	}
}
