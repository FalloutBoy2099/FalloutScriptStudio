namespace FalloutScriptStudio.Core.Configuration
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Runtime.ExceptionServices;
	using System.Text.Json;
	using System.Text.Json.Nodes;
	using System.Threading;
	using System.Threading.Tasks;
	using Microsoft.Extensions.Logging;

	/// <summary>
	/// <see cref="ISettingsService"/> that stores everything as JSON files in the settings folder.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A file is only written when this instance has something to say about it: a file whose content is the same as when it was loaded is
	/// not touched, and a file that changed meanwhile (another instance of the program, for example the stand-alone message editor) is
	/// merged property by property, so a stale snapshot never wipes what another instance saved. A file that could not be read is never
	/// overwritten with the defaults, and a service that never loaded anything writes only the models that were really modified.
	/// </para>
	/// </remarks>
	public sealed class SettingsService : ISettingsService, IDisposable
	{
		private readonly LegacySettingsImporter _legacyImporter;
		private readonly ILogger<SettingsService> _logger;
		private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);

		/// <summary>The content of every file as it was when this instance loaded or last wrote it (the base of the merge).</summary>
		private readonly Dictionary<String, JsonNode> _baselines = new Dictionary<String, JsonNode>(StringComparer.OrdinalIgnoreCase);

		private Dictionary<String, Byte[]>? _pristine;
		private Boolean _loaded;

		/// <summary>
		/// Initializes a new instance of the <see cref="SettingsService"/> class.
		/// </summary>
		/// <param name="paths">The paths of the installation.</param>
		/// <param name="legacyImporter">Reads the files of the old editor when no new settings exist.</param>
		/// <param name="logger">The logger.</param>
		public SettingsService(AppPaths paths, LegacySettingsImporter legacyImporter, ILogger<SettingsService> logger)
		{
			ArgumentNullException.ThrowIfNull(paths);
			ArgumentNullException.ThrowIfNull(legacyImporter);
			ArgumentNullException.ThrowIfNull(logger);
			this.Paths = paths;
			this._legacyImporter = legacyImporter;
			this._logger = logger;
		}

		/// <inheritdoc/>
		public event EventHandler<SettingsChangedEventArgs>? SettingsChanged;

		/// <inheritdoc/>
		public AppPaths Paths { get; }

		/// <inheritdoc/>
		public AppSettings Settings { get; private set; } = new AppSettings();

		/// <inheritdoc/>
		public RecentFiles Recent { get; private set; } = new RecentFiles();

		/// <inheritdoc/>
		public ScriptPositions ScriptPositions { get; private set; } = new ScriptPositions();

		/// <inheritdoc/>
		public FoldedProcedures FoldedProcedures { get; private set; } = new FoldedProcedures();

		/// <inheritdoc/>
		public SearchHistory SearchHistory { get; private set; } = new SearchHistory();

		/// <inheritdoc/>
		public PreprocessorDefines PreprocessorDefines { get; private set; } = new PreprocessorDefines();

		/// <inheritdoc/>
		public Boolean ImportedLegacyData { get; private set; }

		/// <summary>Gets or sets how many times a file that cannot be read is tried before it is given up for this session.</summary>
		internal Int32 ReadAttempts { get; init; } = 4;

		/// <summary>Gets or sets the pause between two attempts to read a file (a virus scanner or a backup tool may hold it for a moment).</summary>
		internal TimeSpan ReadRetryDelay { get; init; } = TimeSpan.FromMilliseconds(60);

		/// <summary>Gets or sets the function that reads a file (replaced in tests).</summary>
		internal Func<String, Byte[]> ReadFile { get; init; } = File.ReadAllBytes;

		/// <inheritdoc/>
		public void Load()
		{
			this._gate.Wait();
			try
			{
				this.EnsureDirectories();
				this.ApplyLoaded(new RawFiles(
					this.ReadBytes(this.Paths.SettingsFile),
					this.ReadBytes(this.Paths.RecentFilesFile),
					this.ReadBytes(this.Paths.ScriptPositionsFile),
					this.ReadBytes(this.Paths.FoldingFile),
					this.ReadBytes(this.Paths.SearchHistoryFile),
					this.ReadBytes(this.Paths.PreprocessorDefinesFile)));
			}
			finally
			{
				this._gate.Release();
			}

			this.OnSettingsChanged();
		}

		/// <inheritdoc/>
		public async Task LoadAsync(CancellationToken cancellationToken = default)
		{
			await this._gate.WaitAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				this.EnsureDirectories();
				RawFiles raw = new RawFiles(
					await this.ReadBytesAsync(this.Paths.SettingsFile, cancellationToken).ConfigureAwait(false),
					await this.ReadBytesAsync(this.Paths.RecentFilesFile, cancellationToken).ConfigureAwait(false),
					await this.ReadBytesAsync(this.Paths.ScriptPositionsFile, cancellationToken).ConfigureAwait(false),
					await this.ReadBytesAsync(this.Paths.FoldingFile, cancellationToken).ConfigureAwait(false),
					await this.ReadBytesAsync(this.Paths.SearchHistoryFile, cancellationToken).ConfigureAwait(false),
					await this.ReadBytesAsync(this.Paths.PreprocessorDefinesFile, cancellationToken).ConfigureAwait(false));
				this.ApplyLoaded(raw);
			}
			finally
			{
				this._gate.Release();
			}

			this.OnSettingsChanged();
		}

		/// <inheritdoc/>
		public void Save()
		{
			this._gate.Wait();
			try
			{
				ExceptionDispatchInfo? failure = null;
				foreach ((String path, Byte[] content) in this.SerializeAll())
				{
					try
					{
						JsonNode? ours = ParseNode(content);
						SaveAction action = this.Decide(path, content, ours);
						Byte[]? toWrite = action == SaveAction.WriteOurs ? content : null;
						if (action == SaveAction.Merge)
						{
							toWrite = this.Merge(path, ours, content, this.ReadBytes(path));
						}

						if (toWrite != null)
						{
							AtomicFile.WriteAllBytes(path, toWrite);
							this.Remember(path, ours);
						}
					}
					catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
					{
						this._logger.LogError(ex, "The file {Path} could not be saved", path);
						failure ??= ExceptionDispatchInfo.Capture(ex);
					}
				}

				failure?.Throw();
			}
			finally
			{
				this._gate.Release();
			}
		}

		/// <inheritdoc/>
		public async Task SaveAsync(CancellationToken cancellationToken = default)
		{
			await this._gate.WaitAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				ExceptionDispatchInfo? failure = null;
				foreach ((String path, Byte[] content) in this.SerializeAll())
				{
					try
					{
						JsonNode? ours = ParseNode(content);
						SaveAction action = this.Decide(path, content, ours);
						Byte[]? toWrite = action == SaveAction.WriteOurs ? content : null;
						if (action == SaveAction.Merge)
						{
							toWrite = this.Merge(path, ours, content, await this.ReadBytesAsync(path, cancellationToken).ConfigureAwait(false));
						}

						if (toWrite != null)
						{
							await AtomicFile.WriteAllBytesAsync(path, toWrite, cancellationToken).ConfigureAwait(false);
							this.Remember(path, ours);
						}
					}
					catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
					{
						this._logger.LogError(ex, "The file {Path} could not be saved", path);
						failure ??= ExceptionDispatchInfo.Capture(ex);
					}
				}

				failure?.Throw();
			}
			finally
			{
				this._gate.Release();
			}
		}

		/// <inheritdoc/>
		public void Apply(AppSettings settings)
		{
			ArgumentNullException.ThrowIfNull(settings);
			settings.Normalize();
			this.Settings = settings;
			this.OnSettingsChanged();
		}

		/// <inheritdoc/>
		public void Update(Action<AppSettings> update)
		{
			ArgumentNullException.ThrowIfNull(update);
			update(this.Settings);
			this.Settings.Normalize();
			this.OnSettingsChanged();
		}

		/// <inheritdoc/>
		public void AddRecentScript(String path)
		{
			String? dropped = this.Recent.AddScript(path);
			if (dropped != null)
			{
				this.ScriptPositions.Remove(Path.GetFileName(dropped));
			}
		}

		private static JsonNode? ParseNode(Byte[] content)
		{
			return JsonNode.Parse(content);
		}

		/// <summary>
		/// Three-way merge of the JSON of a file: what this instance changed since it loaded the file (<paramref name="ours"/> against
		/// <paramref name="baseline"/>) is kept, everything else comes from the current content of the file (<paramref name="theirs"/>).
		/// </summary>
		private static JsonNode? MergeNodes(JsonNode? baseline, JsonNode? ours, JsonNode? theirs)
		{
			if (JsonNode.DeepEquals(ours, baseline))
			{
				return theirs?.DeepClone();
			}

			if (JsonNode.DeepEquals(theirs, baseline))
			{
				return ours?.DeepClone();
			}

			if (baseline is JsonObject baseObject && ours is JsonObject ourObject && theirs is JsonObject theirObject)
			{
				JsonObject merged = new JsonObject();
				foreach (String key in ourObject.Select(pair => pair.Key).Concat(theirObject.Select(pair => pair.Key)).Distinct(StringComparer.Ordinal))
				{
					JsonNode? value = MergeNodes(baseObject[key], ourObject[key], theirObject[key]);
					if (value != null)
					{
						merged[key] = value;
					}
				}

				return merged;
			}

			// Both sides changed the same value (or the shape of it): the instance that saves wins.
			return ours?.DeepClone();
		}

		private void OnSettingsChanged()
		{
			this.SettingsChanged?.Invoke(this, new SettingsChangedEventArgs(this.Settings));
		}

		private void EnsureDirectories()
		{
			try
			{
				foreach (String problem in this.Paths.EnsureCreated())
				{
					this._logger.LogError("The program folders could not be prepared: {Problem}", problem);
				}
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				this._logger.LogError(ex, "The program folders could not be created");
			}

			// A crash between the creation of a temporary file and its move leaves the temporary file behind for good.
			AtomicFile.DeleteOrphans(this.Paths.SettingsDirectory, TimeSpan.FromMinutes(10));
		}

		private FileRead ReadBytes(String path)
		{
			for (Int32 attempt = 1; ; attempt++)
			{
				try
				{
					return new FileRead(File.Exists(path) ? this.ReadFile(path) : null, false);
				}
				catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
				{
					return new FileRead(null, false);
				}
				catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
				{
					if (attempt >= this.ReadAttempts)
					{
						this._logger.LogError(ex, "The file {Path} could not be read; it is left untouched", path);
						return new FileRead(null, true);
					}

					if (this.ReadRetryDelay > TimeSpan.Zero)
					{
						// A short bounded retry for a file another program holds for a moment; not a polling loop.
						Thread.Sleep(this.ReadRetryDelay);
					}
				}
			}
		}

		private async Task<FileRead> ReadBytesAsync(String path, CancellationToken cancellationToken)
		{
			for (Int32 attempt = 1; ; attempt++)
			{
				try
				{
					return new FileRead(File.Exists(path) ? await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false) : null, false);
				}
				catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
				{
					return new FileRead(null, false);
				}
				catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
				{
					if (attempt >= this.ReadAttempts)
					{
						this._logger.LogError(ex, "The file {Path} could not be read; it is left untouched", path);
						return new FileRead(null, true);
					}

					await Task.Delay(this.ReadRetryDelay, cancellationToken).ConfigureAwait(false);
				}
			}
		}

		private void ApplyLoaded(RawFiles raw)
		{
			LegacyImportResult? legacy = null;
			Boolean legacyChecked = false;

			LegacyImportResult? GetLegacy()
			{
				if (!legacyChecked)
				{
					legacyChecked = true;
					LegacyImportResult imported = this._legacyImporter.ImportFromDirectory(this.Paths);
					legacy = imported.FoundAnything ? imported : null;
					foreach (String problem in imported.Problems)
					{
						this._logger.LogWarning("Legacy import: {Problem}", problem);
					}
				}

				return legacy;
			}

			HashSet<String> unreadable = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
			HashSet<String> loadedFromFile = new HashSet<String>(StringComparer.OrdinalIgnoreCase);

			T ResolveFile<T>(FileRead read, String path, Func<T?> fromLegacy)
				where T : class, new()
			{
				if (read.Failed)
				{
					unreadable.Add(path);
					return new T();
				}

				T model = this.Resolve(read.Content, path, fromLegacy, out Boolean fromFile);
				if (fromFile)
				{
					loadedFromFile.Add(path);
				}

				return model;
			}

			AppSettings settings = ResolveFile(raw.Settings, this.Paths.SettingsFile, () => GetLegacy()?.Settings);
			settings.Normalize();

			RecentFiles recent = ResolveFile(raw.Recent, this.Paths.RecentFilesFile, () => GetLegacy()?.Recent);
			recent.Scripts ??= new List<String>();
			recent.Messages ??= new List<String>();

			ScriptPositions positions = ResolveFile(raw.Positions, this.Paths.ScriptPositionsFile, () => GetLegacy()?.Positions);
			FoldedProcedures folding = ResolveFile(raw.Folding, this.Paths.FoldingFile, () => GetLegacy()?.Folding);

			SearchHistory history = ResolveFile(raw.SearchHistory, this.Paths.SearchHistoryFile, () => GetLegacy()?.SearchHistory);
			history.Queries ??= new List<String>();
			history.Directories ??= new List<String>();

			PreprocessorDefines defines = ResolveFile(raw.Defines, this.Paths.PreprocessorDefinesFile, () => GetLegacy()?.Defines);
			defines.Items ??= new List<String>();
			defines.Sanitize();

			this.Settings = settings;
			this.Recent = recent;
			this.ScriptPositions = positions;
			this.FoldedProcedures = folding;
			this.SearchHistory = history;
			this.PreprocessorDefines = defines;
			this.ImportedLegacyData = legacy != null;
			if (legacy != null)
			{
				this._logger.LogInformation("Settings were imported from the files of the old editor");
			}

			// The base of the later merge: the content of a file that was read, the defaults for one that could not be read (the first
			// save then keeps the real file and merges only what the user changed).
			this._baselines.Clear();
			Dictionary<String, Byte[]> pristine = this.GetPristine();
			List<(String Path, Byte[] Content)> current = SerializeModels(settings.Clone(), recent, positions, folding, history, defines, this.Paths);
			foreach ((String path, Byte[] content) in current)
			{
				if (unreadable.Contains(path))
				{
					this._baselines[path] = ParseNode(pristine[path])!;
				}
				else if (loadedFromFile.Contains(path))
				{
					this._baselines[path] = ParseNode(content)!;
				}
			}

			this._loaded = true;
		}

		/// <summary>
		/// Deserializes a model. A missing file falls back to the legacy data, a damaged file to the defaults.
		/// </summary>
		private T Resolve<T>(Byte[]? content, String path, Func<T?> fromLegacy, out Boolean fromFile)
			where T : class, new()
		{
			fromFile = false;
			if (content == null)
			{
				return fromLegacy() ?? new T();
			}

			try
			{
				T? model = JsonSerializer.Deserialize<T>(content, SettingsJson.Options);
				if (model != null)
				{
					fromFile = true;
					return model;
				}

				this._logger.LogWarning("The file {Path} is empty; defaults are used", path);
			}
			catch (Exception ex) when (ex is JsonException or NotSupportedException)
			{
				this._logger.LogWarning(ex, "The file {Path} is damaged; defaults are used", path);
				this.KeepCorruptFile(path);
			}

			return new T();
		}

		private void KeepCorruptFile(String path)
		{
			try
			{
				File.Copy(path, path + ".corrupt", true);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				this._logger.LogWarning(ex, "A copy of the damaged file {Path} could not be made", path);
			}
		}

		private SaveAction Decide(String path, Byte[] ours, JsonNode? oursNode)
		{
			if (this._baselines.TryGetValue(path, out JsonNode? baseline))
			{
				return JsonNode.DeepEquals(oursNode, baseline) ? SaveAction.Skip : SaveAction.Merge;
			}

			if (this._loaded)
			{
				// The file did not exist or was damaged when it was loaded: it is (re)created from the current state.
				return SaveAction.WriteOurs;
			}

			// Nothing was loaded (the start-up did not get that far): defaults must not replace the real files.
			return ours.AsSpan().SequenceEqual(this.GetPristine()[path]) ? SaveAction.Skip : SaveAction.WriteOurs;
		}

		/// <summary>
		/// Merges the changes of this instance into the current content of the file.
		/// </summary>
		/// <returns>The bytes to write, or <see langword="null"/> when the file cannot be read now and must be left alone.</returns>
		private Byte[]? Merge(String path, JsonNode? ours, Byte[] oursBytes, FileRead disk)
		{
			if (disk.Failed)
			{
				this._logger.LogWarning("The file {Path} cannot be read and is not overwritten", path);
				return null;
			}

			if (disk.Content == null)
			{
				return oursBytes;
			}

			JsonNode? theirs;
			try
			{
				theirs = ParseNode(disk.Content);
			}
			catch (JsonException)
			{
				return oursBytes;
			}

			JsonNode? merged = MergeNodes(this._baselines[path], ours, theirs);
			return merged == null ? oursBytes : JsonSerializer.SerializeToUtf8Bytes(merged, SettingsJson.Options);
		}

		/// <summary>After a write: what this instance wrote from is the base of the next merge (not the merged result, which holds changes of others).</summary>
		private void Remember(String path, JsonNode? ours)
		{
			if (ours != null)
			{
				this._baselines[path] = ours;
			}
		}

		private Dictionary<String, Byte[]> GetPristine()
		{
			return this._pristine ??= SerializeModels(
				new AppSettings(),
				new RecentFiles(),
				new ScriptPositions(),
				new FoldedProcedures(),
				new SearchHistory(),
				new PreprocessorDefines(),
				this.Paths).ToDictionary(item => item.Path, item => item.Content, StringComparer.OrdinalIgnoreCase);
		}

		private List<(String Path, Byte[] Content)> SerializeAll()
		{
			return SerializeModels(this.Settings, this.Recent, this.ScriptPositions, this.FoldedProcedures, this.SearchHistory, this.PreprocessorDefines, this.Paths);
		}

		private static List<(String Path, Byte[] Content)> SerializeModels(
			AppSettings settings,
			RecentFiles recent,
			ScriptPositions positions,
			FoldedProcedures folding,
			SearchHistory history,
			PreprocessorDefines defines,
			AppPaths paths)
		{
			settings.IsFirstRunCompleted = true;
			settings.Version = AppSettings.CurrentVersion;

			return new List<(String Path, Byte[] Content)>
			{
				(paths.SettingsFile, JsonSerializer.SerializeToUtf8Bytes(settings, SettingsJson.Options)),
				(paths.RecentFilesFile, JsonSerializer.SerializeToUtf8Bytes(recent, SettingsJson.Options)),
				(paths.ScriptPositionsFile, JsonSerializer.SerializeToUtf8Bytes(positions, SettingsJson.Options)),
				(paths.FoldingFile, JsonSerializer.SerializeToUtf8Bytes(folding, SettingsJson.Options)),
				(paths.SearchHistoryFile, JsonSerializer.SerializeToUtf8Bytes(history, SettingsJson.Options)),
				(paths.PreprocessorDefinesFile, JsonSerializer.SerializeToUtf8Bytes(defines, SettingsJson.Options)),
			};
		}

		private enum SaveAction
		{
			Skip,
			WriteOurs,
			Merge,
		}

		/// <summary>The outcome of reading a file: its content (null when the file does not exist) or the fact that it exists but cannot be read.</summary>
		private sealed record FileRead(Byte[]? Content, Boolean Failed);

		private sealed record RawFiles(
			FileRead Settings,
			FileRead Recent,
			FileRead Positions,
			FileRead Folding,
			FileRead SearchHistory,
			FileRead Defines);

		/// <summary>Releases the lock of the service.</summary>
		public void Dispose()
		{
			this._gate.Dispose();
		}
	}
}
