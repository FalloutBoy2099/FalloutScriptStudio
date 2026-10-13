namespace FalloutScriptStudio.Services.ToolWindows
{
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.ViewModels.ToolWindows;

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
		/// Shows the settings dialog modally with one page selected (the fix offered by a problem such as a missing compiler).
		/// </summary>
		/// <param name="page">The page to show.</param>
		/// <returns><see langword="true"/> when the settings were applied; <see langword="false"/> when the dialog was cancelled.</returns>
		Task<Boolean> ShowSettingsAsync(SettingsPage page)
		{
			return this.ShowSettingsAsync();
		}

		/// <summary>
		/// Shows the settings of the active project (<c>fss-project.json</c>) modally. The changes are saved to the project file, not to the global settings.
		/// </summary>
		/// <returns><see langword="true"/> when the project settings were saved; <see langword="false"/> when the dialog was cancelled.</returns>
		Task<Boolean> ShowProjectSettingsAsync()
		{
			return Task.FromResult(false);
		}

		/// <summary>
		/// Shows the settings of the active project like <see cref="ShowProjectSettingsAsync()"/>, opened on one option (the link "Edit project
		/// setting" of the Settings window).
		/// </summary>
		/// <param name="optionId">The option (see <c>ProjectOptions</c>) the window scrolls to.</param>
		/// <returns><see langword="true"/> when the project settings were saved; <see langword="false"/> when the dialog was cancelled.</returns>
		Task<Boolean> ShowProjectSettingsAsync(String optionId)
		{
			return this.ShowProjectSettingsAsync();
		}

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
		/// Compiles scripts in a batch like <see cref="RunBatchCompileAsync(IReadOnlyList{String})"/>, reports every finished script and stops when
		/// the token is cancelled (the compilations in flight are cancelled, their processes killed, and the result says it was cancelled).
		/// </summary>
		/// <param name="scriptPaths">The scripts to compile.</param>
		/// <param name="progress">Receives a notification for every finished script, or <see langword="null"/>.</param>
		/// <param name="cancellationToken">Cancels the run, like the Cancel button of the window.</param>
		/// <returns>The summary of the run.</returns>
		Task<BatchCompileResult> RunBatchCompileAsync(IReadOnlyList<String> scriptPaths, IProgress<BatchCompileProgress>? progress, CancellationToken cancellationToken)
		{
			return this.RunBatchCompileAsync(scriptPaths);
		}

		/// <summary>
		/// Lets the user pick one of the header files of the configured headers folder.
		/// </summary>
		/// <param name="chooseAndKeepOpen">
		/// Called with the header path when the user chooses a header while holding Ctrl; the window then stays open so that several headers
		/// can be chosen one after another (as in the old editor). Ctrl is ignored when <see langword="null"/>.
		/// </param>
		/// <returns>The full path of the chosen header, or <see langword="null"/> when cancelled, when only headers chosen with Ctrl were handled, or when the headers folder is not available (the user is told).</returns>
		Task<String?> PickHeaderAsync(Func<String, Task>? chooseAndKeepOpen = null);

		/// <summary>
		/// Shows the list of the symbols of the project ("Go to symbol": a filter box and the best matches while the user types), or the
		/// list of the places to choose from when a name is defined several times.
		/// </summary>
		/// <param name="request">What to show.</param>
		/// <returns>The chosen symbol, or <see langword="null"/> when the list was cancelled. The default implementation chooses nothing.</returns>
		Task<FalloutScriptStudio.Core.Indexing.IndexedSymbol?> PickSymbolAsync(SymbolPickerRequest request)
		{
			return Task.FromResult<FalloutScriptStudio.Core.Indexing.IndexedSymbol?>(null);
		}
	}
}
