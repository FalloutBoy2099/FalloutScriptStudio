namespace FalloutScriptStudio.Services.ScriptTools.Flowchart
{
	using System;

	/// <summary>
	/// The result of renaming a dialogue node.
	/// </summary>
	/// <param name="Outcome">How the rename ended.</param>
	/// <param name="NewName">The name the user typed, or <see langword="null"/> when nothing was renamed.</param>
	public sealed record NodeRenameResult(NodeRenameOutcome Outcome, String? NewName);
}