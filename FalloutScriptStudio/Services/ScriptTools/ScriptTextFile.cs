namespace FalloutScriptStudio.Services.ScriptTools
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Text;

	/// <summary>
	/// Reads and writes the small text files the script tools edit (<c>scripts.lst</c>, <c>scripts.h</c>, scripts being searched).
	/// The encoding is detected like the legacy editor did: byte order mark first, then strict UTF-8, otherwise the fallback code page.
	/// </summary>
	internal static class ScriptTextFile
	{
		/// <summary>The result of decoding a file.</summary>
		/// <param name="Text">The decoded text without a byte order mark.</param>
		/// <param name="Encoding">The encoding to use when the file is written back (never UTF-16).</param>
		/// <param name="HasByteOrderMark">Whether the file started with a UTF-8 byte order mark.</param>
		internal sealed record DecodedText(String Text, Encoding Encoding, Boolean HasByteOrderMark);

		/// <summary>Decodes the content of a text file.</summary>
		/// <param name="bytes">The raw content.</param>
		/// <param name="fallback">The encoding used when the content is not Unicode.</param>
		/// <returns>The text and the encoding to write it back with.</returns>
		internal static DecodedText Decode(Byte[] bytes, Encoding fallback)
		{
			Encoding utf8 = new UTF8Encoding(false);
			if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
			{
				return new DecodedText(utf8.GetString(bytes, 3, bytes.Length - 3), utf8, true);
			}

			if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
			{
				return new DecodedText(Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2), utf8, false);
			}

			if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
			{
				return new DecodedText(Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2), utf8, false);
			}

			try
			{
				return new DecodedText(new UTF8Encoding(false, true).GetString(bytes), HasNonAscii(bytes) ? utf8 : fallback, false);
			}
			catch (DecoderFallbackException)
			{
				return new DecodedText(fallback.GetString(bytes), fallback, false);
			}
		}

		/// <summary>Encodes text for writing.</summary>
		/// <param name="text">The text.</param>
		/// <param name="encoding">The encoding.</param>
		/// <param name="withByteOrderMark">Whether a UTF-8 byte order mark is written first.</param>
		/// <returns>The bytes.</returns>
		internal static Byte[] Encode(String text, Encoding encoding, Boolean withByteOrderMark)
		{
			Byte[] body = encoding.GetBytes(text);
			if (!withByteOrderMark)
			{
				return body;
			}

			Byte[] result = new Byte[body.Length + 3];
			result[0] = 0xEF;
			result[1] = 0xBB;
			result[2] = 0xBF;
			Buffer.BlockCopy(body, 0, result, 3, body.Length);
			return result;
		}

		/// <summary>Splits text into lines like <see cref="File.ReadAllLines(String)"/> (a trailing line break does not add an empty line).</summary>
		/// <param name="text">The text.</param>
		/// <returns>The lines without line breaks.</returns>
		internal static List<String> SplitLines(String text)
		{
			List<String> lines = new List<String>();
			using StringReader reader = new StringReader(text);
			String? line;
			while ((line = reader.ReadLine()) != null)
			{
				lines.Add(line);
			}

			return lines;
		}

		/// <summary>Joins lines with CRLF line breaks, ending the last line too (like <see cref="File.WriteAllLines(String, IEnumerable{String})"/> on Windows).</summary>
		/// <param name="lines">The lines.</param>
		/// <returns>The text.</returns>
		internal static String JoinLines(IEnumerable<String> lines)
		{
			StringBuilder builder = new StringBuilder();
			foreach (String line in lines)
			{
				builder.Append(line).Append("\r\n");
			}

			return builder.ToString();
		}

		/// <summary>Writes a file through a temporary file in the same folder so that a failure never leaves a half-written file.</summary>
		/// <param name="path">The target file.</param>
		/// <param name="content">The content.</param>
		internal static void WriteAllBytesAtomic(String path, Byte[] content)
		{
			String temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
			Boolean moved = false;
			try
			{
				File.WriteAllBytes(temporary, content);
				File.Move(temporary, path, true);
				moved = true;
			}
			finally
			{
				if (!moved && File.Exists(temporary))
				{
					try
					{
						File.Delete(temporary);
					}
					catch (IOException)
					{
						// The leftover temporary file is harmless.
					}
				}
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
	}
}
