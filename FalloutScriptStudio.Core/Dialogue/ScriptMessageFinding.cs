namespace FalloutScriptStudio.Core.Dialogue
{
	using System;

	/// <summary>One inconsistency between a script and its message file.</summary>
	/// <param name="Kind">What is reported.</param>
	/// <param name="Severity">How serious it is.</param>
	/// <param name="Number">The message number; 0 for the findings that are not about one number.</param>
	/// <param name="FileNumber">The message file the number belongs to (<see cref="ScriptMessageUsage.OwnFile"/> for the message file of the script).</param>
	/// <param name="InScript"><see langword="true"/> when <paramref name="Line"/> is a line of the script, <see langword="false"/> for a line of the message file.</param>
	/// <param name="Line">The 1-based line, or -1 when the finding has no place.</param>
	/// <param name="Column">The 1-based column, or -1.</param>
	/// <param name="Length">The length of the text the finding points to, or -1.</param>
	/// <param name="Count">The number of uses or lines for the summary findings, otherwise 0.</param>
	public sealed record ScriptMessageFinding(
		ScriptMessageFindingKind Kind,
		ScriptMessageSeverity Severity,
		Int32 Number,
		Int32 FileNumber,
		Boolean InScript,
		Int32 Line,
		Int32 Column,
		Int32 Length,
		Int32 Count = 0);
}