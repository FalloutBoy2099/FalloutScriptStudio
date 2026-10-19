namespace FalloutScriptStudio.Services.ScriptTools
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;
	using Documents;
	using Procedures;

	/// <summary>
	/// Script-aware tool windows opened from the main window and from the editor: procedure name prompt,
	/// scripts.lst registration, advanced search in files, dialogue flowchart, dialogue preview and the
	/// dialogue function rules. Implemented by the tool-window modules; the main window only calls this interface.
	/// </summary>
	public interface IScriptToolService
	{
		/// <summary>Asks for a procedure name (validated against <paramref name="existingNames"/>); null when cancelled.</summary>
		/// <returns>The text to put between <c>procedure</c> and <c>begin</c>: the name, and for a new procedure the argument list with <c>variable</c> keywords.</returns>
		Task<String?> PromptProcedureNameAsync(String initialName, ProcedureNameDialogMode mode, IReadOnlyCollection<String> existingNames);

		/// <summary>
		/// Asks for a procedure name and, when the request allows it, where to insert the procedure and whether to copy the current one.
		/// </summary>
		/// <param name="request">What the dialog is used for.</param>
		/// <returns>The answer, or <see langword="null"/> when cancelled.</returns>
		Task<ProcedureNameResult?> PromptProcedureAsync(ProcedureNameRequest request);

		/// <summary>
		/// Opens the "register script in scripts.lst" window (the list editor). The script is checked first (saved, valid name...)
		/// and added to the list when it is not registered yet.
		/// </summary>
		/// <param name="script">The script to register, or <see langword="null"/> to only edit the list.</param>
		Task ShowRegisterScriptAsync(DocumentViewModel? script);

		/// <summary>Opens the advanced "search in files" window; results are sent to the workspace output list.</summary>
		Task ShowAdvancedSearchAsync(String? initialText);

		/// <summary>
		/// Opens the advanced search window ready for "Replace in Files" (the folder scope is chosen when a folder is known). The default
		/// implementation opens the plain search window.
		/// </summary>
		/// <param name="initialText">The text to look for, or <see langword="null"/>.</param>
		/// <returns>A task that completes when the window is shown.</returns>
		Task ShowReplaceInFilesAsync(String? initialText)
		{
			return this.ShowAdvancedSearchAsync(initialText);
		}

		/// <summary>
		/// Puts back what the last "Replace in Files" replaced, in every file that was not changed since. The default implementation does nothing.
		/// </summary>
		/// <returns>A task that completes when the files are restored.</returns>
		Task UndoReplaceInFilesAsync()
		{
			return Task.CompletedTask;
		}

		/// <summary>Gets a value indicating whether the last "Replace in Files" can be undone. The default implementation says there is nothing to undo.</summary>
		Boolean CanUndoReplaceInFiles => false;

		/// <summary>Raised when <see cref="CanUndoReplaceInFiles"/> changed (a replacement was made, or it was undone). The default implementation never raises it.</summary>
		event EventHandler? UndoReplaceInFilesStateChanged
		{
			add { }
			remove { }
		}

		/// <summary>Opens the dialogue nodes flowchart of a script.</summary>
		Task ShowFlowchartAsync(DocumentViewModel script);

		/// <summary>Opens the dialogue preview / testing window of a script.</summary>
		Task ShowDialoguePreviewAsync(DocumentViewModel script);

		/// <summary>
		/// Opens the dialogue preview / testing window of a script at a given node; a window that is already open for the script is moved
		/// to that node and brought to the front.
		/// </summary>
		/// <param name="script">The script.</param>
		/// <param name="options">How to open the window.</param>
		/// <returns>A task that completes when the window is shown.</returns>
		Task ShowDialoguePreviewAsync(DocumentViewModel script, DialoguePreviewOpenOptions options);

		/// <summary>Opens the dialogue function rules configuration.</summary>
		Task ShowFunctionRulesAsync();

		/// <summary>
		/// Closes the tool windows that belong to a script (its flowchart, node code editors and dialogue preview); called when the
		/// tab of the script closes.
		/// </summary>
		/// <param name="script">The script.</param>
		/// <returns>A task that completes when the windows are closed.</returns>
		Task CloseToolWindowsAsync(DocumentViewModel script);
	}
}