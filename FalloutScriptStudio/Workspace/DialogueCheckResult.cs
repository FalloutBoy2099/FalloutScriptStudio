namespace FalloutScriptStudio.Workspace
{
	using System;

	/// <summary>The outcome of the check of the dialogue of a script.</summary>
	/// <param name="HasDialogue">Whether the script has a dialogue at all (a node with a reply, a message, an option or a call).</param>
	/// <param name="Count">The number of problems that were reported.</param>
	public readonly record struct DialogueCheckResult(Boolean HasDialogue, Int32 Count);
}