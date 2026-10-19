namespace FalloutScriptStudio.Core.Refactoring
{
	using System;
	using System.Collections.Generic;
	using Parsing;

	/// <summary>
	/// Renames procedures, variables and macros (legacy <c>Refactor</c>). The service never touches a document or a file and
	/// never asks the user anything: it takes the texts it should look at and returns edits and preview entries.
	/// </summary>
	public interface IRefactoringService
	{
		/// <summary>
		/// Checks a new name: it must be a valid identifier, differ from the old name and not collide with an existing
		/// procedure, variable or macro (a change of letter case only is allowed for procedures and variables).
		/// </summary>
		/// <param name="request">The rename; its documents are not used.</param>
		/// <returns>The check.</returns>
		RenameValidation Validate(RenameRequest request);

		/// <summary>
		/// Renames a procedure, a variable (script or procedure local) or a macro in the supplied documents.
		/// </summary>
		/// <param name="request">The rename.</param>
		/// <returns>
		/// The edits per file. Variables and procedures are renamed through their references (the declaration, the definition and
		/// every recorded reference line); procedures without references (data of the internal parser) are renamed by pattern
		/// in the current file. Macros are renamed wherever the whole word occurs and their <c>#define</c> line is re-aligned.
		/// </returns>
		RenameResult Rename(RenameRequest request);

		/// <summary>
		/// Finds all occurrences of a macro name so the user can pick the ones to rename (global macros are renamed in every
		/// script and header of the project).
		/// </summary>
		/// <param name="macro">The macro.</param>
		/// <param name="newName">The new name; only used for the result.</param>
		/// <param name="documents">The files to search; may be enumerated lazily (for example reading project files one by one).</param>
		/// <returns>The occurrences grouped by file.</returns>
		MacroRenamePreview PreviewMacroRename(ScriptMacro macro, String newName, IEnumerable<RefactorDocument> documents);

		/// <summary>
		/// Renames the selected occurrences of a preview and re-aligns the <c>#define</c> line when its occurrence is selected.
		/// </summary>
		/// <param name="preview">The preview.</param>
		/// <param name="selected">The occurrences to rename (a subset of <see cref="MacroRenamePreview.Matches"/>).</param>
		/// <returns>The edits per file.</returns>
		RenameResult ApplyMacroRename(MacroRenamePreview preview, IEnumerable<PreviewMatch> selected);
	}
}