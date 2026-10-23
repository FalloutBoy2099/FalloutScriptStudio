namespace FalloutScriptStudio.Core.Messages
{
	using System;
	using System.Collections.Generic;
	using System.Text;

	/// <summary>The content of a message file as read from the disk, with what is needed to write it back the way it was.</summary>
	/// <param name="Lines">The physical lines, without line breaks.</param>
	/// <param name="Encoding">The encoding the bytes were decoded with (without the byte order mark in <see cref="Encoding.GetPreamble"/>: see <paramref name="HasByteOrderMark"/>).</param>
	/// <param name="HasByteOrderMark">Whether the file starts with a byte order mark.</param>
	/// <param name="NewLine">The line break the file uses most (<c>CR LF</c> when it has none).</param>
	/// <param name="EndsWithNewLine">Whether the last line is followed by a line break.</param>
	/// <param name="UsedFallback">Whether the bytes had to be read with the code page (non-ASCII bytes that are neither a byte order mark nor valid UTF-8).</param>
	/// <param name="HasSuspiciousCharacters">Whether the text holds characters that the code page cannot give (the replacement character, or control characters of the C1 range): the file is probably in another encoding.</param>
	public sealed record DecodedMessageFile(
		IReadOnlyList<String> Lines,
		Encoding Encoding,
		Boolean HasByteOrderMark,
		String NewLine,
		Boolean EndsWithNewLine,
		Boolean UsedFallback,
		Boolean HasSuspiciousCharacters);
}