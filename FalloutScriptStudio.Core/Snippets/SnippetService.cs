namespace FalloutScriptStudio.Core.Snippets
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using Configuration;

	/// <inheritdoc />
	public sealed class SnippetService : ISnippetService
	{
		private readonly ISettingsService _settings;
		private readonly Object _gate = new Object();
		private Layer _user = new Layer(String.Empty);
		private Layer _project = new Layer(String.Empty);
		private IReadOnlyList<Snippet>? _merged;

		/// <summary>Initializes a new instance of the <see cref="SnippetService"/> class.</summary>
		/// <param name="settings">Gives the settings folder and the active project.</param>
		public SnippetService(ISettingsService settings)
		{
			ArgumentNullException.ThrowIfNull(settings);
			this._settings = settings;
			this.UserFilePath = Path.Combine(settings.Paths.SettingsDirectory, SnippetFile.UserFileName);
			this._user = new Layer(this.UserFilePath);
			settings.SettingsChanged += (_, _) => this.OnSettingsChanged();
		}

		/// <inheritdoc />
		public event EventHandler? Changed;

		/// <inheritdoc />
		public String UserFilePath { get; }

		/// <inheritdoc />
		public String? ProjectFilePath => GetProjectFilePath(this._settings);

		/// <inheritdoc />
		public IReadOnlyList<Snippet> GetSnippets()
		{
			lock (this._gate)
			{
				Boolean changed = Refresh(this._user, this.UserFilePath, SnippetSource.User);
				String? projectPath = this.ProjectFilePath;
				if (!String.Equals(this._project.Path, projectPath ?? String.Empty, StringComparison.OrdinalIgnoreCase))
				{
					this._project = new Layer(projectPath ?? String.Empty);
					changed = true;
				}

				changed |= projectPath is not null && Refresh(this._project, projectPath, SnippetSource.Project);
				if (changed || this._merged is null)
				{
					this._merged = SnippetCatalog.Merge(BuiltInSnippets.All, this._user.Snippets, this._project.Snippets);
				}

				return this._merged;
			}
		}

		/// <inheritdoc />
		public SnippetFileReadResult GetFile(SnippetSource source)
		{
			lock (this._gate)
			{
				if (source == SnippetSource.User)
				{
					_ = Refresh(this._user, this.UserFilePath, SnippetSource.User);
					return this._user.Result;
				}

				if (source == SnippetSource.Project && this.ProjectFilePath is { } projectPath)
				{
					if (!String.Equals(this._project.Path, projectPath, StringComparison.OrdinalIgnoreCase))
					{
						this._project = new Layer(projectPath);
					}

					_ = Refresh(this._project, projectPath, SnippetSource.Project);
					return this._project.Result;
				}

				return new SnippetFileReadResult(SnippetFileStatus.Missing, new SnippetFile());
			}
		}

		/// <inheritdoc />
		public Boolean TrySave(SnippetSource source, IReadOnlyList<SnippetDefinition> snippets, out String? error)
		{
			ArgumentNullException.ThrowIfNull(snippets);

			error = null;
			lock (this._gate)
			{
				String? path = source == SnippetSource.User ? this.UserFilePath : source == SnippetSource.Project ? this.ProjectFilePath : null;
				if (path is null)
				{
					error = "There is no file for these snippets.";
					return false;
				}

				SnippetFileReadResult current = this.GetFile(source);
				if (current.Status == SnippetFileStatus.Unreadable)
				{
					error = current.Error;
					return false;
				}

				SnippetFile file = current.File.Clone();
				file.Snippets = snippets.Select(snippet => snippet.Clone()).ToList();
				if (current.Status == SnippetFileStatus.Missing && file.Snippets.Count == 0)
				{
					return true;
				}

				try
				{
					SnippetFileStore.Write(path, file);
				}
				catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
				{
					error = ex.Message;
					return false;
				}

				// The file is read again at the next request, so that what the service holds is what is on the disk.
				(source == SnippetSource.User ? this._user : this._project).Invalidate();
				this._merged = null;
			}

			this.Changed?.Invoke(this, EventArgs.Empty);
			return true;
		}

		/// <inheritdoc />
		public void Reload()
		{
			lock (this._gate)
			{
				this._user.Invalidate();
				this._project.Invalidate();
				this._merged = null;
			}

			this.Changed?.Invoke(this, EventArgs.Empty);
		}

		private static String? GetProjectFilePath(ISettingsService settings)
		{
			String? folder = settings.Effective.ProjectFolder ?? settings.Settings.Paths.SolutionFolder;
			return String.IsNullOrWhiteSpace(folder) ? null : Path.Combine(folder, SnippetFile.ProjectFileName);
		}

		private void OnSettingsChanged()
		{
			Boolean projectChanged;
			lock (this._gate)
			{
				String projectPath = this.ProjectFilePath ?? String.Empty;
				projectChanged = !String.Equals(this._project.Path, projectPath, StringComparison.OrdinalIgnoreCase);
				if (projectChanged)
				{
					this._project = new Layer(projectPath);
					this._merged = null;
				}
			}

			if (projectChanged)
			{
				this.Changed?.Invoke(this, EventArgs.Empty);
			}
		}

		/// <summary>Reads a file again when its size or time changed.</summary>
		private static Boolean Refresh(Layer layer, String path, SnippetSource source)
		{
			FileStamp stamp = FileStamp.Of(path);
			if (layer.Loaded && layer.Stamp == stamp)
			{
				return false;
			}

			layer.Result = SnippetFileStore.Read(path);
			layer.Stamp = stamp;
			layer.Loaded = true;
			layer.Snippets = layer.Result.File.Snippets.Select(definition => definition.ToSnippet(source)).ToList();
			return true;
		}

		private readonly record struct FileStamp(Boolean Exists, Int64 Length, DateTime LastWriteUtc)
		{
			public static FileStamp Of(String path)
			{
				try
				{
					FileInfo info = new FileInfo(path);
					return info.Exists ? new FileStamp(true, info.Length, info.LastWriteTimeUtc) : default;
				}
				catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
				{
					return new FileStamp(true, -1, DateTime.MinValue);
				}
			}
		}

		private sealed class Layer
		{
			public Layer(String path)
			{
				this.Path = path;
				this.Result = new SnippetFileReadResult(SnippetFileStatus.Missing, new SnippetFile());
			}

			public String Path { get; }

			public Boolean Loaded { get; set; }

			public FileStamp Stamp { get; set; }

			public SnippetFileReadResult Result { get; set; }

			public IReadOnlyList<Snippet> Snippets { get; set; } = Array.Empty<Snippet>();

			public void Invalidate()
			{
				this.Loaded = false;
			}
		}
	}
}