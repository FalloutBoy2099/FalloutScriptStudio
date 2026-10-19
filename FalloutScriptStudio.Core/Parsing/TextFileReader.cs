namespace FalloutScriptStudio.Core.Parsing
{
	using System;
	using System.IO;
	using System.Text;
	using IO;

	/// <summary>
	/// Reads script and header files, detecting the encoding the way the legacy editor did:
	/// byte order mark first, then strict UTF-8, otherwise the Windows ANSI code page.
	/// </summary>
	internal static class TextFileReader
	{
		/// <summary>Reads a whole text file.</summary>
		/// <param name="path">Path of the file.</param>
		/// <returns>The decoded text.</returns>
		internal static String ReadAllText(String path)
		{
			return Decode(SharedFile.ReadAllBytes(path));
		}

		/// <summary>Decodes the content of a text file.</summary>
		/// <param name="bytes">Raw file content.</param>
		/// <returns>The decoded text, without a byte order mark.</returns>
		internal static String Decode(Byte[] bytes)
		{
			if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
			{
				return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
			}

			if (bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0 && bytes[3] == 0)
			{
				return Encoding.UTF32.GetString(bytes, 4, bytes.Length - 4);
			}

			if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
			{
				return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
			}

			if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
			{
				return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
			}

			try
			{
				return new UTF8Encoding(false, true).GetString(bytes);
			}
			catch (DecoderFallbackException)
			{
				return TextEncodings.Ansi.GetString(bytes);
			}
		}
	}
}