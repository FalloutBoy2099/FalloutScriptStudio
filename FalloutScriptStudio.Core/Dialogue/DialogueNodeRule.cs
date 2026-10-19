namespace FalloutScriptStudio.Core.Dialogue
{
	using System;

	/// <summary>The rule that tells which procedures of a script are dialogue nodes (the same everywhere: parser, panels, editor and flowchart).</summary>
	public static class DialogueNodeRule
	{
		/// <summary>The name of the procedure the game calls to start a dialogue.</summary>
		public const String TalkProcedure = "talk_p_proc";

		private const String NodeKeyword = "node";

		/// <summary>Determines whether a procedure is a dialogue node: its name contains <c>node</c> or it is <c>talk_p_proc</c> (case is ignored).</summary>
		/// <param name="procedureName">The name of the procedure.</param>
		/// <returns><see langword="true"/> for a dialogue node.</returns>
		public static Boolean IsNode(String procedureName)
		{
			ArgumentNullException.ThrowIfNull(procedureName);
			return procedureName.Contains(NodeKeyword, StringComparison.OrdinalIgnoreCase) || String.Equals(procedureName, TalkProcedure, StringComparison.OrdinalIgnoreCase);
		}
	}
}