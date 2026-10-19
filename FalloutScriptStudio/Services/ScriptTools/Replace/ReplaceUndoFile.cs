namespace FalloutScriptStudio.Services.ScriptTools.Replace
{
	using System;
	using System.Collections.Generic;

	/// <summary>What is needed to undo the replacements made in one file: the hash of the result (the file must still be that) and the edits that put the old text back.</summary>
	/// <param name="Path">The full path of the file.</param>
	/// <param name="Owner">The open document that was changed, or <see langword="null"/> for a file that was rewritten.</param>
	/// <param name="ResultHash">The SHA-256 of the file after the replacement (of the characters of the text for a document).</param>
	/// <param name="Edits">The edits that undo the replacements.</param>
	public sealed record ReplaceUndoFile(String Path, Object? Owner, Byte[] ResultHash, IReadOnlyList<ReplaceReverseEdit> Edits);
}