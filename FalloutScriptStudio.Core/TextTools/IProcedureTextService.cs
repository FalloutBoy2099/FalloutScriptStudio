namespace FalloutScriptStudio.Core.TextTools
{
	using System;
	using System.Collections.Generic;
	using FalloutScriptStudio.Core.Parsing;

	/// <summary>
	/// Text level procedure operations of the editor: get and replace a procedure body, delete, insert and move procedures
	/// including their forward declarations and the blank lines around them. Nothing is applied to a document: every
	/// method returns edits in the coordinates of the text it was given.
	/// </summary>
	/// <remarks>All line numbers are 1-based. The text of a script is scanned again by each call, so results never depend on a stale parse.</remarks>
	public interface IProcedureTextService
	{
		/// <summary>
		/// Gets the code between the <c>begin</c> line and the <c>end</c> line of a procedure (legacy <c>GetProcedureCode</c>).
		/// </summary>
		/// <param name="text">The text of the file that holds the body of the procedure.</param>
		/// <param name="procedure">The procedure; its <see cref="ScriptProcedure.StartLine"/> and <see cref="ScriptProcedure.EndLine"/> are used.</param>
		/// <returns>The body lines without the final line break; empty for an empty body; <c>null</c> for a procedure without a body (imported or only declared).</returns>
		String? GetProcedureCode(String text, ScriptProcedure procedure);

		/// <summary>Gets the body of a procedure found by name.</summary>
		/// <param name="text">The script text.</param>
		/// <param name="procedureName">The procedure name (case-insensitive).</param>
		/// <returns>The body, or <c>null</c> when the script has no such procedure body.</returns>
		String? GetProcedureCode(String text, String procedureName);

		/// <summary>Gets the bodies of several procedures with a single scan of the script (much cheaper than one call per procedure).</summary>
		/// <param name="text">The script text.</param>
		/// <param name="procedureNames">The names of the procedures (case-insensitive).</param>
		/// <returns>The bodies by procedure name (case-insensitive lookup); procedures without a body in the script are left out.</returns>
		IReadOnlyDictionary<String, String> GetProcedureCodes(String text, IEnumerable<String> procedureNames);

		/// <summary>
		/// Creates the edit that replaces the body of a procedure (legacy <c>ReplaceProcedureCode</c>).
		/// </summary>
		/// <param name="text">The script text.</param>
		/// <param name="procedureName">The procedure name (case-insensitive).</param>
		/// <param name="newCode">The new body.</param>
		/// <returns>The edit, or <c>null</c> when the script has no such procedure body.</returns>
		TextEdit? ReplaceProcedureCode(String text, String procedureName, String newCode);

		/// <summary>
		/// Deletes a procedure together with its forward declaration; a blank line is removed too when the procedure was
		/// surrounded by blank lines.
		/// </summary>
		/// <param name="text">The script text.</param>
		/// <param name="procedureName">The procedure name (case-insensitive).</param>
		/// <returns>The result, or <c>null</c> when the script has no such procedure body.</returns>
		ProcedureEditResult? DeleteProcedure(String text, String procedureName);

		/// <summary>Deletes several procedures; names that are not found are skipped.</summary>
		/// <param name="text">The script text.</param>
		/// <param name="procedureNames">The names to delete.</param>
		/// <returns>The combined result (never <c>null</c>; no edits when nothing was deleted).</returns>
		ProcedureEditResult DeleteProcedures(String text, IEnumerable<String> procedureNames);

		/// <summary>
		/// Builds the text of a new procedure (without surrounding blank lines).
		/// </summary>
		/// <param name="request">What to create.</param>
		/// <param name="newLine">The line terminator.</param>
		/// <param name="copiedBody">The body to copy, when <see cref="ProcedureInsertRequest.CopyAnchorBody"/> is used.</param>
		/// <returns>The procedure text.</returns>
		String BuildProcedureBlock(ProcedureInsertRequest request, String newLine, String? copiedBody = null);

		/// <summary>
		/// Inserts a new procedure and its forward declaration (legacy <c>PrepareInsertProcedure</c> / <c>InsertProcedure</c>).
		/// </summary>
		/// <param name="text">The script text.</param>
		/// <param name="request">What to insert and where.</param>
		/// <returns>
		/// The result, whose <see cref="ProcedureEditResult.FocusLine"/> is the line for the caret; <c>null</c> when a
		/// procedure with that name already has a body.
		/// </returns>
		/// <exception cref="ArgumentException">The declaration does not start with a valid name.</exception>
		ProcedureEditResult? InsertProcedure(String text, ProcedureInsertRequest request);

		/// <summary>
		/// Moves a procedure (with its declaration) in front of another one, or to the end of the script.
		/// </summary>
		/// <param name="text">The script text.</param>
		/// <param name="procedureName">The procedure to move.</param>
		/// <param name="beforeProcedure">The procedure to put it in front of, or <c>null</c> to move it to the end.</param>
		/// <returns>The result, or <c>null</c> when a procedure is missing or both names are the same.</returns>
		ProcedureEditResult? MoveProcedure(String text, String procedureName, String? beforeProcedure);
	}
}
