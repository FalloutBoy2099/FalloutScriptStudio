namespace FalloutScriptStudio.Core.Messages
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics.CodeAnalysis;
	using System.Text;

	/// <summary>
	/// Reads the bytes of a message file the way the editor reads text files: a byte order mark wins (UTF-8, UTF-16), otherwise text that is valid
	/// UTF-8 and has non-ASCII characters is UTF-8, and everything else is read with the code page given. The line breaks the file uses are kept
	/// so that it can be written back unchanged.
	/// </summary>
	public static class MessageFileDecoder
	{
		/// <summary>Decodes a message file.</summary>
		/// <param name="bytes">The content of the file.</param>
		/// <param name="fallback">The encoding of the file when it has neither a byte order mark nor valid UTF-8 text.</param>
		/// <param name="detect">Whether valid UTF-8 text is recognised. <see langword="false"/> reads the file with <paramref name="fallback"/> whatever its bytes are (a byte order mark still wins).</param>
		/// <returns>The decoded file.</returns>
		public static DecodedMessageFile Decode(Byte[] bytes, Encoding fallback, Boolean detect)
		{
			ArgumentNullException.ThrowIfNull(bytes);
			ArgumentNullException.ThrowIfNull(fallback);

			String text;
			Encoding encoding;
			Boolean bom = false;
			Boolean usedFallback = false;
			if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
			{
				text = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
				encoding = new UTF8Encoding(false);
				bom = true;
			}
			else if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
			{
				text = Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
				encoding = new UnicodeEncoding(false, false);
				bom = true;
			}
			else if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
			{
				text = Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
				encoding = new UnicodeEncoding(true, false);
				bom = true;
			}
			else if (detect && HasNonAscii(bytes) && TryUtf8(bytes, out String? utf8))
			{
				text = utf8;
				encoding = new UTF8Encoding(false);
			}
			else
			{
				text = fallback.GetString(bytes);
				encoding = fallback;
				usedFallback = HasNonAscii(bytes);
			}

			String newLine = DetectNewLine(text);
			List<String> lines = SplitLines(text, out Boolean endsWithNewLine);
			return new DecodedMessageFile(lines, encoding, bom, newLine, endsWithNewLine, usedFallback, HasSuspicious(text));
		}

		private static Boolean TryUtf8(Byte[] bytes, [NotNullWhen(true)] out String? text)
		{
			try
			{
				text = new UTF8Encoding(false, true).GetString(bytes);
				return true;
			}
			catch (DecoderFallbackException)
			{
				text = null;
				return false;
			}
		}

		private static Boolean HasNonAscii(Byte[] bytes)
		{
			foreach (Byte b in bytes)
			{
				if (b >= 0x80)
				{
					return true;
				}
			}

			return false;
		}

		private static Boolean HasSuspicious(String text)
		{
			foreach (Char c in text)
			{
				if (c == '�' || (c >= '\u0080' && c <= '\u009F'))
				{
					return true;
				}
			}

			return false;
		}

		private static String DetectNewLine(String text)
		{
			Int32 crlf = 0;
			Int32 lf = 0;
			for (Int32 i = 0; i < text.Length; i++)
			{
				if (text[i] != '\n')
				{
					continue;
				}

				if (i > 0 && text[i - 1] == '\r')
				{
					crlf++;
				}
				else
				{
					lf++;
				}
			}

			return lf > crlf ? "\n" : "\r\n";
		}

		private static List<String> SplitLines(String text, out Boolean endsWithNewLine)
		{
			List<String> lines = [];
			Int32 start = 0;
			for (Int32 i = 0; i < text.Length; i++)
			{
				if (text[i] == '\n')
				{
					Int32 end = i > start && text[i - 1] == '\r' ? i - 1 : i;
					lines.Add(text.Substring(start, end - start));
					start = i + 1;
				}
			}

			endsWithNewLine = start == text.Length && text.Length > 0;
			if (start < text.Length)
			{
				lines.Add(text.Substring(start));
			}

			return lines;
		}
	}
}