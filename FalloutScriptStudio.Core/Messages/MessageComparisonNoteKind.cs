namespace FalloutScriptStudio.Core.Messages
{
	/// <summary>What a <see cref="MessageComparisonNote"/> says about a whole file.</summary>
	public enum MessageComparisonNoteKind
	{
		/// <summary>The two files are not in the same encoding (for example UTF-8 and Windows-1252).</summary>
		EncodingDiffers,

		/// <summary>The target file holds characters that its code page cannot give: part of it was read with the wrong encoding.</summary>
		SuspiciousEncoding,

		/// <summary>A number appears more than once in the reference file (the first message is compared).</summary>
		DuplicateInReference,

		/// <summary>A number appears more than once in the target file (the first message is compared).</summary>
		DuplicateInTarget,

		/// <summary>The target file has lines that are neither a message, a comment nor empty.</summary>
		UnrecognizedLines,
	}
}