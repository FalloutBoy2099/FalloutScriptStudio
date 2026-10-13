namespace FalloutScriptStudio.Core.Indexing
{
	using System;
	using System.Text;

	/// <summary>
	/// Decodes the bytes of a script the way the editor opens it: a byte order mark wins, valid UTF-8 with non-ASCII characters is UTF-8,
	/// everything else uses the configured code page.
	/// </summary>
	internal static class IndexTextDecoder
	{
		private const Int32 BinaryProbeLength = 8192;

		/// <summary>Determines whether the beginning of a file contains a zero byte (a binary file).</summary>
		internal static Boolean LooksBinary(Byte[] bytes)
		{
			Int32 length = Math.Min(bytes.Length, BinaryProbeLength);
			if (bytes.Length >= 2 && ((bytes[0] == 0xFF && bytes[1] == 0xFE) || (bytes[0] == 0xFE && bytes[1] == 0xFF)))
			{
				return false;
			}

			return bytes.AsSpan(0, length).IndexOf((Byte)0) >= 0;
		}

		/// <summary>Decodes a file.</summary>
		internal static String Decode(Byte[] bytes, Encoding fallback)
		{
			if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
			{
				return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
			}

			if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
			{
				return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
			}

			if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
			{
				return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
			}

			if (bytes.AsSpan().ContainsAnyInRange((Byte)0x80, (Byte)0xFF))
			{
				try
				{
					return new UTF8Encoding(false, true).GetString(bytes);
				}
				catch (DecoderFallbackException)
				{
					// Not UTF-8: the configured code page.
				}

				return fallback.GetString(bytes);
			}

			return Encoding.ASCII.GetString(bytes);
		}
	}
}
