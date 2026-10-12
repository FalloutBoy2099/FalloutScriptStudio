namespace FalloutScriptStudio.Workspace
{
	using System;
	using System.Text;
	using ICSharpCode.AvalonEdit.Document;

	/// <summary>Reads and prepares script text: encoding detection, line ending normalization, trailing space removal.</summary>
	public static class DocumentTextCodec
	{
		/// <summary>
		/// Decodes the bytes of a file. A byte order mark wins; otherwise valid UTF-8 with non-ASCII characters is taken as UTF-8
		/// (that is how this editor saves scripts with the "Save in UTF-8" option) and everything else uses the fallback code page.
		/// </summary>
		public static DecodedText Decode(Byte[] bytes, Encoding fallback)
		{
			ArgumentNullException.ThrowIfNull(bytes);
			ArgumentNullException.ThrowIfNull(fallback);

			if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
			{
				return new DecodedText(Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3), new UTF8Encoding(true));
			}

			if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
			{
				return new DecodedText(Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2), Encoding.Unicode);
			}

			if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
			{
				return new DecodedText(Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2), Encoding.BigEndianUnicode);
			}

			if (HasNonAscii(bytes))
			{
				try
				{
					UTF8Encoding strict = new UTF8Encoding(false, true);
					return new DecodedText(strict.GetString(bytes), new UTF8Encoding(false));
				}
				catch (DecoderFallbackException)
				{
					// Not UTF-8: fall through to the configured code page.
				}
			}

			return new DecodedText(fallback.GetString(bytes), fallback) { UsedFallback = HasNonAscii(bytes) };
		}

		/// <summary>Encodes a text the way it is written to a file: the byte order mark of the encoding (if any) followed by the text.</summary>
		public static Byte[] Encode(Encoding encoding, String text)
		{
			ArgumentNullException.ThrowIfNull(encoding);
			ArgumentNullException.ThrowIfNull(text);

			Byte[] preamble = encoding.GetPreamble();
			Byte[] body = encoding.GetBytes(text);
			if (preamble.Length == 0)
			{
				return body;
			}

			Byte[] result = new Byte[preamble.Length + body.Length];
			preamble.CopyTo(result, 0);
			body.CopyTo(result, preamble.Length);
			return result;
		}

		/// <summary>Converts every line ending of the text to <c>CR LF</c>.</summary>
		public static String NormalizeNewLines(String text)
		{
			ArgumentNullException.ThrowIfNull(text);

			Boolean needsWork = false;
			for (Int32 i = 0; i < text.Length; i++)
			{
				Char c = text[i];
				if ((c == '\r' && (i + 1 >= text.Length || text[i + 1] != '\n')) || (c == '\n' && (i == 0 || text[i - 1] != '\r')))
				{
					needsWork = true;
					break;
				}
			}

			if (!needsWork)
			{
				return text;
			}

			StringBuilder result = new StringBuilder(text.Length + 16);
			for (Int32 i = 0; i < text.Length; i++)
			{
				Char c = text[i];
				if (c == '\r')
				{
					result.Append("\r\n");
					if (i + 1 < text.Length && text[i + 1] == '\n')
					{
						i++;
					}
				}
				else if (c == '\n')
				{
					result.Append("\r\n");
				}
				else
				{
					result.Append(c);
				}
			}

			return result.ToString();
		}

		/// <summary>Returns the text without the spaces and tabs at the end of its lines (the same rule as the document overload).</summary>
		public static String TrimTrailingWhitespace(String text)
		{
			ArgumentNullException.ThrowIfNull(text);

			TextDocument copy = new TextDocument(text);
			copy.UndoStack.SizeLimit = 0;
			return TrimTrailingWhitespace(copy) ? copy.Text : text;
		}

		/// <summary>Removes spaces and tabs at the end of every line as one undoable change.</summary>
		/// <returns><see langword="true"/> when the document changed.</returns>
		public static Boolean TrimTrailingWhitespace(TextDocument document)
		{
			ArgumentNullException.ThrowIfNull(document);

			Boolean changed = false;
			document.UndoStack.StartUndoGroup();
			try
			{
				for (Int32 number = document.LineCount; number >= 1; number--)
				{
					DocumentLine line = document.GetLineByNumber(number);
					Int32 end = line.EndOffset;
					Int32 start = end;
					while (start > line.Offset && (document.GetCharAt(start - 1) == ' ' || document.GetCharAt(start - 1) == '\t'))
					{
						start--;
					}

					if (start < end)
					{
						document.Remove(start, end - start);
						changed = true;
					}
				}
			}
			finally
			{
				document.UndoStack.EndUndoGroup();
			}

			return changed;
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
