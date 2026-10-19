namespace FalloutScriptStudio.Core.Dialogue
{
	using System;
	using System.Collections.Generic;
	using Parsing;

	/// <summary>
	/// Reads the dialogue statements (replies, messages, options, calls) of the nodes of a script (legacy <c>DialogueParser</c>).
	/// </summary>
	public interface IDialogueNodeParser
	{
		/// <summary>
		/// Gets the names of the dialogue node procedures: the ones whose name contains <c>node</c> (any case) and <c>talk_p_proc</c>
		/// (legacy <c>GetAllNodesName</c>).
		/// </summary>
		/// <param name="procedures">The procedures of the script.</param>
		/// <returns>The names as written in the script, in the order of <paramref name="procedures"/>.</returns>
		IReadOnlyList<String> GetNodeProcedureNames(IEnumerable<ScriptProcedure> procedures);

		/// <summary>Determines whether a procedure name is the name of a dialogue node.</summary>
		/// <param name="procedureName">The procedure name.</param>
		/// <returns><c>true</c> for names that contain <c>node</c> (any case) and for <c>talk_p_proc</c>.</returns>
		Boolean IsNodeProcedureName(String procedureName);

		/// <summary>
		/// Parses the code of a node (legacy <c>ParseNodeCode</c>).
		/// </summary>
		/// <param name="nodeCode">The body of the node procedure.</param>
		/// <param name="info">The parse result of the script, used to resolve macros and procedure names.</param>
		/// <param name="options">How to parse.</param>
		/// <returns>The lines that hold a dialogue statement (and plain lines when requested), in code order of the passes (messages, replies, options, macro calls, calls) per line.</returns>
		IReadOnlyList<DialogueLine> ParseNode(String nodeCode, ProgramInfo info, DialogueNodeParseOptions options);

		/// <summary>
		/// Parses the code of a node for the flowchart (legacy <c>PrepareNodeCode</c>): tabs become three spaces, a statement that
		/// follows <c>;</c> on the same line as <c>else</c> is moved to a line of its own, and every line of code is returned.
		/// </summary>
		/// <param name="nodeCode">The body of the node procedure.</param>
		/// <param name="info">The parse result of the script.</param>
		/// <param name="excludeComments">Whether commented code is ignored.</param>
		/// <returns>The lines of the node.</returns>
		IReadOnlyList<DialogueLine> ParseNodeForDiagram(String nodeCode, ProgramInfo info, Boolean excludeComments);
	}
}