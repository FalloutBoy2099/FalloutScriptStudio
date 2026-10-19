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
	using ICSharpCode.AvalonEdit.Document;
	using Core.Compilation;
	using Core.Configuration;
	using Documents;
	using Localization;
	using Services;
	using Services.ScriptTools.Replace;
	using Services.ScriptTools.Search;

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
		private readonly IReplaceInFilesService? _replaceInFiles;
		private Boolean _loading = true;

		/// <summary>
		/// Initializes a new instance of the <see cref="AdvancedSearchViewModel"/> class.
		/// </summary>
		/// <param name="workspace">The open documents and navigation.</param>
		/// <param name="settings">The settings and the search history.</param>
		/// <param name="dialogs">Shows messages and the folder picker.</param>
		/// <param name="searcher">Runs the searches.</param>
		/// <param name="replaceInFiles">Runs "Replace in Files"; the command is unavailable without it.</param>
		public AdvancedSearchViewModel(IWorkspace workspace, ISettingsService settings, IDialogService dialogs, ScriptSearcher searcher, IReplaceInFilesService? replaceInFiles = null)
		{
			ArgumentNullException.ThrowIfNull(workspace);
			ArgumentNullException.ThrowIfNull(settings);
			ArgumentNullException.ThrowIfNull(dialogs);
			ArgumentNullException.ThrowIfNull(searcher);

			this._workspace = workspace;
			this._settings = settings;
			this._dialogs = dialogs;
			this._searcher = searcher;
			this._replaceInFiles = replaceInFiles;

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
		public ObservableCollection<String> QueryHistory { get; } = [];

		/// <summary>Gets the saved search folders.</summary>
		public ObservableCollection<String> Folders { get; } = [];

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
		[NotifyPropertyChangedFor(nameof(HasNoResults))]
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

		/// <summary>Gets a value indicating whether the list of matching lines is empty (the window then shows a hint instead of an empty grid).</summary>
		public Boolean HasNoResults => this.Results.Count == 0;

		/// <summary>Gets a value indicating whether <see cref="ErrorMessage"/> is shown.</summary>
		public Boolean HasError => this.ErrorMessage.Length > 0;

		/// <summary>
		/// Puts text into the search field.
		/// </summary>
		/// <param name="text">The text.</param>
		public void SetSearchText(String text)
		{
			this.SearchText = SearchTextSuggestion.FirstLine(text, Int32.MaxValue);
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

		partial void OnSearchTextChanged(String value)
		{
			// The field is a single line and a match never spans a line break: a pasted multi-line text keeps its first line, so the field
			// cannot grow to several lines and push the layout.
			if (value.AsSpan().IndexOfAny('\r', '\n') >= 0)
			{
				this.SearchText = SearchTextSuggestion.FirstLine(value, Int32.MaxValue);
			}
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
			Progress<SearchProgress> progress = new(this.OnProgress);
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
					Dictionary<String, String> openTexts = new(StringComparer.OrdinalIgnoreCase);
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
	}
}