namespace FalloutScriptStudio.Core.Messages
{
	using System;
	using Compilation;

	/// <summary>
	/// A problem found by the message file structure check.
	/// </summary>
	/// <param name="Type">The severity: <see cref="BuildErrorType.Error"/>, <see cref="BuildErrorType.Warning"/>,
	/// <see cref="BuildErrorType.Message"/> for style hints, <see cref="BuildErrorType.None"/> for a probably missing comment mark.</param>
	/// <param name="Message">The description of the problem.</param>
	/// <param name="Offset">The offset in the text where the highlighted span starts.</param>
	/// <param name="Length">The length of the highlighted span.</param>
	/// <param name="Line">The line of the problem (1-based).</param>
	/// <param name="Column">The column of the problem (1-based).</param>
	public sealed record MessageStructureIssue(BuildErrorType Type, String Message, Int32 Offset, Int32 Length, Int32 Line, Int32 Column);
}