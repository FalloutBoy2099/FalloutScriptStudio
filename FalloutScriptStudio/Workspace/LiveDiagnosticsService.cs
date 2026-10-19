namespace FalloutScriptStudio.Workspace
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using ICSharpCode.AvalonEdit.Document;
	using Microsoft.Extensions.Logging;
	using Microsoft.Extensions.Logging.Abstractions;
	using Core.Analysis;
	using Core.Compilation;
	using Core.Configuration;
	using Core.Parsing;
	using Documents;
	using Problems;

	/// <summary>
	/// Runs the semantic analysis of the open scripts (<see cref="ScriptDiagnostics"/>: a wrong number of arguments, an unknown name, a procedure
	/// used before its definition, a <c>begin</c> without <c>end</c>...) in the background after each parse and publishes the findings: they
	/// are stored in <see cref="DocumentViewModel.LiveDiagnostics"/> (the editor underlines them) and listed in the problems with their file,
	/// line, hint and quick fix. The analysis starts a short moment after the parse, a newer parse cancels the one that is waiting or running,
	/// and a result for a text that was edited meanwhile is dropped. The setting <see cref="EditorSettings.LiveDiagnostics"/> turns it off or
	/// limits it to the errors.
	/// </summary>
	public sealed class LiveDiagnosticsService : IDisposable
	{
		/// <summary>The default pause between a parse and the start of the analysis.</summary>
		public static readonly TimeSpan DefaultDebounce = TimeSpan.FromMilliseconds(120);

		private readonly ISettingsService _settings;
		private readonly IProblemService _problems;
		private readonly ProjectSymbolService? _symbols;
		private readonly ILogger _logger;
		private readonly TimeSpan _debounce;
		private readonly IncludeSymbolCache _includes = new();
		private readonly Dictionary<DocumentViewModel, Run> _runs = [];
		private readonly Object _catalogGate = new();
		private readonly SynchronizationContext? _context = SynchronizationContext.Current;
		private readonly Int32 _uiThreadId = Environment.CurrentManagedThreadId;
		private IWorkspaceManager? _workspace;
		private FunctionSignatureCatalog? _catalog;
		private String? _catalogStamp;

		/// <summary>Initializes the service.</summary>
		/// <param name="settings">Supplies the level of the analysis, the include folders, the code page and the folder of the function descriptions.</param>
		/// <param name="problems">Receives the findings.</param>
		/// <param name="symbols">The index of the project, used to find a header that the include folders do not lead to; may be <see langword="null"/>.</param>
		/// <param name="logger">Receives the failures of the analysis.</param>
		/// <param name="debounce">The pause between a parse and the analysis; <see cref="DefaultDebounce"/> when <see langword="null"/>.</param>
		public LiveDiagnosticsService(ISettingsService settings, IProblemService problems, ProjectSymbolService? symbols = null, ILogger<LiveDiagnosticsService>? logger = null, TimeSpan? debounce = null)
		{
			ArgumentNullException.ThrowIfNull(settings);
			ArgumentNullException.ThrowIfNull(problems);
			this._settings = settings;
			this._problems = problems;
			this._symbols = symbols;
			this._logger = logger ?? NullLogger<LiveDiagnosticsService>.Instance;
			this._debounce = debounce ?? DefaultDebounce;
		}

		/// <summary>Gets the level of the analysis in the settings.</summary>
		public LiveDiagnosticsLevel Level => this._settings.Settings.Editor.LiveDiagnostics;

		/// <summary>Connects the service to the workspace whose parses it follows. Called once by the workspace.</summary>
		/// <param name="workspace">The workspace.</param>
		public void Attach(IWorkspaceManager workspace)
		{
			ArgumentNullException.ThrowIfNull(workspace);
			this._workspace = workspace;
			workspace.DocumentParsed += this.OnDocumentParsed;
			workspace.DocumentClosed += this.OnDocumentClosed;
		}

		/// <summary>Gets the analysis that waits or runs for a document, so that a caller can wait for its end.</summary>
		/// <param name="document">The document.</param>
		/// <returns>The task, or <see langword="null"/> when no analysis is pending for the document.</returns>
		public Task? GetPending(DocumentViewModel document)
		{
			ArgumentNullException.ThrowIfNull(document);
			return this._runs.TryGetValue(document, out Run? run) ? run.Task : null;
		}

		/// <summary>Analyses the current text of a script now, on the calling thread, with the same environment as the background analysis.</summary>
		/// <param name="document">The script.</param>
		/// <param name="cancellationToken">Cancels the analysis.</param>
		/// <returns>The findings of every severity in text order (the level of the setting is not applied).</returns>
		public IReadOnlyList<ScriptDiagnostic> AnalyzeNow(DocumentViewModel document, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(document);
			return this.Compute(this.Capture(document), cancellationToken);
		}

		/// <summary>Cancels what waits or runs and forgets the findings of every document.</summary>
		public void Dispose()
		{
			if (this._workspace is not null)
			{
				this._workspace.DocumentParsed -= this.OnDocumentParsed;
				this._workspace.DocumentClosed -= this.OnDocumentClosed;
			}

			foreach (Run run in this._runs.Values.ToList())
			{
				run.Source.Cancel();
			}

			this._runs.Clear();
		}

		/// <summary>Schedules the analysis of a document: after the pause, off the UI thread.</summary>
		/// <param name="document">The document.</param>
		public void Schedule(DocumentViewModel document)
		{
			ArgumentNullException.ThrowIfNull(document);
			if (this._context is not null && Environment.CurrentManagedThreadId != this._uiThreadId)
			{
				this._context.Post(static state => ((Action)state!)(), () => this.Schedule(document));
				return;
			}

			this.Cancel(document);
			if (!IsAnalysed(document) || this.Level == LiveDiagnosticsLevel.Off)
			{
				this.Clear(document);
				return;
			}

			CancellationTokenSource source = new();
			Snapshot snapshot = this.Capture(document);
			Run run = new(source);
			this._runs[document] = run;
			run.Task = this.RunAsync(document, snapshot, run);
		}

		private static Boolean IsAnalysed(DocumentViewModel document)
		{
			return document.Kind == DocumentKind.Script;
		}

		private void OnDocumentParsed(Object? sender, DocumentViewModel document)
		{
			this.Schedule(document);
		}

		private void OnDocumentClosed(Object? sender, DocumentViewModel document)
		{
			this.Cancel(document);
			this.Clear(document);
		}

		private void Cancel(DocumentViewModel document)
		{
			if (this._runs.Remove(document, out Run? run))
			{
				run.Source.Cancel();
			}
		}

		private void Clear(DocumentViewModel document)
		{
			if (document.LiveDiagnostics.Count > 0)
			{
				document.LiveDiagnostics = Array.Empty<ScriptDiagnostic>();
			}

			this._problems.Remove(problem => problem.Origin == ProblemOrigins.LiveDiagnostics && Object.ReferenceEquals(problem.Owner, document));
		}

		private async Task RunAsync(DocumentViewModel document, Snapshot snapshot, Run run)
		{
			CancellationToken token = run.Source.Token;
			try
			{
				if (this._debounce > TimeSpan.Zero)
				{
					await Task.Delay(this._debounce, token);
				}

				IReadOnlyList<ScriptDiagnostic> found = await Task.Run(() => this.Compute(snapshot, token), token);
				token.ThrowIfCancellationRequested();
				this.Publish(document, snapshot, found);
			}
			catch (OperationCanceledException)
			{
				// Superseded by a newer parse, or the document was closed.
			}
			catch (Exception ex)
			{
				this._logger.DiagnosticsFailed(ex, document.FilePath ?? document.FileName);
			}
			finally
			{
				if (this._runs.TryGetValue(document, out Run? current) && Object.ReferenceEquals(current, run))
				{
					this._runs.Remove(document);
				}

				run.Source.Dispose();
			}
		}

		/// <summary>Takes what the analysis needs from the document; the text and the other state of a document belong to the UI thread.</summary>
		private Snapshot Capture(DocumentViewModel document)
		{
			EffectiveSettings effective = this._settings.Effective;
			Dictionary<String, String> unsaved = new(StringComparer.OrdinalIgnoreCase);
			if (this._workspace is not null)
			{
				foreach (DocumentViewModel other in this._workspace.Documents)
				{
					if (!Object.ReferenceEquals(other, document) && other.IsModified && other.IsParsable && other.FilePath is not null)
					{
						unsaved[DocumentPaths.GetFullPath(other.FilePath)] = other.TextDocument.Text;
					}
				}
			}

			return new Snapshot(
				document.TextDocument.Text,
				document.TextDocument.Version,
				document.FilePath is null ? null : DocumentPaths.GetFullPath(document.FilePath),
				document.ProgramInfo,
				unsaved,
				this._settings.Settings.Editor.FileEncoding.ToEncoding(),
				effective.ToIncludeResolver(this._settings.Paths),
				this.GetCatalog(),
				this._symbols?.Index.Files);
		}

		private IReadOnlyList<ScriptDiagnostic> Compute(Snapshot snapshot, CancellationToken cancellationToken)
		{
			SslTokenStream tokens = SslTokenizer.Tokenize(snapshot.Text);
			ScriptEnvironmentBuilder builder = new(
				snapshot.Resolver,
				this._includes,
				path => ScriptEnvironmentBuilder.ReadText(path, snapshot.Encoding),
				path => snapshot.Unsaved.TryGetValue(path, out String? text) ? text : null,
				include => Locate(snapshot.IndexedFiles, include));
			ProgramInfo? info = snapshot.Info;
			ScriptEnvironment environment = builder.Build(tokens, snapshot.Path, info is null ? null : info.FindMacro, snapshot.Catalog);
			cancellationToken.ThrowIfCancellationRequested();
			return ScriptDiagnostics.Analyze(tokens, environment, new ScriptDiagnosticOptions(), cancellationToken);
		}

		private static String? Locate(IReadOnlyCollection<String>? files, String include)
		{
			if (files is null)
			{
				return null;
			}

			String suffix = Path.DirectorySeparatorChar + include.Replace('/', Path.DirectorySeparatorChar).TrimStart('.', Path.DirectorySeparatorChar);
			foreach (String file in files)
			{
				if (file.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
				{
					return file;
				}
			}

			return null;
		}

		private void Publish(DocumentViewModel document, Snapshot snapshot, IReadOnlyList<ScriptDiagnostic> found)
		{
			ITextSourceVersion current = document.TextDocument.Version;
			if (!current.BelongsToSameDocumentAs(snapshot.Version) || current.CompareAge(snapshot.Version) != 0)
			{
				// The text changed while the analysis ran: the positions are stale, the parse that follows the edit starts another analysis.
				return;
			}

			LiveDiagnosticsLevel level = this.Level;
			HashSet<Int32> parserLines = document.ParserErrors
				.Where(error => error.Type == BuildErrorType.Error && error.Line > 0)
				.Select(error => error.Line)
				.ToHashSet();
			List<ScriptDiagnostic> shown = new(found.Count);
			foreach (ScriptDiagnostic diagnostic in found)
			{
				if (level == LiveDiagnosticsLevel.ErrorsOnly && diagnostic.Severity != ScriptDiagnosticSeverity.Error)
				{
					continue;
				}

				// The parser already says it on that line, unless the finding brings a fix.
				if (diagnostic.Severity == ScriptDiagnosticSeverity.Error && diagnostic.Fix is null && parserLines.Contains(diagnostic.Line))
				{
					continue;
				}

				shown.Add(diagnostic);
			}

			this._problems.Remove(problem => problem.Origin == ProblemOrigins.LiveDiagnostics && Object.ReferenceEquals(problem.Owner, document));
			document.LiveDiagnosticsVersion = snapshot.Version;
			document.LiveDiagnostics = shown;
			if (level != LiveDiagnosticsLevel.Off && shown.Count > 0)
			{
				this._problems.ReportRange(shown.Select(diagnostic => ProblemFactory.ScriptDiagnostic(diagnostic, document.FilePath, document)));
			}
		}

		/// <summary>Loads the descriptions of the functions again when one of the three files changed.</summary>
		private FunctionSignatureCatalog GetCatalog()
		{
			String descriptions = this._settings.Paths.DescriptionsDirectory;
			String opcodes = Path.Combine(descriptions, "opcodes.txt");
			String functions = Path.Combine(descriptions, "functions.ini");
			String user = Path.Combine(this._settings.Paths.SettingsDirectory, "userfunctions.ini");
			String stamp = Stamp(opcodes) + "|" + Stamp(functions) + "|" + Stamp(user);
			lock (this._catalogGate)
			{
				if (this._catalog is null || !String.Equals(this._catalogStamp, stamp, StringComparison.Ordinal))
				{
					this._catalog = FunctionSignatureCatalog.LoadFromFiles(opcodes, functions, user);
					this._catalogStamp = stamp;
				}

				return this._catalog;
			}
		}

		private static String Stamp(String path)
		{
			try
			{
				FileInfo info = new(path);
				return info.Exists ? path + ":" + info.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + info.LastWriteTimeUtc.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture) : path + ":none";
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
			{
				return path + ":unreadable";
			}
		}

		private sealed class Run
		{
			public Run(CancellationTokenSource source)
			{
				this.Source = source;
			}

			public CancellationTokenSource Source { get; }

			public Task Task { get; set; } = Task.CompletedTask;
		}

		private sealed record Snapshot(
			String Text,
			ITextSourceVersion Version,
			String? Path,
			ProgramInfo? Info,
			IReadOnlyDictionary<String, String> Unsaved,
			Encoding Encoding,
			IncludeResolver Resolver,
			FunctionSignatureCatalog Catalog,
			IReadOnlyCollection<String>? IndexedFiles);
	}
}