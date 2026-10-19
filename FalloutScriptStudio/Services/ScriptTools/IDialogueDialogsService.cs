namespace FalloutScriptStudio.Services.ScriptTools
{
	using System.Collections.Generic;
	using System.Threading.Tasks;
	using Core.Functions;
	using Core.Refactoring;
	using Dialogs;

	/// <summary>
	/// Small modal dialogs shared by the function tree, the flowchart and the rename refactoring.
	/// Implemented by the "function dialogs" module (legacy <c>FunctionsRules</c>, <c>UserFunction</c>, <c>PreviewRename</c> forms).
	/// </summary>
	public interface IDialogueDialogsService
	{
		/// <summary>Opens the dialogue function rules configuration (legacy "Functions Config").</summary>
		Task ShowFunctionRulesAsync();

		/// <summary>
		/// Shows the add / edit user function dialog (name, description, code).
		/// </summary>
		/// <param name="existing">The function being edited, or <see langword="null"/> to add a new one.</param>
		/// <returns>The edited definition, or <see langword="null"/> when cancelled.</returns>
		Task<FunctionDefinition?> EditFunctionAsync(FunctionDefinition? existing);

		/// <summary>
		/// Shows the add / edit user function dialog, including the "add to highlight" choice of the legacy dialog.
		/// </summary>
		/// <param name="existing">The function being edited, or <see langword="null"/> to add a new one.</param>
		/// <returns>The entered function and the highlight choice, or <see langword="null"/> when cancelled.</returns>
		Task<FunctionEditResult?> ShowFunctionEditorAsync(FunctionDefinition? existing);

		/// <summary>
		/// Shows the global-macro rename preview: every match with its file / line / text, each one can be unticked.
		/// </summary>
		/// <returns>The matches the user accepts, or <see langword="null"/> when cancelled.</returns>
		Task<IReadOnlyList<PreviewMatch>?> ConfirmMacroRenameAsync(MacroRenamePreview preview);
	}
}