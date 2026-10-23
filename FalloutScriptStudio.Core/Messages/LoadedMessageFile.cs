namespace FalloutScriptStudio.Core.Messages
{
	using System;
	using System.Collections.Generic;

	/// <summary>A message file read for a comparison.</summary>
	/// <param name="Path">The full path of the file.</param>
	/// <param name="Language">The language folder of the path (<c>french</c>), or <see langword="null"/>.</param>
	/// <param name="Decoded">The bytes as decoded: lines, encoding, byte order mark and line breaks.</param>
	/// <param name="Document">The parsed messages.</param>
	public sealed record LoadedMessageFile(String Path, String? Language, DecodedMessageFile Decoded, MessageDocument Document)
	{
		/// <summary>Gets the name of the encoding, for the messages and the report (<c>Windows-1252</c>, <c>UTF-8 (BOM)</c>).</summary>
		public String EncodingName => this.Decoded.Encoding.CodePage == 65001 ? (this.Decoded.HasByteOrderMark ? "UTF-8 (BOM)" : "UTF-8") : this.Decoded.Encoding.CodePage == 1200 || this.Decoded.Encoding.CodePage == 1201 ? "UTF-16" : this.Decoded.Encoding.EncodingName;

		/// <summary>Gets the notes this file contributes to a comparison (suspicious characters).</summary>
		/// <returns>The notes.</returns>
		public IEnumerable<MessageComparisonNote> GetNotes()
		{
			if (this.Decoded.HasSuspiciousCharacters)
			{
				yield return new MessageComparisonNote(MessageComparisonNoteKind.SuspiciousEncoding, 0, this.EncodingName);
			}
		}
	}
}