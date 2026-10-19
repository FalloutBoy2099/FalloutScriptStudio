namespace FalloutScriptStudio.Services.ScriptTools.Replace
{
	using System;
	using System.Collections.Generic;

	/// <summary>The outcome of undoing a "Replace in Files".</summary>
	/// <param name="Restored">The number of files and documents that got their previous content back.</param>
	/// <param name="Failures">The files that were left as they are, with the reason (<see cref="ReplaceFailureReason.Changed"/> when they were modified since).</param>
	public sealed record ReplaceUndoResult(Int32 Restored, IReadOnlyList<ReplaceFailure> Failures);
}