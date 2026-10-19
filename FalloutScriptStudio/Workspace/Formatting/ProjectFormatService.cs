namespace FalloutScriptStudio.Workspace.Formatting
{
	using System;
	using FalloutScriptStudio.Core.IO;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Security.Cryptography;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using Core.Configuration;
	using Core.Formatting;
	using Core.TextTools;
	using Documents;
	using Problems;

	/// <inheritdoc />
	public sealed class ProjectFormatService : IProjectFormatService
	{
		private static readonly Char[] _maskSeparators = { ';', ',' };

		private readonly ISettingsService _settings;
		private readonly IWorkspaceManager _workspace;
		private readonly IDocumentFileWriter _writer;
		private readonly IProblemService _problems;
		private readonly CodeFormatter _formatter = new();

		/// <summary>Initializes a new instance of the <see cref="ProjectFormatService"/> class.</summary>
		/// <param name="settings">Gives the style, the encoding and the project folder.</param>
		/// <param name="workspace">Gives the open documents.</param>
		/// <param name="writer">Writes the files atomically; the usual writer when <see langword="null"/>.</param>
		/// <param name="problems">Receives the files that were left alone; a private list when <see langword="null"/>.</param>
		public ProjectFormatService(ISettingsService settings, IWorkspaceManager workspace, IDocumentFileWriter? writer = null, IProblemService? problems = null)
		{
			ArgumentNullException.ThrowIfNull(settings);
			ArgumentNullException.ThrowIfNull(workspace);
			this._settings = settings;
			this._workspace = workspace;
			this._writer = writer ?? new AtomicDocumentFileWriter();
			this._problems = problems ?? new ProblemService();
		}

		/// <inheritdoc />
		public async Task<ProjectFormatPreview> PreviewAsync(ProjectFormatRequest request, IProgress<FormatProgress>? progress, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(request);

			_ = this._problems.ClearOrigin(ProblemOrigins.FormatProject);
			FormatOptions options = this._settings.Effective.ToFormatOptions();
			Encoding fallback = this._settings.Settings.Editor.FileEncoding.ToEncoding();
			String? folder = this._settings.Effective.ProjectFolder ?? this._settings.Settings.Paths.SolutionFolder;
			List<DocumentViewModel> open = this._workspace.Documents.Where(document => document.IsParsable).ToList();

			// The text of the open documents is read here, on the thread that owns them; the files are read by the workers.
			List<Source> sources = [];
			HashSet<String> covered = new(StringComparer.OrdinalIgnoreCase);
			foreach (DocumentViewModel document in open)
			{
				String path = document.FilePath ?? document.FileName;
				Boolean inScope = request.Scope == FormatScope.OpenDocuments
					|| (document.FilePath is not null && folder is not null && IsInside(folder, document.FilePath) && MatchesMasks(document.FilePath, request.Masks));
				if (inScope)
				{
					sources.Add(new Source(path, document.TextDocument.Text, document));
					_ = covered.Add(path);
				}
			}

			if (request.Scope == FormatScope.ProjectFolder && folder is not null)
			{
				try
				{
					IReadOnlyList<String> files = await Task.Run(() => ProjectFileFinder.EnumerateFiles(folder, SplitMasks(request.Masks), true), cancellationToken);
					foreach (String file in files)
					{
						if (covered.Add(file))
						{
							sources.Add(new Source(file, null, null));
						}
					}
				}
				catch (OperationCanceledException)
				{
					return new ProjectFormatPreview(Array.Empty<FormatFileChange>(), 0, 0, true);
				}
			}

			ConcurrentBag<FormatFileChange> changes = [];
			ConcurrentBag<Problem> problems = [];
			Int32 examined = 0;
			Int32 unreadable = 0;
			Boolean cancelled = false;
			Int32 total = sources.Count;
			try
			{
				await Parallel.ForEachAsync(
					sources,
					new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = cancellationToken },
					(source, token) =>
					{
						token.ThrowIfCancellationRequested();
						FormatFileChange? change = this.Examine(source, request, options, fallback, folder, problems, out Boolean failed);
						if (change is not null)
						{
							changes.Add(change);
						}
						else if (failed)
						{
							_ = Interlocked.Increment(ref unreadable);
						}

						Int32 done = Interlocked.Increment(ref examined);
						progress?.Report(new FormatProgress(done, total, source.Path));
						return ValueTask.CompletedTask;
					});
			}
			catch (OperationCanceledException)
			{
				cancelled = true;
			}

			this._problems.ReportRange(problems);
			List<FormatFileChange> ordered = changes.OrderBy(change => change.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
			return new ProjectFormatPreview(ordered, examined, unreadable, cancelled);
		}

		/// <inheritdoc />
		public async Task<String> GetDiffAsync(FormatFileChange change, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(change);

			FormatOptions options = this._settings.Effective.ToFormatOptions();
			Encoding fallback = this._settings.Settings.Editor.FileEncoding.ToEncoding();
			String? text = change.Document?.TextDocument.Text;
			return await Task.Run(
				() =>
				{
					String original = text ?? DocumentTextCodec.Decode(SharedFile.ReadAllBytes(change.Path), fallback).Text;
					return UnifiedDiff.Create(original, this._formatter.Format(original, options));
				},
				cancellationToken);
		}

		/// <inheritdoc />
		public async Task<ProjectFormatResult> ApplyAsync(IReadOnlyList<FormatFileChange> changes, IProgress<FormatProgress>? progress, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(changes);

			_ = this._problems.ClearOrigin(ProblemOrigins.FormatProject);
			FormatOptions options = this._settings.Effective.ToFormatOptions();
			Encoding fallback = this._settings.Settings.Editor.FileEncoding.ToEncoding();
			Int32 formatted = 0;
			Int32 skipped = 0;
			Boolean cancelled = false;
			Boolean headerChanged = false;
			for (Int32 index = 0; index < changes.Count; index++)
			{
				FormatFileChange change = changes[index];
				progress?.Report(new FormatProgress(index, changes.Count, change.Path));
				if (cancellationToken.IsCancellationRequested)
				{
					cancelled = true;
					break;
				}

				Boolean done = change.Document is not null
					? this.ApplyToDocument(change, options)
					: await this.ApplyToFileAsync(change, options, fallback).ConfigureAwait(true);
				if (done)
				{
					formatted++;
					headerChanged |= String.Equals(System.IO.Path.GetExtension(change.Path), ".h", StringComparison.OrdinalIgnoreCase);
				}
				else
				{
					skipped++;
				}
			}

			progress?.Report(new FormatProgress(changes.Count, changes.Count, String.Empty));
			if (headerChanged)
			{
				await this._workspace.ReloadGlobalMacrosAsync().ConfigureAwait(true);
			}

			return new ProjectFormatResult(formatted, skipped, cancelled);
		}

		private static String HashText(String text)
		{
			return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
		}

		private static Boolean IsInside(String folder, String file)
		{
			String root = System.IO.Path.GetFullPath(folder).TrimEnd('\\', '/') + System.IO.Path.DirectorySeparatorChar;
			return System.IO.Path.GetFullPath(file).StartsWith(root, StringComparison.OrdinalIgnoreCase);
		}

		private static String[] SplitMasks(String masks)
		{
			String[] parts = masks.Split(_maskSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
			return parts.Length == 0 ? new[] { ProjectFormatRequest.DefaultMasks } : parts;
		}

		private static Boolean MatchesMasks(String file, String masks)
		{
			String name = System.IO.Path.GetFileName(file);
			foreach (String mask in SplitMasks(masks))
			{
				if (System.IO.Enumeration.FileSystemName.MatchesSimpleExpression(mask, name, true))
				{
					return true;
				}
			}

			return false;
		}

		private static String GetDisplayName(String path, String? folder)
		{
			return folder is not null && System.IO.Path.IsPathRooted(path) && IsInside(folder, path) ? System.IO.Path.GetRelativePath(folder, path) : path;
		}

		/// <summary>Reads a file or a snapshot of a document, formats it in memory and keeps only what is needed to find it again.</summary>
		private FormatFileChange? Examine(Source source, ProjectFormatRequest request, FormatOptions options, Encoding fallback, String? folder, ConcurrentBag<Problem> problems, out Boolean failed)
		{
			failed = false;
			try
			{
				String text;
				String hash;
				if (source.Text is not null)
				{
					text = source.Text;
					hash = HashText(text);
				}
				else
				{
					FileInfo info = new(source.Path);
					if (info.Length > request.MaxFileSize)
					{
						problems.Add(ProblemFactory.FormatFailed(source.Path, "> " + (request.MaxFileSize / (1024 * 1024)).ToString(System.Globalization.CultureInfo.InvariantCulture) + " MB"));
						failed = true;
						return null;
					}

					Byte[] bytes = SharedFile.ReadAllBytes(source.Path);
					hash = Convert.ToHexString(SHA256.HashData(bytes));
					text = DocumentTextCodec.Decode(bytes, fallback).Text;
				}

				String formatted = this._formatter.Format(text, options);
				if (String.Equals(text, formatted, StringComparison.Ordinal))
				{
					return null;
				}

				return new FormatFileChange(source.Path, GetDisplayName(source.Path, folder), LineDiff.CountChangedLines(text, formatted), hash, source.Document);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				problems.Add(ProblemFactory.FormatFailed(source.Path, ex.Message));
				failed = true;
				return null;
			}
		}

		private Boolean ApplyToDocument(FormatFileChange change, FormatOptions options)
		{
			DocumentViewModel document = change.Document!;
			if (!this._workspace.Documents.Contains(document) || !String.Equals(HashText(document.TextDocument.Text), change.Hash, StringComparison.Ordinal))
			{
				this._problems.Report(ProblemFactory.FormatFileChanged(change.Path));
				return false;
			}

			IReadOnlyList<TextEdit> edits = this._formatter.GetDocumentEdits(document.TextDocument.Text, options);
			document.TextDocument.ApplyEdits(edits);
			return true;
		}

		private async Task<Boolean> ApplyToFileAsync(FormatFileChange change, FormatOptions options, Encoding fallback)
		{
			try
			{
				if ((File.GetAttributes(change.Path) & FileAttributes.ReadOnly) != 0)
				{
					this._problems.Report(ProblemFactory.FormatReadOnly(change.Path));
					return false;
				}

				Byte[]? output = await Task.Run(
					() =>
					{
						Byte[] bytes = SharedFile.ReadAllBytes(change.Path);
						if (!String.Equals(Convert.ToHexString(SHA256.HashData(bytes)), change.Hash, StringComparison.Ordinal))
						{
							return null;
						}

						DecodedText decoded = DocumentTextCodec.Decode(bytes, fallback);
						return DocumentTextCodec.Encode(decoded.Encoding, this._formatter.Format(decoded.Text, options));
					}).ConfigureAwait(true);
				if (output is null)
				{
					this._problems.Report(ProblemFactory.FormatFileChanged(change.Path));
					return false;
				}

				await this._writer.WriteAsync(change.Path, output).ConfigureAwait(true);
				return true;
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				this._problems.Report(ProblemFactory.FormatFailed(change.Path, ex.Message));
				return false;
			}
		}

		private sealed record Source(String Path, String? Text, DocumentViewModel? Document);
	}
}