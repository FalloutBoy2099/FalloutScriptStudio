namespace FalloutScriptStudio.Core.Configuration
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Runtime.ExceptionServices;
	using System.Text.Json;
	using System.Threading;
	using System.Threading.Tasks;
	using Microsoft.Extensions.Logging;

	/// <summary>
	/// <see cref="ISettingsService"/> that stores everything as JSON files in the settings folder.
	/// </summary>
	public sealed class SettingsService : ISettingsService, IDisposable
	{
		private readonly LegacySettingsImporter _legacyImporter;
		private readonly ILogger<SettingsService> _logger;
		private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);

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
						AtomicFile.WriteAllBytes(path, content);
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
						await AtomicFile.WriteAllBytesAsync(path, content, cancellationToken).ConfigureAwait(false);
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

		private void OnSettingsChanged()
		{
			this.SettingsChanged?.Invoke(this, new SettingsChangedEventArgs(this.Settings));
		}

		private void EnsureDirectories()
		{
			try
			{
				this.Paths.EnsureCreated();
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				this._logger.LogError(ex, "The program folders could not be created");
			}
		}

		private Byte[]? ReadBytes(String path)
		{
			try
			{
				return File.Exists(path) ? File.ReadAllBytes(path) : null;
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				this._logger.LogError(ex, "The file {Path} could not be read", path);
				return null;
			}
		}

		private async Task<Byte[]?> ReadBytesAsync(String path, CancellationToken cancellationToken)
		{
			try
			{
				return File.Exists(path) ? await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false) : null;
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				this._logger.LogError(ex, "The file {Path} could not be read", path);
				return null;
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

			AppSettings settings = this.Resolve(raw.Settings, this.Paths.SettingsFile, () => GetLegacy()?.Settings);
			settings.Normalize();

			RecentFiles recent = this.Resolve(raw.Recent, this.Paths.RecentFilesFile, () => GetLegacy()?.Recent);
			recent.Scripts ??= new List<String>();
			recent.Messages ??= new List<String>();

			ScriptPositions positions = this.Resolve(raw.Positions, this.Paths.ScriptPositionsFile, () => GetLegacy()?.Positions);
			FoldedProcedures folding = this.Resolve(raw.Folding, this.Paths.FoldingFile, () => GetLegacy()?.Folding);

			SearchHistory history = this.Resolve(raw.SearchHistory, this.Paths.SearchHistoryFile, () => GetLegacy()?.SearchHistory);
			history.Queries ??= new List<String>();
			history.Directories ??= new List<String>();

			PreprocessorDefines defines = this.Resolve(raw.Defines, this.Paths.PreprocessorDefinesFile, () => GetLegacy()?.Defines);
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
		}

		/// <summary>
		/// Deserializes a model. A missing file falls back to the legacy data, a damaged file to the defaults.
		/// </summary>
		private T Resolve<T>(Byte[]? content, String path, Func<T?> fromLegacy)
			where T : class, new()
		{
			if (content == null)
			{
				return fromLegacy() ?? new T();
			}

			try
			{
				T? model = JsonSerializer.Deserialize<T>(content, SettingsJson.Options);
				if (model != null)
				{
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

		private List<(String Path, Byte[] Content)> SerializeAll()
		{
			this.Settings.IsFirstRunCompleted = true;
			this.Settings.Version = AppSettings.CurrentVersion;

			return new List<(String Path, Byte[] Content)>
			{
				(this.Paths.SettingsFile, JsonSerializer.SerializeToUtf8Bytes(this.Settings, SettingsJson.Options)),
				(this.Paths.RecentFilesFile, JsonSerializer.SerializeToUtf8Bytes(this.Recent, SettingsJson.Options)),
				(this.Paths.ScriptPositionsFile, JsonSerializer.SerializeToUtf8Bytes(this.ScriptPositions, SettingsJson.Options)),
				(this.Paths.FoldingFile, JsonSerializer.SerializeToUtf8Bytes(this.FoldedProcedures, SettingsJson.Options)),
				(this.Paths.SearchHistoryFile, JsonSerializer.SerializeToUtf8Bytes(this.SearchHistory, SettingsJson.Options)),
				(this.Paths.PreprocessorDefinesFile, JsonSerializer.SerializeToUtf8Bytes(this.PreprocessorDefines, SettingsJson.Options)),
			};
		}

		private sealed record RawFiles(
			Byte[]? Settings,
			Byte[]? Recent,
			Byte[]? Positions,
			Byte[]? Folding,
			Byte[]? SearchHistory,
			Byte[]? Defines);

		/// <summary>Releases the lock of the service.</summary>
		public void Dispose()
		{
			this._gate.Dispose();
		}
	}
}
