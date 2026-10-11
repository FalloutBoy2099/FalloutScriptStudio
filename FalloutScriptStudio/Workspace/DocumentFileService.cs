namespace FalloutScriptStudio.Workspace
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Text;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.IO;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Services;
	using Microsoft.Extensions.Logging;
	using Microsoft.Extensions.Logging.Abstractions;

	/// <summary>Reads and writes the text files of the documents: encoding detection, new line normalization, templates and saving.</summary>
	public sealed class DocumentFileService
	{
		private const String DefaultTemplateFile = "newScript.txt";
		private const String FallbackTemplate = "procedure start;\r\n\r\nprocedure start begin\r\n\r\nend\r\n";

		private readonly ISettingsService _settings;
		private readonly IDialogService _dialogs;
		private readonly IDocumentFileWriter _writer;
		private readonly ILogger _logger;

		/// <summary>Initializes the service.</summary>
		public DocumentFileService(ISettingsService settings, IDialogService dialogs, ILogger<DocumentFileService>? logger = null, IDocumentFileWriter? writer = null)
		{
			ArgumentNullException.ThrowIfNull(settings);
			ArgumentNullException.ThrowIfNull(dialogs);
			this._settings = settings;
			this._dialogs = dialogs;
			this._writer = writer ?? new AtomicDocumentFileWriter();
			this._logger = logger ?? NullLogger<DocumentFileService>.Instance;
		}

		/// <summary>Builds the filter of the "Save as" dialog with the file type of <paramref name="kind"/> first.</summary>
		public static String BuildSaveFilter(DocumentKind kind)
		{
			String[] parts = Loc.Get("Str.Shell.Filter.Save").Split('|');
			Int32 first = kind switch
			{
				DocumentKind.Script => 0,
				DocumentKind.Header => 1,
				DocumentKind.Message => 2,
				_ => 3,
			};
			List<String> ordered = new List<String> { parts[first * 2], parts[(first * 2) + 1] };
			for (Int32 i = 0; i < 4; i++)
			{
				if (i != first)
				{
					ordered.Add(parts[i * 2]);
					ordered.Add(parts[(i * 2) + 1]);
				}
			}

			return String.Join('|', ordered);
		}

		/// <summary>Reads a file, detecting its encoding and (for scripts and headers) normalizing the new lines.</summary>
		public async Task<DecodedText> ReadAsync(String path)
		{
			Byte[] bytes = await File.ReadAllBytesAsync(path);
			Encoding fallback = this._settings.Settings.Editor.FileEncoding.ToEncoding();
			DecodedText decoded = DocumentTextCodec.Decode(bytes, fallback);
			String extension = Path.GetExtension(path).ToLowerInvariant();
			if (extension is ".ssl" or ".h")
			{
				decoded = decoded with { Text = DocumentTextCodec.NormalizeNewLines(decoded.Text) };
			}

			return decoded;
		}

		/// <summary>Reads the template of new scripts, or the built-in fallback when the template file is missing.</summary>
		public String ReadDefaultTemplate()
		{
			String file = Path.Combine(this._settings.Paths.TemplatesDirectory, DefaultTemplateFile);
			if (File.Exists(file))
			{
				try
				{
					return DocumentTextCodec.Decode(File.ReadAllBytes(file), this._settings.Settings.Editor.FileEncoding.ToEncoding()).Text;
				}
				catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
				{
					this._logger.LogWarning(ex, "Cannot read the default template {File}", file);
				}
			}

			return FallbackTemplate;
		}

		/// <summary>
		/// Writes the text of a document to a file and marks the document as saved. Shows an error and returns <see langword="false"/>
		/// when the file cannot be written.
		/// </summary>
		public async Task<Boolean> WriteAsync(DocumentViewModel document, String path)
		{
			ArgumentNullException.ThrowIfNull(document);
			ArgumentException.ThrowIfNullOrEmpty(path);

			// The kind comes from the target path: "Save as" may give the document another type (and so another encoding rule).
			DocumentKind kind = DocumentViewModel.KindFromPath(path);
			String text = document.TextDocument.Text;
			String fileText = this._settings.Settings.Editor.AutoRemoveTrailingSpaces && kind != DocumentKind.Message
				? DocumentTextCodec.TrimTrailingWhitespace(text)
				: text;

			Encoding encoding = this.GetSaveEncoding(document, kind);
			String encodedText = kind == DocumentKind.Message && encoding.CodePage == 866 ? fileText.Replace('Х', 'X') : fileText;
			Boolean switchedToUtf8 = false;
			String? lost = FindUnrepresentableCharacter(encoding, encodedText);
			if (lost is not null)
			{
				YesNoCancel answer = await this._dialogs.AskYesNoCancelAsync(
					Loc.Get("Str.Shell.Dlg.UnrepresentableTitle"),
					Loc.Format("Str.Shell.Dlg.UnrepresentableText", Path.GetFileName(path), encoding.EncodingName, lost),
					Loc.Get("Str.Shell.Dlg.SaveAsUtf8"),
					Loc.Get("Str.Shell.Dlg.SaveWithReplacement"));
				if (answer == YesNoCancel.Cancel)
				{
					return false;
				}

				if (answer == YesNoCancel.Yes)
				{
					encoding = TextEncodings.Utf8;
					encodedText = fileText;
					switchedToUtf8 = true;
				}
			}

			if (!await this.TryWriteAsync(path, DocumentTextCodec.Encode(encoding, encodedText)))
			{
				return false;
			}

			if (switchedToUtf8)
			{
				document.Encoding = encoding;
			}

			document.LastWriteTimeUtc = File.GetLastWriteTimeUtc(path);
			if (!String.Equals(document.TextDocument.Text, text, StringComparison.Ordinal))
			{
				// The user typed while the file was being written: the file holds the older text, so the document stays modified.
				document.TextDocument.UndoStack.DiscardOriginalFileMarker();
				document.IsModified = true;
				return true;
			}

			if (!String.Equals(fileText, text, StringComparison.Ordinal))
			{
				DocumentTextCodec.TrimTrailingWhitespace(document.TextDocument);
			}

			document.TextDocument.UndoStack.MarkAsOriginalFile();
			document.IsModified = false;
			return true;
		}

		/// <summary>
		/// Writes a text file that is not (yet) an open document, for example a decompiled script. Shows an error and returns
		/// <see langword="false"/> when the file cannot be written.
		/// </summary>
		/// <param name="path">The file.</param>
		/// <param name="text">The text.</param>
		/// <param name="encoding">The encoding (its byte order mark is written).</param>
		public Task<Boolean> WriteTextAsync(String path, String text, Encoding encoding)
		{
			ArgumentException.ThrowIfNullOrEmpty(path);
			ArgumentNullException.ThrowIfNull(text);
			ArgumentNullException.ThrowIfNull(encoding);
			return this.TryWriteAsync(path, DocumentTextCodec.Encode(encoding, text));
		}

		/// <summary>Tells whether a file is a header located directly in the configured headers folder (its macros are global).</summary>
		public Boolean IsGlobalHeader(String path)
		{
			String? headers = this._settings.Settings.Paths.HeadersDirectory;
			return headers is not null
				&& String.Equals(Path.GetExtension(path), ".h", StringComparison.OrdinalIgnoreCase)
				&& String.Equals(DocumentPaths.GetFullPath(headers), DocumentPaths.GetFullPath(Path.GetDirectoryName(path) ?? String.Empty), StringComparison.OrdinalIgnoreCase);
		}

		private async Task<Boolean> TryWriteAsync(String path, Byte[] content)
		{
			try
			{
				await this._writer.WriteAsync(path, content);
				return true;
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				this._logger.LogWarning(ex, "Cannot write {File}", path);
				await this._dialogs.ShowErrorAsync(Loc.Get("Str.Shell.Dlg.CannotSaveTitle"), path + "\n\n" + ex.Message);
				return false;
			}
		}


		private static Boolean IsUnicode(Encoding encoding)
		{
			return encoding.CodePage is 65001 or 1200 or 1201 or 12000 or 12001;
		}

		/// <summary>Finds the first character a code page cannot store (it would be written as "?"), described for the user.</summary>
		private static String? FindUnrepresentableCharacter(Encoding encoding, String text)
		{
			if (IsUnicode(encoding))
			{
				return null;
			}

			Encoding strict = (Encoding)encoding.Clone();
			strict.EncoderFallback = EncoderFallback.ExceptionFallback;
			try
			{
				strict.GetByteCount(text);
				return null;
			}
			catch (EncoderFallbackException ex)
			{
				Int32 codePoint = ex.IsUnknownSurrogate() ? Char.ConvertToUtf32(ex.CharUnknownHigh, ex.CharUnknownLow) : ex.CharUnknown;
				String code = "U+" + codePoint.ToString("X4", System.Globalization.CultureInfo.InvariantCulture);
				return codePoint is >= 0xD800 and <= 0xDFFF ? code : "'" + Char.ConvertFromUtf32(codePoint) + "' (" + code + ")";
			}
		}

		private Encoding GetSaveEncoding(DocumentViewModel document, DocumentKind kind)
		{
			EditorSettings editor = this._settings.Settings.Editor;
			if (kind == DocumentKind.Message)
			{
				// A message file that was read as Unicode (with or without a byte order mark) is written back the same way; the
				// "ANSI / OEM 866" choice of the user applies to the code page files.
				return document.Encoding is { } current && IsUnicode(current) ? current : editor.FileEncoding.ToEncoding();
			}

			if ((kind is DocumentKind.Script or DocumentKind.Header) && editor.SaveScriptsAsUtf8)
			{
				return TextEncodings.Utf8;
			}

			return document.Encoding ?? editor.FileEncoding.ToEncoding();
		}
	}
}
