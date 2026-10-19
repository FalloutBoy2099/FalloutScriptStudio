namespace FalloutScriptStudio.Core.Indexing
{
	using System;
	using FalloutScriptStudio.Core.IO;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.IO;
	using System.IO.Enumeration;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using Microsoft.Extensions.Logging;
	using Microsoft.Extensions.Logging.Abstractions;
	using Analysis;
	using Parsing;

	/// <summary>
	/// Default <see cref="IProjectSymbolIndex"/>. The work runs on thread pool threads, a run does not start before the previous one ended,
	/// and the snapshot that queries read is replaced as a whole, so a query never sees half an update.
	/// </summary>
	public sealed class ProjectSymbolIndex : IProjectSymbolIndex, IDisposable
	{
		/// <summary>A file written less than this before it was read is read again at the next run: its time stamp may not tell two saves apart yet.</summary>
		private static readonly Int64 _recentTicks = TimeSpan.FromSeconds(2).Ticks;

		private static readonly TimeSpan _progressInterval = TimeSpan.FromMilliseconds(100);

		private readonly Func<ProjectIndexOptions> _options;
		private readonly ILogger _logger;
		private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
		private IndexSnapshot _snapshot = IndexSnapshot.Empty;
		private ProjectIndexProgress _progress = ProjectIndexProgress.Idle;

		/// <summary>Initializes a new instance of the <see cref="ProjectSymbolIndex"/> class.</summary>
		/// <param name="options">Supplies the options of each run (the code page can change in the settings); the defaults when <see langword="null"/>.</param>
		/// <param name="logger">Receives diagnostic messages.</param>
		public ProjectSymbolIndex(Func<ProjectIndexOptions>? options = null, ILogger<ProjectSymbolIndex>? logger = null)
		{
			this._options = options ?? (static () => new ProjectIndexOptions());
			this._logger = logger ?? NullLogger<ProjectSymbolIndex>.Instance;
		}

		/// <inheritdoc />
		public event EventHandler<ProjectIndexProgress>? ProgressChanged;

		/// <inheritdoc />
		public event EventHandler? Updated;

		/// <inheritdoc />
		public ProjectIndexProgress Progress => Volatile.Read(ref this._progress);

		/// <inheritdoc />
		public Int32 FileCount => Volatile.Read(ref this._snapshot).Files.Count;

		/// <inheritdoc />
		public Int32 SymbolCount => Volatile.Read(ref this._snapshot).Symbols.Length;

		/// <inheritdoc />
		public Boolean IsTruncated => Volatile.Read(ref this._snapshot).IsTruncated;

		/// <inheritdoc />
		public IReadOnlyCollection<String> Files => Volatile.Read(ref this._snapshot).Files.Keys;

		/// <inheritdoc />
		public async Task UpdateAsync(ProjectIndexRoots roots, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(roots);

			await this._gate.WaitAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				await Task.Run(() => this.Run(roots, cancellationToken), cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				this._gate.Release();
				this.SetProgress(new ProjectIndexProgress(ProjectIndexPhase.Idle, 0, 0, this.SymbolCount));
			}
		}

		/// <inheritdoc />
		public async Task UpdateFileAsync(String path, String? text = null, CancellationToken cancellationToken = default)
		{
			ArgumentException.ThrowIfNullOrEmpty(path);

			ProjectIndexOptions options = this._options();
			String full = Path.GetFullPath(path);
			if (!HasIndexedExtension(full, options))
			{
				return;
			}

			await this._gate.WaitAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				await Task.Run(() => this.ReplaceFile(full, text, options), cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				this._gate.Release();
			}
		}

		/// <inheritdoc />
		public void Clear()
		{
			Volatile.Write(ref this._snapshot, IndexSnapshot.Empty);
			this.Updated?.Invoke(this, EventArgs.Empty);
		}

		/// <inheritdoc />
		public IReadOnlyList<IndexedSymbol> Find(String name, IndexedSymbolKind kinds = IndexedSymbolKind.All)
		{
			ArgumentNullException.ThrowIfNull(name);

			if (!Volatile.Read(ref this._snapshot).ByName.TryGetValue(name, out IndexedSymbol[]? symbols))
			{
				return Array.Empty<IndexedSymbol>();
			}

			List<IndexedSymbol> result = new(symbols.Length);
			foreach (IndexedSymbol symbol in symbols)
			{
				if ((symbol.Kind & kinds) != 0 && (symbol.Kind != IndexedSymbolKind.Macro || String.Equals(symbol.Name, name, StringComparison.Ordinal)))
				{
					result.Add(symbol);
				}
			}

			return result;
		}

		/// <inheritdoc />
		public IReadOnlyList<IndexedSymbol> Search(String? filter, Int32 maxResults, IndexedSymbolKind kinds = IndexedSymbolKind.All)
		{
			if (maxResults <= 0)
			{
				return Array.Empty<IndexedSymbol>();
			}

			IndexSnapshot snapshot = Volatile.Read(ref this._snapshot);
			IReadOnlyList<String> terms = SymbolMatcher.SplitTerms(filter);
			if (terms.Count == 0)
			{
				List<IndexedSymbol> first = new(Math.Min(maxResults, snapshot.Symbols.Length));
				foreach (IndexedSymbol symbol in snapshot.Sorted)
				{
					if ((symbol.Kind & kinds) != 0)
					{
						first.Add(symbol);
						if (first.Count >= maxResults)
						{
							break;
						}
					}
				}

				return first;
			}

			PriorityQueue<Match, Match> best = new PriorityQueue<Match, Match>(Comparer<Match>.Create(static (a, b) => SymbolMatcher.Compare(b.Tier, b.Symbol, a.Tier, a.Symbol)));
			foreach (IndexedSymbol symbol in snapshot.Symbols)
			{
				if ((symbol.Kind & kinds) == 0)
				{
					continue;
				}

				Int32 tier = SymbolMatcher.GetTier(symbol, terms);
				if (tier == SymbolMatcher.NoMatch)
				{
					continue;
				}

				Match match = new Match(symbol, tier);
				if (best.Count < maxResults)
				{
					best.Enqueue(match, match);
				}
				else if (best.TryPeek(out Match worst, out _) && SymbolMatcher.Compare(tier, symbol, worst.Tier, worst.Symbol) < 0)
				{
					best.Dequeue();
					best.Enqueue(match, match);
				}
			}

			IndexedSymbol[] ordered = new IndexedSymbol[best.Count];
			for (Int32 i = ordered.Length - 1; i >= 0; i--)
			{
				ordered[i] = best.Dequeue().Symbol;
			}

			return ordered;
		}

		/// <inheritdoc />
		public async Task<IReadOnlyList<SymbolOccurrence>> FindOccurrencesAsync(SymbolOccurrenceRequest request, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(request);

			ProjectIndexOptions options = this._options();
			IReadOnlyCollection<String> files = request.Files ?? Volatile.Read(ref this._snapshot).Files.Keys;
			String[] paths = files.OrderBy(static f => f, StringComparer.OrdinalIgnoreCase).ToArray();
			return await Task.Run(() => ScanOccurrences(request, options, paths, cancellationToken), cancellationToken).ConfigureAwait(false);
		}

		/// <inheritdoc />
		public IReadOnlySet<String> GetFilesSeeing(String file, IncludeResolver resolver)
		{
			ArgumentNullException.ThrowIfNull(file);
			ArgumentNullException.ThrowIfNull(resolver);

			IndexSnapshot snapshot = Volatile.Read(ref this._snapshot);
			Dictionary<String, List<String>> includedBy = new(StringComparer.OrdinalIgnoreCase);
			Dictionary<String, String> resolved = new(StringComparer.OrdinalIgnoreCase);
			foreach (IndexedFile indexed in snapshot.Files.Values)
			{
				String? directory = Path.GetDirectoryName(indexed.Path);
				foreach (String raw in indexed.Includes)
				{
					String target = Resolve(resolver, resolved, raw, directory);
					if (!includedBy.TryGetValue(target, out List<String>? list))
					{
						list = new(1);
						includedBy.Add(target, list);
					}

					list.Add(indexed.Path);
				}
			}

			HashSet<String> result = new(StringComparer.OrdinalIgnoreCase) { file };
			Queue<String> pending = new Queue<String>();
			pending.Enqueue(file);
			while (pending.Count > 0)
			{
				String current = pending.Dequeue();
				if (includedBy.TryGetValue(current, out List<String>? parents))
				{
					foreach (String parent in parents)
					{
						if (result.Add(parent))
						{
							pending.Enqueue(parent);
						}
					}
				}
			}

			return result;
		}

		/// <inheritdoc />
		public IReadOnlySet<String> GetIncludeClosure(IEnumerable<String> seeds, IncludeResolver resolver)
		{
			ArgumentNullException.ThrowIfNull(seeds);
			ArgumentNullException.ThrowIfNull(resolver);

			IndexSnapshot snapshot = Volatile.Read(ref this._snapshot);
			Dictionary<String, String> resolved = new(StringComparer.OrdinalIgnoreCase);
			HashSet<String> result = new(StringComparer.OrdinalIgnoreCase);
			Queue<String> pending = new Queue<String>();
			foreach (String seed in seeds)
			{
				if (result.Add(seed))
				{
					pending.Enqueue(seed);
				}
			}

			while (pending.Count > 0)
			{
				String current = pending.Dequeue();
				if (!snapshot.Files.TryGetValue(current, out IndexedFile? indexed))
				{
					continue;
				}

				String? directory = Path.GetDirectoryName(current);
				foreach (String raw in indexed.Includes)
				{
					String target = Resolve(resolver, resolved, raw, directory);
					if (result.Add(target))
					{
						pending.Enqueue(target);
					}
				}
			}

			return result;
		}

		/// <summary>Releases the lock of the runs.</summary>
		public void Dispose()
		{
			this._gate.Dispose();
		}

		private static String Resolve(IncludeResolver resolver, Dictionary<String, String> cache, String raw, String? directory)
		{
			String key = (directory ?? String.Empty) + '|' + raw;
			if (!cache.TryGetValue(key, out String? target))
			{
				target = resolver.Resolve(raw, directory);
				cache.Add(key, target);
			}

			return target;
		}

		private static Boolean HasIndexedExtension(String path, ProjectIndexOptions options)
		{
			ReadOnlySpan<Char> extension = Path.GetExtension(path.AsSpan());
			foreach (String candidate in options.Extensions)
			{
				if (extension.Equals(candidate, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}

			return false;
		}

		private static List<FileStamp> Enumerate(ProjectIndexRoots roots, ProjectIndexOptions options, CancellationToken cancellationToken)
		{
			Dictionary<String, FileStamp> found = new(StringComparer.OrdinalIgnoreCase);
			String[] extensions = options.Extensions.ToArray();
			foreach (ProjectIndexRoot root in roots.Folders)
			{
				cancellationToken.ThrowIfCancellationRequested();
				if (String.IsNullOrEmpty(root.Path) || !Directory.Exists(root.Path))
				{
					continue;
				}

				EnumerationOptions enumeration = new EnumerationOptions
				{
					RecurseSubdirectories = root.Recursive,
					IgnoreInaccessible = true,
					ReturnSpecialDirectories = false,
				};
				FileSystemEnumerable<FileStamp> files = new FileSystemEnumerable<FileStamp>(
					root.Path,
					static (ref FileSystemEntry entry) => new FileStamp(entry.ToFullPath(), entry.Length, entry.LastWriteTimeUtc.UtcTicks),
					enumeration)
				{
					ShouldIncludePredicate = (ref FileSystemEntry entry) => !entry.IsDirectory && HasExtension(entry.FileName, extensions),
					ShouldRecursePredicate = static (ref FileSystemEntry entry) => entry.FileName.Length > 0 && entry.FileName[0] != '.',
				};
				Int32 counter = 0;
				foreach (FileStamp stamp in files)
				{
					if ((++counter & 0xFF) == 0)
					{
						cancellationToken.ThrowIfCancellationRequested();
					}

					found.TryAdd(stamp.Path, stamp);
				}
			}

			foreach (String file in roots.Files)
			{
				if (String.IsNullOrEmpty(file) || !HasIndexedExtension(file, options))
				{
					continue;
				}

				try
				{
					FileInfo info = new FileInfo(file);
					if (info.Exists)
					{
						found.TryAdd(info.FullName, new FileStamp(info.FullName, info.Length, info.LastWriteTimeUtc.Ticks));
					}
				}
				catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
				{
					// A file that cannot be examined is not indexed.
				}
			}

			return found.Values.ToList();
		}

		private static Boolean HasExtension(ReadOnlySpan<Char> fileName, String[] extensions)
		{
			ReadOnlySpan<Char> extension = Path.GetExtension(fileName);
			foreach (String candidate in extensions)
			{
				if (extension.Equals(candidate, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}

			return false;
		}

		private static List<SymbolOccurrence> ScanOccurrences(SymbolOccurrenceRequest request, ProjectIndexOptions options, String[] paths, CancellationToken cancellationToken)
		{
			IReadOnlyList<SymbolOccurrence>[] results = new IReadOnlyList<SymbolOccurrence>[paths.Length];
			ParallelOptions parallel = new ParallelOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = options.MaxDegreeOfParallelism };
			Parallel.For(0, paths.Length, parallel, i =>
			{
				String path = paths[i];
				String? text = null;
				if (request.OpenTexts is not null && request.OpenTexts.TryGetValue(path, out String? open))
				{
					text = open;
				}
				else
				{
					try
					{
						FileInfo info = new FileInfo(path);
						if (info.Exists && info.Length <= options.MaxFileBytes)
						{
							Byte[] bytes = SharedFile.ReadAllBytes(path);
							text = IndexTextDecoder.LooksBinary(bytes) ? null : IndexTextDecoder.Decode(bytes, options.Encoding);
						}
					}
					catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
					{
						text = null;
					}
				}

				results[i] = text is null
					? Array.Empty<SymbolOccurrence>()
					: SymbolOccurrenceScanner.Scan(text, path, request.Name, request.CaseSensitive, request.IncludeStrings);
			});

			return results.SelectMany(static r => r).ToList();
		}

		private void Run(ProjectIndexRoots roots, CancellationToken cancellationToken)
		{
			Stopwatch watch = Stopwatch.StartNew();
			ProjectIndexOptions options = this._options();
			IndexSnapshot old = Volatile.Read(ref this._snapshot);
			this.SetProgress(new ProjectIndexProgress(ProjectIndexPhase.Scanning, 0, 0, old.Symbols.Length));

			List<FileStamp> found = Enumerate(roots, options, cancellationToken);
			Dictionary<String, IndexedFile> files = new Dictionary<String, IndexedFile>(found.Count, StringComparer.OrdinalIgnoreCase);
			List<FileStamp> toRead = [];
			Int32 keptSymbols = 0;
			foreach (FileStamp stamp in found)
			{
				if (old.Files.TryGetValue(stamp.Path, out IndexedFile? known) && IsUnchanged(known, stamp))
				{
					files.Add(stamp.Path, known);
					keptSymbols += known.Symbols.Length;
				}
				else
				{
					toRead.Add(stamp);
				}
			}

			if (toRead.Count == 0 && files.Count == old.Files.Count)
			{
				return;
			}

			RunState state = new RunState(Math.Max(options.MaxSymbols - keptSymbols, 0));
			IndexedFile?[] read = new IndexedFile?[toRead.Count];
			Int32 total = toRead.Count;
			this.SetProgress(new ProjectIndexProgress(ProjectIndexPhase.Indexing, 0, total, old.Symbols.Length));
			ParallelOptions parallel = new ParallelOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = options.MaxDegreeOfParallelism };
			Parallel.For(0, total, parallel, i =>
			{
				read[i] = this.ReadFile(toRead[i], null, options, state);
				Int32 done = Interlocked.Increment(ref state.Done);
				this.ReportProgress(state, done, total, keptSymbols);
			});

			cancellationToken.ThrowIfCancellationRequested();
			foreach (IndexedFile? file in read)
			{
				if (file is not null)
				{
					files[file.Path] = file;
				}
			}

			if (state.Truncated)
			{
				this._logger.LimitReached(options.MaxSymbols);
			}

			IndexSnapshot snapshot = new IndexSnapshot(files, state.Truncated);
			Volatile.Write(ref this._snapshot, snapshot);
			this._logger.Updated(total, files.Count, snapshot.Symbols.Length, watch.ElapsedMilliseconds);
			this.Updated?.Invoke(this, EventArgs.Empty);
		}

		private void ReplaceFile(String path, String? text, ProjectIndexOptions options)
		{
			IndexSnapshot old = Volatile.Read(ref this._snapshot);
			Dictionary<String, IndexedFile> files = new Dictionary<String, IndexedFile>(old.Files, StringComparer.OrdinalIgnoreCase);

			FileInfo info = new FileInfo(path);
			if (text is null && !info.Exists)
			{
				if (!files.Remove(path))
				{
					return;
				}
			}
			else
			{
				FileStamp stamp = info.Exists ? new FileStamp(path, info.Length, info.LastWriteTimeUtc.Ticks) : new FileStamp(path, text?.Length ?? 0, DateTime.UtcNow.Ticks);
				RunState state = new RunState(Math.Max(options.MaxSymbols - (old.Symbols.Length - (files.TryGetValue(path, out IndexedFile? previous) ? previous.Symbols.Length : 0)), 0));
				IndexedFile? replacement = this.ReadFile(stamp, text, options, state);
				if (replacement is null)
				{
					return;
				}

				files[path] = replacement;
			}

			Volatile.Write(ref this._snapshot, new IndexSnapshot(files, old.IsTruncated));
			this.Updated?.Invoke(this, EventArgs.Empty);
		}

		private IndexedFile? ReadFile(FileStamp stamp, String? knownText, ProjectIndexOptions options, RunState state)
		{
			Int64 startedAt = DateTime.UtcNow.Ticks;
			String text;
			try
			{
				if (knownText is null)
				{
					if (stamp.Length > options.MaxFileBytes)
					{
						return new IndexedFile(stamp.Path, stamp.Length, stamp.LastWriteTicks, startedAt, Array.Empty<IndexedSymbol>(), Array.Empty<String>());
					}

					Byte[] bytes = SharedFile.ReadAllBytes(stamp.Path);
					if (bytes.Length > options.MaxFileBytes || IndexTextDecoder.LooksBinary(bytes))
					{
						return new IndexedFile(stamp.Path, stamp.Length, stamp.LastWriteTicks, startedAt, Array.Empty<IndexedSymbol>(), Array.Empty<String>());
					}

					text = IndexTextDecoder.Decode(bytes, options.Encoding);
				}
				else
				{
					text = knownText;
				}
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				this._logger.FileNotRead(stamp.Path, ex.Message);

				// The time stamp makes the file "recent", so that the next run reads it again.
				return new IndexedFile(stamp.Path, stamp.Length, stamp.LastWriteTicks, stamp.LastWriteTicks, Array.Empty<IndexedSymbol>(), Array.Empty<String>());
			}

			List<IndexedSymbol> symbols = [];
			List<String> includes = [];
			SymbolFileScanner.Scan(stamp.Path, text, symbols, includes);

			Int32 left = Interlocked.Add(ref state.Remaining, -symbols.Count);
			if (left < 0)
			{
				Interlocked.Add(ref state.Remaining, symbols.Count);
				state.Truncated = true;
				return new IndexedFile(stamp.Path, stamp.Length, stamp.LastWriteTicks, startedAt, Array.Empty<IndexedSymbol>(), includes.ToArray());
			}

			return new IndexedFile(stamp.Path, stamp.Length, stamp.LastWriteTicks, startedAt, symbols.ToArray(), includes.ToArray());
		}

		private static Boolean IsUnchanged(IndexedFile known, FileStamp stamp)
		{
			return known.Length == stamp.Length
				&& known.LastWriteTicks == stamp.LastWriteTicks
				&& known.IndexedAtTicks - stamp.LastWriteTicks > _recentTicks;
		}

		private void ReportProgress(RunState state, Int32 done, Int32 total, Int32 baseSymbols)
		{
			Int64 now = Stopwatch.GetTimestamp();
			Int64 last = Volatile.Read(ref state.LastReport);
			Boolean due = done == total || Stopwatch.GetElapsedTime(last, now) >= _progressInterval;
			if (due && (done == total || Interlocked.CompareExchange(ref state.LastReport, now, last) == last))
			{
				this.SetProgress(new ProjectIndexProgress(ProjectIndexPhase.Indexing, done, total, baseSymbols));
			}
		}

		private void SetProgress(ProjectIndexProgress progress)
		{
			Volatile.Write(ref this._progress, progress);
			this.ProgressChanged?.Invoke(this, progress);
		}

		private readonly record struct FileStamp(String Path, Int64 Length, Int64 LastWriteTicks);

		private readonly record struct Match(IndexedSymbol Symbol, Int32 Tier);

		private sealed class RunState
		{
			internal Int32 Done;
			internal Int32 Remaining;
			internal Int64 LastReport;
			internal volatile Boolean Truncated;

			internal RunState(Int32 remaining)
			{
				this.Remaining = remaining;
			}
		}
	}
}