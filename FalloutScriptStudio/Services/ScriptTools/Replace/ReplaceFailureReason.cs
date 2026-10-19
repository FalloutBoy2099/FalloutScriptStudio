namespace FalloutScriptStudio.Services.ScriptTools.Replace
{
	/// <summary>Why a file was not changed when the replacements were applied.</summary>
	public enum ReplaceFailureReason
	{
		/// <summary>The file is read-only.</summary>
		ReadOnly,

		/// <summary>The file cannot be read (it is locked by another program, it was deleted, access is denied).</summary>
		Unreadable,

		/// <summary>The file is not what it was when the preview was made (it was changed, or the document was edited), so the places of the preview are not valid any more.</summary>
		Changed,

		/// <summary>The open document was closed since the preview.</summary>
		Closed,

		/// <summary>The replacement has characters that the encoding of the file cannot hold.</summary>
		NotEncodable,

		/// <summary>The file could not be written; it still has its previous content.</summary>
		WriteFailed,
	}
}