namespace FalloutScriptStudio.Services.ScriptTools
{
	using System;
	using System.Collections.Generic;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Services.ScriptTools.Procedures;

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

		/// <summary>Opens the dialogue nodes flowchart of a script.</summary>
		Task ShowFlowchartAsync(DocumentViewModel script);

		/// <summary>Opens the dialogue preview / testing window of a script.</summary>
		Task ShowDialoguePreviewAsync(DocumentViewModel script);

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
