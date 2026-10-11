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
		private readonly ILogger _logger;

		/// <summary>Initializes the service.</summary>
		public DocumentFileService(ISettingsService settings, IDialogService dialogs, ILogger<DocumentFileService>? logger = null)
		{
			ArgumentNullException.ThrowIfNull(settings);
			ArgumentNullException.ThrowIfNull(dialogs);
			this._settings = settings;
			this._dialogs = dialogs;
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
			EditorSettings editor = this._settings.Settings.Editor;
			if (editor.AutoRemoveTrailingSpaces && document.Kind != DocumentKind.Message)
			{
				DocumentTextCodec.TrimTrailingWhitespace(document.TextDocument);
			}

			Encoding encoding = this.GetSaveEncoding(document);
			String text = document.TextDocument.Text;
			if (document.Kind == DocumentKind.Message && encoding.CodePage == 866)
			{
				text = text.Replace('Х', 'X');
			}

			try
			{
				await File.WriteAllTextAsync(path, text, encoding);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				await this._dialogs.ShowErrorAsync(Loc.Get("Str.Shell.Dlg.CannotSaveTitle"), path + "\n\n" + ex.Message);
				return false;
			}

			document.LastWriteTimeUtc = File.GetLastWriteTimeUtc(path);
			document.TextDocument.UndoStack.MarkAsOriginalFile();
			document.IsModified = false;
			return true;
		}

		/// <summary>Tells whether a file is a header located directly in the configured headers folder (its macros are global).</summary>
		public Boolean IsGlobalHeader(String path)
		{
			String? headers = this._settings.Settings.Paths.HeadersDirectory;
			return headers is not null
				&& String.Equals(Path.GetExtension(path), ".h", StringComparison.OrdinalIgnoreCase)
				&& String.Equals(DocumentPaths.GetFullPath(headers), DocumentPaths.GetFullPath(Path.GetDirectoryName(path) ?? String.Empty), StringComparison.OrdinalIgnoreCase);
		}

		private Encoding GetSaveEncoding(DocumentViewModel document)
		{
			EditorSettings editor = this._settings.Settings.Editor;
			if (document.Kind == DocumentKind.Message)
			{
				return editor.FileEncoding.ToEncoding();
			}

			if (document.IsParsable && editor.SaveScriptsAsUtf8)
			{
				return TextEncodings.Utf8;
			}

			return document.Encoding ?? editor.FileEncoding.ToEncoding();
		}
	}
}
