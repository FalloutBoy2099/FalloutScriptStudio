namespace FalloutScriptStudio.Core.Configuration
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Text.Json;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>Reading the settings files (with retries), the legacy import and the defaults for damaged or missing files.</summary>
	public sealed partial class SettingsService
	{
		private void EnsureDirectories()
		{
			try
			{
				foreach (String problem in this.Paths.EnsureCreated())
				{
					this._logger.FoldersNotPrepared(problem);
				}
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				this._logger.FoldersNotCreated(ex);
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
						this._logger.FileNotRead(ex, path);
						this.RaiseProblem(new SettingsProblem(SettingsProblemKind.Unreadable, path, ex.Message));
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
						this._logger.FileNotRead(ex, path);
						this.RaiseProblem(new SettingsProblem(SettingsProblemKind.Unreadable, path, ex.Message));
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
						this._logger.LegacyImportProblem(problem);
					}
				}

				return legacy;
			}

			HashSet<String> unreadable = new(StringComparer.OrdinalIgnoreCase);
			HashSet<String> loadedFromFile = new(StringComparer.OrdinalIgnoreCase);

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
			recent.Scripts ??= [];
			recent.Messages ??= [];
			recent.Projects ??= [];
			recent.ProjectSessions ??= [];

			ScriptPositions positions = ResolveFile(raw.Positions, this.Paths.ScriptPositionsFile, () => GetLegacy()?.Positions);
			FoldedProcedures folding = ResolveFile(raw.Folding, this.Paths.FoldingFile, () => GetLegacy()?.Folding);

			SearchHistory history = ResolveFile(raw.SearchHistory, this.Paths.SearchHistoryFile, () => GetLegacy()?.SearchHistory);
			history.Queries ??= [];
			history.Directories ??= [];

			PreprocessorDefines defines = ResolveFile(raw.Defines, this.Paths.PreprocessorDefinesFile, () => GetLegacy()?.Defines);
			defines.Items ??= [];
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
				this._logger.LegacyImported();
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

				this._logger.FileEmpty(path);
			}
			catch (Exception ex) when (ex is JsonException or NotSupportedException)
			{
				this._logger.FileDamaged(ex, path);
				this.RaiseProblem(new SettingsProblem(SettingsProblemKind.Damaged, path));
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
				this._logger.CorruptCopyFailed(ex, path);
			}
		}
	}
}