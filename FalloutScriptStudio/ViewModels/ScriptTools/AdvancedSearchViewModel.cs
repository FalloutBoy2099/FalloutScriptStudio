namespace FalloutScriptStudio.ViewModels.ScriptTools
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Text.RegularExpressions;
	using System.Threading;
	using System.Threading.Tasks;
	using CommunityToolkit.Mvvm.ComponentModel;
	using CommunityToolkit.Mvvm.Input;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.TextTools;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Services.ScriptTools.Search;
	using ICSharpCode.AvalonEdit.Document;

	/// <summary>
	/// View model of the advanced "Search &amp; Replace" window (legacy <c>SearchForm</c>): searches the active script, all open scripts
	/// or a folder, lists the matching lines and sends them to the output panel.
	/// </summary>
	public sealed partial class AdvancedSearchViewModel : ScriptToolViewModelBase
	{
		private readonly IWorkspace _workspace;
		private readonly ISettingsService _settings;
		private readonly IDialogService _dialogs;
		private readonly ScriptSearcher _searcher;
		private Boolean _loading = true;

		/// <summary>
		/// Initializes a new instance of the <see cref="AdvancedSearchViewModel"/> class.
		/// </summary>
		/// <param name="workspace">The open documents and navigation.</param>
		/// <param name="settings">The settings and the search history.</param>
		/// <param name="dialogs">Shows messages and the folder picker.</param>
		/// <param name="searcher">Runs the searches.</param>
		public AdvancedSearchViewModel(IWorkspace workspace, ISettingsService settings, IDialogService dialogs, ScriptSearcher searcher)
		{
			ArgumentNullException.ThrowIfNull(workspace);
			ArgumentNullException.ThrowIfNull(settings);
			ArgumentNullException.ThrowIfNull(dialogs);
			ArgumentNullException.ThrowIfNull(searcher);

			this._workspace = workspace;
			this._settings = settings;
			this._dialogs = dialogs;
			this._searcher = searcher;

			EditorSettings editor = settings.Settings.Editor;
			this.MatchCase = !editor.SearchIgnoreCase;
			this.WholeWord = editor.SearchWholeWord;
			foreach (String query in settings.SearchHistory.Queries)
			{
				this.QueryHistory.Add(query);
			}

			foreach (String folder in settings.SearchHistory.Directories.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
			{
				this.Folders.Add(folder);
			}

			this.FolderPath = settings.Settings.Paths.LastSearchDirectory ?? String.Empty;
			this._loading = false;
		}

		/// <summary>Gets the previously used search texts, most recent first.</summary>
		public ObservableCollection<String> QueryHistory { get; } = new ObservableCollection<String>();

		/// <summary>Gets the saved search folders.</summary>
		public ObservableCollection<String> Folders { get; } = new ObservableCollection<String>();

		/// <summary>Gets the file masks offered for a folder search.</summary>
		public IReadOnlyList<String> MaskPresets { get; } = new[] { SearchFileMasks.AllScriptFiles, "*.ssl", "*.h", "*.msg", "*.*" };

		/// <summary>Gets or sets the text to look for.</summary>
		[ObservableProperty]
		public partial String SearchText { get; set; } = String.Empty;

		/// <summary>Gets or sets the replacement text.</summary>
		[ObservableProperty]
		public partial String ReplaceText { get; set; } = String.Empty;

		/// <summary>Gets or sets a value indicating whether the letter case matters.</summary>
		[ObservableProperty]
		public partial Boolean MatchCase { get; set; }

		/// <summary>Gets or sets a value indicating whether only whole words match.</summary>
		[ObservableProperty]
		public partial Boolean WholeWord { get; set; }

		/// <summary>Gets or sets a value indicating whether the text is a regular expression.</summary>
		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(CanChangeWholeWord))]
		public partial Boolean UseRegex { get; set; }

		/// <summary>Gets or sets where to search.</summary>
		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(IsCurrentScope), nameof(IsOpenScope), nameof(IsFolderScope), nameof(CanReplace))]
		[NotifyCanExecuteChangedFor(nameof(FindNextCommand), nameof(ReplaceCommand), nameof(ReplaceAllCommand))]
		public partial SearchScope Scope { get; set; } = SearchScope.OpenScripts;

		/// <summary>Gets or sets the folder to search.</summary>
		[ObservableProperty]
		public partial String FolderPath { get; set; } = String.Empty;

		/// <summary>Gets or sets the file masks of a folder search.</summary>
		[ObservableProperty]
		public partial String FileMask { get; set; } = SearchFileMasks.AllScriptFiles;

		/// <summary>Gets or sets a value indicating whether sub folders are searched too.</summary>
		[ObservableProperty]
		public partial Boolean IncludeSubfolders { get; set; } = true;

		/// <summary>Gets the matching lines of the last search.</summary>
		[ObservableProperty]
		public partial IReadOnlyList<SearchResultItem> Results { get; private set; } = Array.Empty<SearchResultItem>();

		/// <summary>Gets or sets the selected result.</summary>
		[ObservableProperty]
		public partial SearchResultItem? SelectedResult { get; set; }

		/// <summary>Gets a value indicating whether a search is running.</summary>
		[ObservableProperty]
		public partial Boolean IsSearching { get; private set; }

		/// <summary>Gets the number of files to search in the running search.</summary>
		[ObservableProperty]
		public partial Double ProgressMaximum { get; private set; } = 1;

		/// <summary>Gets the number of files searched so far.</summary>
		[ObservableProperty]
		public partial Double ProgressValue { get; private set; }

		/// <summary>Gets the state of the search for the status line.</summary>
		[ObservableProperty]
		public partial String StatusText { get; private set; } = String.Empty;

		/// <summary>Gets the reason the search cannot run; empty when there is none.</summary>
		[ObservableProperty]
		[NotifyPropertyChangedFor(nameof(HasError))]
		public partial String ErrorMessage { get; private set; } = String.Empty;

		/// <summary>Gets or sets a value indicating whether the active script is searched.</summary>
		public Boolean IsCurrentScope
		{
			get => this.Scope == SearchScope.CurrentScript;
			set
			{
				if (value)
				{
					this.Scope = SearchScope.CurrentScript;
				}
			}
		}

		/// <summary>Gets or sets a value indicating whether all open scripts are searched.</summary>
		public Boolean IsOpenScope
		{
			get => this.Scope == SearchScope.OpenScripts;
			set
			{
				if (value)
				{
					this.Scope = SearchScope.OpenScripts;
				}
			}
		}

		/// <summary>Gets or sets a value indicating whether a folder is searched.</summary>
		public Boolean IsFolderScope
		{
			get => this.Scope == SearchScope.Folder;
			set
			{
				if (value)
				{
					this.Scope = SearchScope.Folder;
				}
			}
		}

		/// <summary>Gets a value indicating whether text can be replaced in the current scope (not in folders, like in the legacy editor).</summary>
		public Boolean CanReplace => this.Scope != SearchScope.Folder;

		/// <summary>Gets a value indicating whether the whole word option applies (it does not to regular expressions).</summary>
		public Boolean CanChangeWholeWord => !this.UseRegex;

		/// <summary>Gets a value indicating whether <see cref="ErrorMessage"/> is shown.</summary>
		public Boolean HasError => this.ErrorMessage.Length > 0;

		/// <summary>
		/// Puts text into the search field.
		/// </summary>
		/// <param name="text">The text.</param>
		public void SetSearchText(String text)
		{
			this.SearchText = text;
			this.ErrorMessage = String.Empty;
		}

		/// <summary>
		/// Refreshes the history list with searches made elsewhere (the quick search panel of the main window).
		/// </summary>
		public void RefreshHistory()
		{
			List<String> queries = this._settings.SearchHistory.Queries;
			if (queries.SequenceEqual(this.QueryHistory))
			{
				return;
			}

			String text = this.SearchText;
			this.QueryHistory.Clear();
			foreach (String query in queries)
			{
				this.QueryHistory.Add(query);
			}

			this.SearchText = text;
		}

		partial void OnMatchCaseChanged(Boolean value)
		{
			if (!this._loading)
			{
				this._settings.Update(settings => settings.Editor.SearchIgnoreCase = !value);
			}
		}

		partial void OnWholeWordChanged(Boolean value)
		{
			if (!this._loading)
			{
				this._settings.Update(settings => settings.Editor.SearchWholeWord = value);
			}
		}

		[RelayCommand]
		private async Task SearchAsync()
		{
			if (this.Scope == SearchScope.Folder)
			{
				await this.FindAllCommand.ExecuteAsync(null);
			}
			else
			{
				await this.FindNextAsync();
			}
		}

		private Boolean TryCreateMatcher(out SearchMatcher? matcher)
		{
			this.ErrorMessage = String.Empty;
			this.StatusText = String.Empty;
			String text = this.SearchText.Trim();
			this.SearchText = text;
			if (!SearchMatcher.TryCreate(new SearchOptions(text, this.MatchCase, this.WholeWord && !this.UseRegex, this.UseRegex), out matcher, out String? error))
			{
				this.ErrorMessage = text.Length == 0 ? String.Empty : error ?? String.Empty;
				return false;
			}

			this.RememberQuery(text);
			return true;
		}

		private void RememberQuery(String text)
		{
			this._settings.SearchHistory.AddQuery(text);
			Int32 existing = this.QueryHistory.IndexOf(text);
			if (existing >= 0)
			{
				this.QueryHistory.Move(existing, 0);
			}
			else
			{
				this.QueryHistory.Insert(0, text);
			}

			while (this.QueryHistory.Count > SearchHistory.MaxQueries)
			{
				this.QueryHistory.RemoveAt(this.QueryHistory.Count - 1);
			}
		}

		[RelayCommand(IncludeCancelCommand = true)]
		private async Task FindAllAsync(CancellationToken cancellationToken)
		{
			if (!this.TryCreateMatcher(out SearchMatcher? matcher) || matcher is null)
			{
				return;
			}

			SearchRequest? request = this.BuildRequest(matcher);
			if (request is null)
			{
				return;
			}

			this.IsSearching = true;
			this.Results = Array.Empty<SearchResultItem>();
			this.ProgressValue = 0;
			this.ProgressMaximum = 1;
			this.StatusText = Loc.Get("Str.ScriptTools.SearchSearching");
			Progress<SearchProgress> progress = new Progress<SearchProgress>(this.OnProgress);
			SearchResult result;
			try
			{
				result = await this._searcher.SearchAsync(request, progress, cancellationToken);
			}
			catch (OperationCanceledException)
			{
				this.StatusText = Loc.Get("Str.ScriptTools.SearchCancelled");
				return;
			}
			catch (RegexMatchTimeoutException)
			{
				this.StatusText = String.Empty;
				this.ErrorMessage = Loc.Get("Str.ScriptTools.SearchErrRegexTimeout");
				return;
			}
			finally
			{
				this.IsSearching = false;
			}

			this.ShowResult(result);
		}

		private void OnProgress(SearchProgress progress)
		{
			if (!this.IsSearching)
			{
				return;
			}

			this.ProgressMaximum = Math.Max(1, progress.FilesTotal);
			this.ProgressValue = progress.FilesProcessed;
			this.StatusText = progress.FilesTotal == 0
				? Loc.Get("Str.ScriptTools.SearchListing")
				: Loc.Format("Str.ScriptTools.SearchProgress", progress.FilesProcessed, progress.FilesTotal, progress.MatchingLines);
		}

		private void ShowResult(SearchResult result)
		{
			this.Results = result.Hits.Select(hit => new SearchResultItem(hit)).ToList();
			if (result.Hits.Count == 0)
			{
				this.StatusText = Loc.Get("Str.ScriptTools.SearchNotFound");
				return;
			}

			String details = (result.Truncated ? Loc.Get("Str.ScriptTools.SearchSummaryTruncated") : String.Empty)
				+ (result.FilesSkipped > 0 ? Loc.Format("Str.ScriptTools.SearchSummarySkipped", result.FilesSkipped) : String.Empty);
			this.StatusText = Loc.Format("Str.ScriptTools.SearchSummary", result.Hits.Count, result.FilesWithMatches, details);
			this._workspace.ShowSearchResults(result.Hits.Select(hit => hit.ToBuildError()).ToList());
		}

		private SearchRequest? BuildRequest(SearchMatcher matcher)
		{
			Encoding fallback = this._settings.Settings.Editor.FileEncoding.ToEncoding();
			switch (this.Scope)
			{
				case SearchScope.CurrentScript:
					DocumentViewModel? active = this._workspace.ActiveDocument;
					if (active is null)
					{
						this.ErrorMessage = Loc.Get("Str.ScriptTools.SearchErrNoScript");
						return null;
					}

					return new SearchRequest
					{
						Matcher = matcher,
						Sources = new[] { ToSource(active) },
						FallbackEncoding = fallback,
					};

				case SearchScope.OpenScripts:
					List<SearchTextSource> sources = this._workspace.Documents.Select(ToSource).ToList();
					if (sources.Count == 0)
					{
						this.ErrorMessage = Loc.Get("Str.ScriptTools.SearchErrNoOpenScripts");
						return null;
					}

					return new SearchRequest
					{
						Matcher = matcher,
						Sources = sources,
						FallbackEncoding = fallback,
					};

				default:
					String path = this.FolderPath.Trim();
					if (path.Length == 0 || !Directory.Exists(path))
					{
						this.ErrorMessage = Loc.Get("Str.ScriptTools.SearchErrNoPath");
						return null;
					}

					String fullPath = Path.GetFullPath(path);
					this.RememberFolder(fullPath);
					Dictionary<String, String> openTexts = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
					foreach (KeyValuePair<String, String> pair in this._workspace.GetOpenDocumentTexts())
					{
						openTexts[pair.Key] = pair.Value;
					}

					return new SearchRequest
					{
						Matcher = matcher,
						Folder = new SearchFolder(fullPath, SearchFileMasks.Parse(this.FileMask), this.IncludeSubfolders, openTexts),
						FallbackEncoding = fallback,
					};
			}
		}

		private static SearchTextSource ToSource(DocumentViewModel document)
		{
			return new SearchTextSource(document.FilePath ?? BuildError.NoFile, document.TextDocument.Text);
		}

		private void RememberFolder(String path)
		{
			this._settings.Update(settings => settings.Paths.LastSearchDirectory = path);
			if (!this.Folders.Contains(path, StringComparer.OrdinalIgnoreCase))
			{
				Int32 index = 0;
				while (index < this.Folders.Count && StringComparer.OrdinalIgnoreCase.Compare(this.Folders[index], path) < 0)
				{
					index++;
				}

				this.Folders.Insert(index, path);
				this.SaveFolders();
			}
		}

		private void SaveFolders()
		{
			List<String> directories = this._settings.SearchHistory.Directories;
			directories.Clear();
			directories.AddRange(this.Folders);
		}

		[RelayCommand]
		private void BrowseFolder()
		{
			String? initial = Directory.Exists(this.FolderPath) ? this.FolderPath : null;
			String? picked = this._dialogs.PickFolder(Loc.Get("Str.ScriptTools.SearchPickFolder"), initial);
			if (picked is null)
			{
				return;
			}

			this.RememberFolder(picked);
			this.FolderPath = picked;
		}

		[RelayCommand]
		private void RemoveSavedFolder()
		{
			String path = this.FolderPath.Trim();
			Int32 index = this.Folders.ToList().FindIndex(item => String.Equals(item, path, StringComparison.OrdinalIgnoreCase));
			if (index < 0)
			{
				return;
			}

			this.Folders.RemoveAt(index);
			this.SaveFolders();
		}

		[RelayCommand(CanExecute = nameof(CanReplace))]
		private async Task ReplaceAsync()
		{
			if (!this.TryCreateMatcher(out SearchMatcher? matcher) || matcher is null)
			{
				return;
			}

			DocumentViewModel? document = this._workspace.ActiveDocument;
			IEditorSurface? editor = document?.Editor;
			if (document is not null && editor is not null && editor.SelectionLength > 0
				&& TryMatchSelection(document, editor, matcher, out LineMatch? match) && match is not null)
			{
				editor.ReplaceSelection(matcher.Expand(match, this.ReplaceText));
			}

			await this.FindNextCoreAsync(matcher);
		}

		[RelayCommand(CanExecute = nameof(CanReplace))]
		private async Task ReplaceAllAsync()
		{
			if (!this.TryCreateMatcher(out SearchMatcher? matcher) || matcher is null)
			{
				return;
			}

			List<DocumentViewModel> documents = this.GetDocumentsInScope();
			if (documents.Count == 0)
			{
				this.ErrorMessage = Loc.Get("Str.ScriptTools.SearchErrNoScript");
				return;
			}

			List<(DocumentViewModel Document, IReadOnlyList<TextEdit> Edits)> plans = new List<(DocumentViewModel, IReadOnlyList<TextEdit>)>();
			foreach (DocumentViewModel document in documents)
			{
				IReadOnlyList<TextEdit> edits = matcher.ComputeReplacements(document.TextDocument.Text, this.ReplaceText);
				if (edits.Count > 0)
				{
					plans.Add((document, edits));
				}
			}

			Int32 total = plans.Sum(plan => plan.Edits.Count);
			if (total == 0)
			{
				this.StatusText = Loc.Get("Str.ScriptTools.SearchNotFound");
				return;
			}

			if (plans.Count > 1
				&& !await this._dialogs.ConfirmAsync(
					Loc.Get("Str.ScriptTools.SearchReplaceAll"),
					Loc.Format("Str.ScriptTools.SearchConfirmReplaceAll", total, plans.Count),
					Loc.Get("Str.Common.Replace"),
					Loc.Get("Str.Common.Cancel")))
			{
				return;
			}

			foreach ((DocumentViewModel document, IReadOnlyList<TextEdit> edits) in plans)
			{
				ApplyEdits(document.TextDocument, edits);
			}

			this.StatusText = Loc.Format("Str.ScriptTools.SearchReplaced", total, plans.Count);
			this._workspace.ReportStatus(this.StatusText);
		}

		private List<DocumentViewModel> GetDocumentsInScope()
		{
			if (this.Scope == SearchScope.CurrentScript)
			{
				return this._workspace.ActiveDocument is null ? new List<DocumentViewModel>() : new List<DocumentViewModel> { this._workspace.ActiveDocument };
			}

			return this._workspace.Documents.ToList();
		}

		private static void ApplyEdits(TextDocument document, IReadOnlyList<TextEdit> edits)
		{
			document.UndoStack.StartUndoGroup();
			try
			{
				document.BeginUpdate();
				try
				{
					for (Int32 i = edits.Count - 1; i >= 0; i--)
					{
						document.Replace(edits[i].Offset, edits[i].Length, edits[i].NewText);
					}
				}
				finally
				{
					document.EndUpdate();
				}
			}
			finally
			{
				document.UndoStack.EndUndoGroup();
			}
		}

		private static Boolean TryMatchSelection(DocumentViewModel document, IEditorSurface editor, SearchMatcher matcher, out LineMatch? match)
		{
			match = null;
			TextDocument text = document.TextDocument;
			if (editor.SelectionStart < 0 || editor.SelectionStart + editor.SelectionLength > text.TextLength)
			{
				return false;
			}

			DocumentLine line = text.GetLineByOffset(editor.SelectionStart);
			Int32 relative = editor.SelectionStart - line.Offset;
			if (relative + editor.SelectionLength > line.Length)
			{
				return false;
			}

			String lineText = text.GetText(line.Offset, line.Length);
			match = matcher.FindAll(lineText).FirstOrDefault(item => item.Index == relative && item.Length == editor.SelectionLength);
			return match is not null;
		}

		[RelayCommand(CanExecute = nameof(CanReplace))]
		private async Task FindNextAsync()
		{
			if (this.TryCreateMatcher(out SearchMatcher? matcher) && matcher is not null)
			{
				await this.FindNextCoreAsync(matcher);
			}
		}

		private async Task FindNextCoreAsync(SearchMatcher matcher)
		{
			List<DocumentViewModel> documents = this.GetDocumentsInScope();
			DocumentViewModel? active = this._workspace.ActiveDocument;
			if (documents.Count == 0 || active is null)
			{
				this.ErrorMessage = Loc.Get("Str.ScriptTools.SearchErrNoScript");
				return;
			}

			Int32 firstIndex = Math.Max(documents.IndexOf(active), 0);
			DocumentViewModel first = documents[firstIndex];
			IEditorSurface? editor = first.Editor;
			Int32 start = editor is null ? 0 : (editor.SelectionLength > 0 ? editor.SelectionStart + editor.SelectionLength : editor.CaretOffset);

			TextMatch? found = matcher.FindNext(first.TextDocument.Text, start, false);
			DocumentViewModel? foundIn = found is null ? null : first;
			for (Int32 i = 1; found is null && i < documents.Count; i++)
			{
				DocumentViewModel document = documents[(firstIndex + i) % documents.Count];
				found = matcher.FindNext(document.TextDocument.Text, 0, false);
				foundIn = document;
			}

			if (found is null)
			{
				found = matcher.FindNext(first.TextDocument.Text, 0, false);
				foundIn = first;
			}

			if (found is null || foundIn is null)
			{
				this.StatusText = Loc.Get("Str.ScriptTools.SearchNotFound");
				return;
			}

			this.StatusText = String.Empty;
			await this.NavigateAsync(foundIn, found.Value.Line, found.Value.Column, found.Value.Length);
		}

		private async Task NavigateAsync(DocumentViewModel document, Int32 line, Int32 column, Int32 length)
		{
			if (document.FilePath is not null)
			{
				await this._workspace.GoToAsync(document.FilePath, line, column, length);
				return;
			}

			this._workspace.ActiveDocument = document;
			document.Editor?.GoTo(line, column, length);
		}

		[RelayCommand]
		private async Task GoToResultAsync(SearchResultItem? item)
		{
			SearchResultItem? target = item ?? this.SelectedResult;
			if (target is null)
			{
				return;
			}

			SearchHit hit = target.Hit;
			await this._workspace.GoToAsync(hit.FilePath == BuildError.NoFile ? null : hit.FilePath, hit.Line, hit.Column, hit.Length);
		}
	}
}
