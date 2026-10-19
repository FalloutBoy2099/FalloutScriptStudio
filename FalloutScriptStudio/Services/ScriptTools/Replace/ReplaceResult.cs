namespace FalloutScriptStudio.Services.ScriptTools.Replace
{
	using System;
	using System.Collections.Generic;

	/// <summary>The outcome of the second phase of "Replace in Files": the replacements that were made and the files that were left unchanged.</summary>
	/// <param name="Replacements">The number of replacements that were made.</param>
	/// <param name="FilesChanged">The number of files and documents that were changed.</param>
	/// <param name="Failures">The files that were left unchanged, with the reason.</param>
	/// <param name="Undo">What is needed to undo the replacements, or <see langword="null"/> when nothing was changed.</param>
	/// <param name="Cancelled">Whether the run stopped because it was cancelled; the files that were not reached are untouched.</param>
	public sealed record ReplaceResult(Int32 Replacements, Int32 FilesChanged, IReadOnlyList<ReplaceFailure> Failures, ReplaceUndoRecord? Undo, Boolean Cancelled);
}