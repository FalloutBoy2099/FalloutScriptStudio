namespace FalloutScriptStudio.Workspace
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Services;

	/// <summary>Turns the references of a symbol into search results of the Errors tab (with the text of every reference line).</summary>
	public sealed class ReferenceResultsPresenter
	{
		private readonly IDialogService _dialogs;
		private readonly IOutputSink _output;
		private readonly ISettingsService _settings;

		/// <summary>Initializes the presenter.</summary>
		public ReferenceResultsPresenter(IDialogService dialogs, IOutputSink output, ISettingsService settings)
		{
			ArgumentNullException.ThrowIfNull(dialogs);
			ArgumentNullException.ThrowIfNull(output);
			ArgumentNullException.ThrowIfNull(settings);
			this._dialogs = dialogs;
			this._output = output;
			this._settings = settings;
		}

		/// <summary>Shows the references of a symbol, or a message when there are none.</summary>
		/// <param name="symbolName">The symbol.</param>
		/// <param name="references">The references.</param>
		/// <param name="openTexts">The texts of the open documents by full path (they win over the files on disk).</param>
		public async Task ShowAsync(String symbolName, IReadOnlyList<SourceReference> references, IReadOnlyDictionary<String, String> openTexts)
		{
			ArgumentNullException.ThrowIfNull(references);
			ArgumentNullException.ThrowIfNull(openTexts);

			if (references.Count == 0)
			{
				await this._dialogs.ShowInfoAsync(Loc.Get("Str.Shell.Dlg.ReferencesTitle"), Loc.Get("Str.Shell.Dlg.NoReferences"));
				return;
			}

			Encoding fileEncoding = this._settings.Settings.Editor.FileEncoding.ToEncoding();
			Dictionary<String, String[]> lineCache = new Dictionary<String, String[]>(StringComparer.OrdinalIgnoreCase);
			List<BuildError> results = new List<BuildError>();
			Int32 lastLine = 0;
			Int32 nextColumn = 0;
			foreach (SourceReference reference in references)
			{
				String file = reference.File ?? BuildError.NoFile;
				String[] lines = GetLines(lineCache, openTexts, file, fileEncoding);
				String lineText = reference.Line >= 1 && reference.Line <= lines.Length ? lines[reference.Line - 1] : String.Empty;
				if (lastLine != reference.Line)
				{
					nextColumn = 0;
				}

				Int32 column = lineText.IndexOf(symbolName, Math.Min(nextColumn, lineText.Length), StringComparison.OrdinalIgnoreCase);
				lastLine = reference.Line;
				nextColumn = column < 0 ? 0 : column + symbolName.Length;
				results.Add(new BuildError(BuildErrorType.Search, lineText.Trim(), file, reference.Line, column < 0 ? -1 : column + 1, symbolName.Length));
			}

			this._output.ShowSearchResults(results, "Str.Shell.Output.References", symbolName);
		}

		private static String[] GetLines(Dictionary<String, String[]> cache, IReadOnlyDictionary<String, String> open, String file, Encoding fallback)
		{
			if (cache.TryGetValue(file, out String[]? lines))
			{
				return lines;
			}

			String text = String.Empty;
			String full = DocumentPaths.GetFullPath(file);
			if (open.TryGetValue(full, out String? openText))
			{
				text = openText;
			}
			else if (File.Exists(full))
			{
				try
				{
					text = DocumentTextCodec.Decode(File.ReadAllBytes(full), fallback).Text;
				}
				catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
				{
					text = String.Empty;
				}
			}

			lines = text.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
			cache[file] = lines;
			return lines;
		}
	}
}
