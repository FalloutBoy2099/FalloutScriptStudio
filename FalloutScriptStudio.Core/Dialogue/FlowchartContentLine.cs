namespace FalloutScriptStudio.Core.Dialogue
{
	using System;

	/// <summary>
	/// One line of the code of a node as the flowchart shows it (legacy <c>ContentBody</c>).
	/// </summary>
	/// <param name="Code">The line of code, with one indentation level removed.</param>
	/// <param name="MessageText">The text of the message of the line, or <c>null</c> for plain code and calls.</param>
	/// <param name="Opcode">The kind of statement on the line.</param>
	/// <param name="MessageNumber">The message number (0 when unknown, -1 for calls).</param>
	/// <param name="MessageFileNumber">The message file number (-1 for the script's own file).</param>
	/// <param name="Index">The zero-based index of the line in the node.</param>
	public sealed record FlowchartContentLine(String Code, String? MessageText, DialogueOpcode Opcode, Int32 MessageNumber, Int32 MessageFileNumber, Int32 Index);
}