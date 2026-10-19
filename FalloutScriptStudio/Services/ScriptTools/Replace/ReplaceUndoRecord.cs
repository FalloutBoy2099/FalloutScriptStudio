namespace FalloutScriptStudio.Services.ScriptTools.Replace
{
	using System;
	using System.Collections.Generic;
	using System.Text;

	/// <summary>
	/// What the last "Replace in Files" changed, kept in memory so that it can be undone as far as the files were not touched since. It holds
	/// the edits (a few strings per replacement), never the content of the files.
	/// </summary>
	/// <param name="Files">The files that were changed.</param>
	/// <param name="FallbackEncoding">The encoding the files that are not Unicode are read with.</param>
	/// <param name="Replacements">The number of replacements that were made.</param>
	public sealed record ReplaceUndoRecord(IReadOnlyList<ReplaceUndoFile> Files, Encoding FallbackEncoding, Int32 Replacements);
}