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
	public sealed partial class SettingsService : ISettingsService, IDisposable
	{
		private readonly LegacySettingsImporter _legacyImporter;
		private readonly ILogger<SettingsService> _logger;
		private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);

		/// <summary>The content of every file as it was when this instance loaded or last wrote it (the base of the merge).</summary>
		private readonly Dictionary<String, JsonNode> _baselines = new Dictionary<String, JsonNode>(StringComparer.OrdinalIgnoreCase);

		private readonly List<SettingsProblem> _problems = new List<SettingsProblem>();
		private Dictionary<String, Byte[]>? _pristine;
		private Boolean _loaded;
		private volatile ActiveProjectState _project = new ActiveProjectState(null, null);

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
		public event EventHandler<SettingsProblem>? ProblemRaised;

		/// <inheritdoc/>
		public AppPaths Paths { get; }

		/// <inheritdoc/>
		public IReadOnlyList<SettingsProblem> Problems
		{
			get
			{
				lock (this._problems)
				{
					return this._problems.ToArray();
				}
			}
		}

		/// <inheritdoc/>
		public AppSettings Settings { get; private set; } = new AppSettings();

		/// <inheritdoc/>
		public String? ActiveProjectFolder => this._project.Folder;

		/// <inheritdoc/>
		public ProjectSettings? ActiveProject => this._project.Settings;

		/// <inheritdoc/>
		public EffectiveSettings Effective
		{
			get
			{
				// One read: the folder and the settings of a project always belong together, even when another thread switches the project.
				ActiveProjectState project = this._project;
				return new EffectiveSettings(this.Settings, this.PreprocessorDefines, project.Folder, project.Settings);
			}
		}

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
						this._logger.FileNotSaved(ex, path);
						this.RaiseProblem(new SettingsProblem(SettingsProblemKind.SaveFailed, path, ex.Message));
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
						this._logger.FileNotSaved(ex, path);
						this.RaiseProblem(new SettingsProblem(SettingsProblemKind.SaveFailed, path, ex.Message));
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
		public void SetActiveProject(String? folder, ProjectSettings? project)
		{
			this._project = new ActiveProjectState(folder, project);
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

		private void RaiseProblem(SettingsProblem problem)
		{
			lock (this._problems)
			{
				this._problems.RemoveAll(existing => existing.Kind == problem.Kind && String.Equals(existing.Path, problem.Path, StringComparison.OrdinalIgnoreCase));
				this._problems.Add(problem);
			}

			this.ProblemRaised?.Invoke(this, problem);
		}

		private void OnSettingsChanged()
		{
			this.SettingsChanged?.Invoke(this, new SettingsChangedEventArgs(this.Settings));
		}

		/// <summary>The active project: its folder and its overrides, replaced as a whole.</summary>
		private sealed record ActiveProjectState(String? Folder, ProjectSettings? Settings);

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
