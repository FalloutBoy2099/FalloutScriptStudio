namespace FalloutScriptStudio.Services.ScriptTools.Replace
{
	using System;

	/// <summary>A file that was left unchanged when the replacements were applied.</summary>
	/// <param name="Path">The full path of the file (or the name of a document that was never saved).</param>
	/// <param name="Reason">Why it was not changed.</param>
	/// <param name="Detail">The message of the error behind it, or an empty text.</param>
	/// <param name="Matches">The number of replacements that were not made in it.</param>
	public sealed record ReplaceFailure(String Path, ReplaceFailureReason Reason, String Detail, Int32 Matches);
}