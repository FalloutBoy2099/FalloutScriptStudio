namespace FalloutScriptStudio.Core.Dialogue
{
	using System;

	/// <summary>Where the message number and the message file are in the arguments of a function or macro that shows a message.</summary>
	/// <param name="MessageArg">The zero-based position of the message number argument.</param>
	/// <param name="Offset">The number added to that argument (a macro that expands to <c>message_str(NAME, x + 5)</c>).</param>
	/// <param name="FileArg">The zero-based position of the message file argument, or -1 when there is none.</param>
	/// <param name="FileExpression">The message file expression the function always uses (<c>NAME</c> in <c>message_str(NAME, x)</c>) when <paramref name="FileArg"/> is -1; <see langword="null"/> means the file of the script.</param>
	/// <param name="Kind">What the message is used for.</param>
	/// <param name="MinArguments">The fewest arguments a call has.</param>
	/// <param name="MaxArguments">The most arguments a call has.</param>
	/// <param name="IsHeuristic">Whether the name was taken for a message call only because it contains <c>Reply</c>, <c>Message</c> or <c>Option</c> (a call whose message argument is a text is then no message call).</param>
	internal sealed record MessageCallSpec(Int32 MessageArg, Int32 Offset, Int32 FileArg, String? FileExpression, ScriptMessageUsageKind Kind, Int32 MinArguments, Int32 MaxArguments, Boolean IsHeuristic = false);
}