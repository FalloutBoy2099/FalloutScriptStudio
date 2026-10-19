namespace FalloutScriptStudio.Workspace
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using Microsoft.Extensions.Logging;
	using Microsoft.Extensions.Logging.Abstractions;
	using Core.Analysis;
	using Core.Configuration;
	using Core.Indexing;
	using Core.Parsing;
	using Documents;
	using Localization;

	/// <summary>
	/// Keeps the index of the symbols of the project up to date and answers the questions of the navigation and of the refactorings: the
	/// project folder (or, without a project, the folders of the open scripts) and the headers folder are indexed in the background when
	/// the application starts, when the project, the open scripts or the headers folder change, and when a file is saved; the progress is
	/// written in the status bar and never blocks a command.
	/// </summary>
	public sealed class ProjectSymbolService : IDisposable
	{
		/// <summary>The default pause before a refresh starts (several changes in a row make one run).</summary>
		public static readonly TimeSpan DefaultDebounce = TimeSpan.FromMilliseconds(400);

		/// <summary>A run that reads fewer files than this is not announced in the status bar.</summary>
		public const Int32 MinimumReportedFiles = 10;

		private static readonly TimeSpan _statusInterval = TimeSpan.FromMilliseconds(250);

		private readonly IProjectSymbolIndex _index;
		private readonly ISettingsService _settings;
		private readonly IProjectService _projects;
		private readonly IOutputSink _output;
		private readonly ILogger _logger;
		private readonly TimeSpan _debounce;
		private readonly SynchronizationContext? _context = SynchronizationContext.Current;
		private readonly Int32 _uiThreadId = Environment.CurrentManagedThreadId;
		private readonly Object _gate = new();
		private IWorkspaceManager? _workspace;
		private CancellationTokenSource? _pending;
		private ProjectIndexRoots _lastRoots = ProjectIndexRoots.None;
		private System.Text.Encoding? _lastEncoding;
		private Int32 _runTotal;
		private Int64 _lastStatus;
		private Boolean _disposed;

		/// <summary>Initializes the service.</summary>
		/// <param name="index">The index to keep up to date.</param>
		/// <param name="settings">The settings (headers folder, code page of the files).</param>
		/// <param name="projects">The active project.</param>
		/// <param name="output">Receives the progress messages.</param>
		/// <param name="logger">Receives the diagnostics.</param>
		/// <param name="debounce">The pause before a refresh starts; <see cref="DefaultDebounce"/> when <see langword="null"/>.</param>
		public ProjectSymbolService(IProjectSymbolIndex index, ISettingsService settings, IProjectService projects, IOutputSink output, ILogger<ProjectSymbolService>? logger = null, TimeSpan? debounce = null)
		{
			ArgumentNullException.ThrowIfNull(index);
			ArgumentNullException.ThrowIfNull(settings);
			ArgumentNullException.ThrowIfNull(projects);
			ArgumentNullException.ThrowIfNull(output);
			this._index = index;
			this._settings = settings;
			this._projects = projects;
			this._output = output;
			this._logger = logger ?? NullLogger<ProjectSymbolService>.Instance;
			this._debounce = debounce ?? DefaultDebounce;
			this._index.ProgressChanged += this.OnProgress;
		}

		/// <summary>Gets the index.</summary>
		public IProjectSymbolIndex Index => this._index;

		/// <summary>Starts following the open documents and the project, and starts the first run.</summary>
		/// <param name="workspace">The workspace.</param>
		public void Attach(IWorkspaceManager workspace)
		{
			ArgumentNullException.ThrowIfNull(workspace);
			this._workspace = workspace;
			workspace.DocumentOpened += this.OnDocumentOpenedOrClosed;
			workspace.DocumentClosed += this.OnDocumentOpenedOrClosed;
			this._projects.ProjectChanged += this.OnProjectChanged;
			this._settings.SettingsChanged += this.OnSettingsChanged;
			this.RequestRefresh(true);
		}

		private void OnDocumentOpenedOrClosed(Object? sender, DocumentViewModel document)
		{
			this.RequestRefresh(false);
		}

		private void OnProjectChanged(Object? sender, EventArgs e)
		{
			this.RequestRefresh(true);
		}

		private void OnSettingsChanged(Object? sender, SettingsChangedEventArgs e)
		{
			this.RequestRefresh(false);
		}

		/// <summary>Determines what is indexed now: the project folder, or the folders of the open scripts, and the headers folder.</summary>
		/// <returns>The roots.</returns>
		public ProjectIndexRoots GetRoots()
		{
			List<ProjectIndexRoot> folders = [];
			List<String> files = [];
			IEnumerable<DocumentViewModel> documents = this._workspace?.Documents.Where(d => d.IsParsable && d.FilePath is not null).ToList() ?? Enumerable.Empty<DocumentViewModel>();
			foreach (DocumentViewModel document in documents)
			{
				files.Add(DocumentPaths.GetFullPath(document.FilePath!));
			}

			String? project = this._projects.IsOpen ? this._projects.Folder : null;
			if (!String.IsNullOrEmpty(project) && Directory.Exists(project))
			{
				folders.Add(new ProjectIndexRoot(Path.GetFullPath(project), true));
			}
			else
			{
				foreach (String? directory in files.Select(Path.GetDirectoryName).Distinct(StringComparer.OrdinalIgnoreCase))
				{
					if (!String.IsNullOrEmpty(directory))
					{
						folders.Add(new ProjectIndexRoot(directory, false));
					}
				}
			}

			String? headers = this._settings.Effective.Settings.Paths.HeadersDirectory;
			if (!String.IsNullOrWhiteSpace(headers) && Directory.Exists(headers))
			{
				folders.Add(new ProjectIndexRoot(Path.GetFullPath(headers), true));
			}

			return new ProjectIndexRoots { Folders = folders, Files = files };
		}

		/// <summary>
		/// Brings the index up to date now: lists the files of the roots and reads the ones that are new or changed. A change of the code
		/// page for files makes the index start again.
		/// </summary>
		/// <param name="cancellationToken">Cancels the run.</param>
		/// <returns>A task that completes when the index is up to date.</returns>
		public async Task RefreshAsync(CancellationToken cancellationToken = default)
		{
			await this.RefreshAsync(this.GetRoots(), cancellationToken).ConfigureAwait(false);
		}

		private async Task RefreshAsync(ProjectIndexRoots roots, CancellationToken cancellationToken)
		{
			System.Text.Encoding encoding = this._settings.Settings.Editor.FileEncoding.ToEncoding();
			lock (this._gate)
			{
				if (this._lastEncoding is not null && !Object.ReferenceEquals(this._lastEncoding, encoding))
				{
					this._index.Clear();
				}

				this._lastEncoding = encoding;
				this._lastRoots = roots;
			}

			await this._index.UpdateAsync(roots, cancellationToken).ConfigureAwait(false);
		}

		/// <summary>
		/// Asks for a refresh in the background after a short pause. A refresh that is already waiting or running is replaced.
		/// </summary>
		/// <param name="force">Whether to refresh even when the folders to index did not change.</param>
		public void RequestRefresh(Boolean force)
		{
			if (this._disposed)
			{
				return;
			}

			// The documents belong to the UI thread: the roots are computed there, and the background run only gets the result.
			if (this._context is not null && Environment.CurrentManagedThreadId != this._uiThreadId)
			{
				this.Post(() => this.RequestRefresh(force));
				return;
			}

			ProjectIndexRoots roots = this.GetRoots();
			if (!force)
			{
				lock (this._gate)
				{
					if (SameRoots(roots, this._lastRoots) && Object.ReferenceEquals(this._lastEncoding, this._settings.Settings.Editor.FileEncoding.ToEncoding()))
					{
						return;
					}
				}
			}

			CancellationTokenSource source = new();
			CancellationTokenSource? previous;
			lock (this._gate)
			{
				previous = this._pending;
				this._pending = source;
			}

			previous?.Cancel();
			_ = Task.Run(() => this.RunPendingAsync(source, roots), CancellationToken.None);
		}

		/// <summary>Updates the index after a file was saved or reloaded; the headers of the project change what the macros mean everywhere.</summary>
		/// <param name="path">The full path of the file.</param>
		/// <param name="text">The text that was saved, or <see langword="null"/> to read the file.</param>
		/// <returns>A task that completes when the index was updated.</returns>
		public async Task NotifySavedAsync(String path, String? text)
		{
			if (this._disposed || String.IsNullOrEmpty(path))
			{
				return;
			}

			try
			{
				await this._index.UpdateFileAsync(path, text).ConfigureAwait(false);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException)
			{
				this._logger.IndexUpdateFailed(path, ex.Message);
			}
		}

		/// <summary>Determines whether the index holds a symbol of that name.</summary>
		/// <param name="name">The name.</param>
		/// <returns><see langword="true"/> when there is one.</returns>
		public Boolean HasSymbol(String name)
		{
			return !String.IsNullOrEmpty(name) && this._index.Find(name).Count > 0;
		}

		/// <summary>Finds the symbols of a name, the ones nearest to a document first.</summary>
		/// <param name="name">The name.</param>
		/// <param name="kinds">The kinds to look for.</param>
		/// <param name="from">The document the symbol is used in, or <see langword="null"/>.</param>
		/// <returns>The candidates: the file itself, then the files it includes, then the others by distance of their folder.</returns>
		public IReadOnlyList<IndexedSymbol> FindCandidates(String name, IndexedSymbolKind kinds, DocumentViewModel? from)
		{
			IReadOnlyList<IndexedSymbol> found = this._index.Find(name, kinds);
			if (found.Count <= 1)
			{
				return found;
			}

			String? current = from?.FilePath is null ? null : DocumentPaths.GetFullPath(from.FilePath);
			IReadOnlySet<String>? included = null;
			if (from is not null && current is not null)
			{
				IncludeResolver resolver = this._settings.Effective.ToIncludeResolver(this._settings.Paths);
				included = this._index.GetIncludeClosure(resolver.GetAllIncludes(from.TextDocument.Text, current), resolver);
			}

			return SymbolCloseness.Order(found, current, included);
		}

		/// <summary>Finds the places where a symbol is written in the files that can see its definition.</summary>
		/// <param name="name">The name of the symbol.</param>
		/// <param name="kind">The kind of the symbol (a macro name is case sensitive).</param>
		/// <param name="definingFile">The file that defines the symbol, or <see langword="null"/> to search every file of the project.</param>
		/// <param name="allFiles">Whether to search every file even if the defining file is known (an exported or imported name).</param>
		/// <param name="cancellationToken">Cancels the search.</param>
		/// <returns>The places, ordered by file and position.</returns>
		public async Task<IReadOnlyList<SourceReference>> FindReferencesAsync(String name, IndexedSymbolKind kind, String? definingFile, Boolean allFiles, CancellationToken cancellationToken = default)
		{
			IReadOnlyList<SymbolOccurrence> occurrences = await this.FindOccurrencesAsync(name, kind, definingFile, allFiles, false, cancellationToken).ConfigureAwait(false);
			return occurrences.Select(o => new SourceReference(o.File, o.Line)).ToList();
		}

		/// <summary>Finds the places where a symbol is written in the files that can see its definition.</summary>
		/// <param name="name">The name of the symbol.</param>
		/// <param name="kind">The kind of the symbol (a macro name is case sensitive).</param>
		/// <param name="definingFile">The file that defines the symbol, or <see langword="null"/> to search every file of the project.</param>
		/// <param name="allFiles">Whether to search every file even if the defining file is known (an exported or imported name).</param>
		/// <param name="includeStrings">Whether a string literal that is exactly the name counts.</param>
		/// <param name="cancellationToken">Cancels the search.</param>
		/// <returns>The occurrences, ordered by file and position; the texts of the open documents are searched instead of their files.</returns>
		public async Task<IReadOnlyList<SymbolOccurrence>> FindOccurrencesAsync(String name, IndexedSymbolKind kind, String? definingFile, Boolean allFiles, Boolean includeStrings, CancellationToken cancellationToken = default)
		{
			ArgumentException.ThrowIfNullOrEmpty(name);

			IReadOnlyCollection<String>? files = null;
			Dictionary<String, String> openTexts = this._workspace is null
				? new(StringComparer.OrdinalIgnoreCase)
				: new Dictionary<String, String>(this._workspace.GetOpenDocumentTexts(), StringComparer.OrdinalIgnoreCase);
			if (definingFile is not null && !allFiles)
			{
				IncludeResolver resolver = this._settings.Effective.ToIncludeResolver(this._settings.Paths);
				HashSet<String> scope = new(this._index.GetFilesSeeing(DocumentPaths.GetFullPath(definingFile), resolver), StringComparer.OrdinalIgnoreCase);
				HashSet<String> known = new(this._index.Files, StringComparer.OrdinalIgnoreCase);
				known.UnionWith(openTexts.Keys);
				scope.IntersectWith(known);
				files = scope;
			}

			return await this._index.FindOccurrencesAsync(
				new SymbolOccurrenceRequest(name) { CaseSensitive = kind == IndexedSymbolKind.Macro, IncludeStrings = includeStrings, Files = files, OpenTexts = openTexts },
				cancellationToken).ConfigureAwait(false);
		}

		/// <summary>Stops the background runs.</summary>
		public void Dispose()
		{
			this._disposed = true;
			this._index.ProgressChanged -= this.OnProgress;
			this._projects.ProjectChanged -= this.OnProjectChanged;
			this._settings.SettingsChanged -= this.OnSettingsChanged;
			if (this._workspace is not null)
			{
				this._workspace.DocumentOpened -= this.OnDocumentOpenedOrClosed;
				this._workspace.DocumentClosed -= this.OnDocumentOpenedOrClosed;
			}

			CancellationTokenSource? pending;
			lock (this._gate)
			{
				pending = this._pending;
				this._pending = null;
			}

			pending?.Cancel();
		}

		private static Boolean SameRoots(ProjectIndexRoots a, ProjectIndexRoots b)
		{
			return a.Folders.Select(f => f.Path + (f.Recursive ? "|r" : "|f")).ToHashSet(StringComparer.OrdinalIgnoreCase)
					.SetEquals(b.Folders.Select(f => f.Path + (f.Recursive ? "|r" : "|f")))
				&& a.Files.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(b.Files);
		}

		private async Task RunPendingAsync(CancellationTokenSource source, ProjectIndexRoots roots)
		{
			try
			{
				if (this._debounce > TimeSpan.Zero)
				{
					await Task.Delay(this._debounce, source.Token).ConfigureAwait(false);
				}

				await this.RefreshAsync(roots, source.Token).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				// Replaced by a newer request, or the application is closing.
			}
			catch (Exception ex)
			{
				// Nobody awaits this run: whatever failed is logged here, never left as an unobserved task exception.
				this._logger.IndexRunFailed(ex.Message);
			}
			finally
			{
				lock (this._gate)
				{
					if (Object.ReferenceEquals(this._pending, source))
					{
						this._pending = null;
					}
				}

				source.Dispose();
			}
		}

		private void OnProgress(Object? sender, ProjectIndexProgress progress)
		{
			if (progress.Phase == ProjectIndexPhase.Indexing)
			{
				this._runTotal = Math.Max(this._runTotal, progress.Total);
				if (progress.Total < MinimumReportedFiles)
				{
					return;
				}

				Int64 now = System.Diagnostics.Stopwatch.GetTimestamp();
				if (progress.Done < progress.Total && System.Diagnostics.Stopwatch.GetElapsedTime(this._lastStatus, now) < _statusInterval)
				{
					return;
				}

				this._lastStatus = now;
				this.Post(() => this._output.ReportStatus(Loc.Format("Str.Shell.Status.Indexing", progress.Done, progress.Total)));
			}
			else if (progress.Phase == ProjectIndexPhase.Idle && this._runTotal >= MinimumReportedFiles)
			{
				this._runTotal = 0;
				Int32 files = this._index.FileCount;
				this.Post(() => this._output.ReportStatus(Loc.Format("Str.Shell.Status.Indexed", progress.Symbols, files)));
			}
			else if (progress.Phase == ProjectIndexPhase.Idle)
			{
				this._runTotal = 0;
			}
		}

		private void Post(Action action)
		{
			if (this._context is null)
			{
				action();
			}
			else
			{
				this._context.Post(static state => ((Action)state!)(), action);
			}
		}
	}
}