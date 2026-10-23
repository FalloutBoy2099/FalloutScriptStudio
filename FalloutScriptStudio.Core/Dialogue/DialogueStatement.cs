namespace FalloutScriptStudio.Core.Dialogue
{
	using System;

	/// <summary>A dialogue statement of a node, with its place in the script.</summary>
	/// <param name="Kind">The kind.</param>
	/// <param name="Line">The 1-based line of the script.</param>
	/// <param name="MessageNumber">The message number, 0 when it is not known.</param>
	/// <param name="MessageFile">The number of the message file (-1 for the file of the script).</param>
	/// <param name="Target">The raw name of the target of an option or a call, or <see langword="null"/>.</param>
	/// <param name="Resolved">The node the target stands for, or <see langword="null"/> when it is not a node of the script.</param>
	/// <param name="Iq">The intelligence requirement of an option as written, or <see langword="null"/>.</param>
	internal sealed record DialogueStatement(DialogueStatementKind Kind, Int32 Line, Int32 MessageNumber, Int32 MessageFile, String? Target, String? Resolved, String? Iq);
}