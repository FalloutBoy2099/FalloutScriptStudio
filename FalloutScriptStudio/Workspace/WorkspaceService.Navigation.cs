namespace FalloutScriptStudio.Workspace
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Localization;

	/// <summary>Going to positions, opening includes and showing references and search results.</summary>
	public sealed partial class WorkspaceService
	{
		public async Task GoToAsync(String? file, Int32 line, Int32 column = -1, Int32 selectLength = -1)
		{
			DocumentViewModel? document;
			if (String.IsNullOrEmpty(file) || file == BuildError.NoFile)
			{
				document = this.ActiveDocument;
			}
			else
			{
				document = this.FindDocument(file)
					?? await this.OpenAsync(file, new OpenDocumentOptions { AddToRecent = false });
			}

			if (document is null || line <= 0)
			{
				if (document is not null)
				{
					this.ActiveDocument = document;
				}

				return;
			}

			this.ActiveDocument = document;
			DocumentNavigation.Navigate(document, new OpenDocumentOptions { Line = line, Column = column, SelectLength = selectLength });
		}

		public async Task OpenIncludeAsync(DocumentViewModel from, String includePath)
		{
			ArgumentNullException.ThrowIfNull(from);
			ArgumentException.ThrowIfNullOrEmpty(includePath);

			if (includePath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
			{
				return;
			}

			if (from.FilePath is null && !Path.IsPathRooted(includePath) && this._settings.Settings.GetIncludeHeadersDirectory() is null)
			{
				await this._dialogs.ShowErrorAsync(Loc.Get("Str.Shell.Dlg.OpenIncludeTitle"), Loc.Get("Str.Shell.Dlg.IncludeRelativeUnsaved"));
				return;
			}

			IncludeResolver resolver = this._settings.Settings.ToIncludeResolver(this._settings.Paths);
			String resolved = resolver.Resolve(includePath, from.FilePath is null ? null : Path.GetDirectoryName(from.FilePath));
			if (!File.Exists(resolved))
			{
				await this._dialogs.ShowErrorAsync(Loc.Get("Str.Shell.Dlg.OpenIncludeTitle"), Loc.Format("Str.Shell.Dlg.HeaderNotFound", resolved));
				return;
			}

			await this.OpenAsync(resolved, new OpenDocumentOptions { AddToRecent = false });
		}

		public Task ShowReferencesAsync(String symbolName, IReadOnlyList<SourceReference> references)
		{
			ArgumentNullException.ThrowIfNull(references);
			return this._references.ShowAsync(symbolName, references, this.GetOpenDocumentTexts());
		}

		public void ShowSearchResults(IReadOnlyList<BuildError> results)
		{
			this._output.ShowSearchResults(results, "Str.Shell.Output.SearchResults");
		}
	}
}
