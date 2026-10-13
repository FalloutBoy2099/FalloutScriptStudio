namespace FalloutScriptStudio.Core.Configuration
{
	using System;
	using System.IO;
	using System.Security.Cryptography;
	using System.Threading;
	using System.Threading.Tasks;
	using Microsoft.Extensions.Logging;

	/// <summary>
	/// Watches <c>fss-project.json</c> of a project folder and reports what really changed: the new valid content, an invalid content, or
	/// the deletion. A file system event only starts a quiet period; the file is examined once nothing happened for
	/// <see cref="ProjectFileWatchOptions.Debounce"/>, so an editor that saves through a temporary file and a rename (or deletes and recreates
	/// the file) is one change. A locked file is read again with a bounded backoff, a content that is not valid (or a file that has
	/// disappeared) is looked at again before it is reported, so a half-written file is never applied. A content that equals the one the
	/// editor itself read or wrote (<see cref="Acknowledge"/>) is ignored, which is what keeps the editor's own save from causing a reload.
	/// A slow safety net compares the size and the time of the file in case the file system reports nothing.
	/// </summary>
	public sealed class ProjectFileWatcher : IDisposable
	{
		private static readonly FileState Absent = new FileState(false, String.Empty);

		private readonly String _folder;
		private readonly String _path;
		private readonly ProjectFileWatchOptions _options;
		private readonly Action<ProjectFileChange> _onChange;
		private readonly ILogger? _logger;
		private readonly Object _sync = new Object();
		private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();
		private readonly SemaphoreSlim _examining = new SemaphoreSlim(1, 1);
		private FileSystemWatcher? _watcher;
		private Timer? _debounce;
		private Timer? _poll;
		private FileState? _known;
		private FileStamp _lastStamp;
		private Boolean _started;
		private Boolean _disposed;

		/// <summary>
		/// Initializes a new instance of the <see cref="ProjectFileWatcher"/> class. Nothing is watched until <see cref="Start"/> is called.
		/// </summary>
		/// <param name="projectFolder">The project folder.</param>
		/// <param name="options">How to watch.</param>
		/// <param name="onChange">Called, on a thread-pool thread, for every verified change.</param>
		/// <param name="logger">The logger, or <see langword="null"/>.</param>
		public ProjectFileWatcher(String projectFolder, ProjectFileWatchOptions options, Action<ProjectFileChange> onChange, ILogger? logger = null)
		{
			ArgumentException.ThrowIfNullOrEmpty(projectFolder);
			ArgumentNullException.ThrowIfNull(options);
			ArgumentNullException.ThrowIfNull(onChange);
			this._folder = projectFolder;
			this._path = ProjectSettingsFile.GetPath(projectFolder);
			this._options = options;
			this._onChange = onChange;
			this._logger = logger;
		}

		/// <summary>Gets the folder that is watched.</summary>
		public String Folder => this._folder;

		/// <summary>
		/// Starts watching. The content the editor holds is the reference: only a different content is reported.
		/// </summary>
		/// <param name="current">The bytes the editor read, or <see langword="null"/> when there is no file or it could not be read (then any content found later is reported).</param>
		/// <param name="exists">Whether the file exists; with <see langword="false"/> a file that appears is reported as a change.</param>
		public void Start(Byte[]? current, Boolean exists)
		{
			lock (this._sync)
			{
				if (this._disposed || this._started)
				{
					return;
				}

				this._started = true;
				this._known = exists ? (current is null ? null : Describe(current)) : Absent;
				this._lastStamp = FileStamp.Of(this._path);
			}

			if (!this._options.Enabled)
			{
				return;
			}

			this._debounce = new Timer(this.OnDebounceElapsed, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
			if (this._options.UseFileSystemEvents)
			{
				this.StartFileSystemWatcher();
			}

			if (this._options.PollInterval > TimeSpan.Zero)
			{
				this._poll = new Timer(this.OnPoll, null, this._options.PollInterval, this._options.PollInterval);
			}
		}

		/// <summary>
		/// Tells the watcher what the editor itself read or is about to write, so that this content is not reported as a change made by somebody else.
		/// </summary>
		/// <param name="content">The bytes, or <see langword="null"/> when the file does not exist.</param>
		public void Acknowledge(Byte[]? content)
		{
			lock (this._sync)
			{
				this._known = content is null ? Absent : Describe(content);
			}
		}

		/// <summary>Forgets what is known about the file: whatever it contains next is reported (used when the file could not be read).</summary>
		public void Forget()
		{
			lock (this._sync)
			{
				this._known = null;
			}
		}

		/// <summary>Stops watching.</summary>
		public void Dispose()
		{
			FileSystemWatcher? watcher;
			Timer? debounce;
			Timer? poll;
			lock (this._sync)
			{
				if (this._disposed)
				{
					return;
				}

				this._disposed = true;
				watcher = this._watcher;
				debounce = this._debounce;
				poll = this._poll;
				this._watcher = null;
				this._debounce = null;
				this._poll = null;
			}

			this._cancellation.Cancel();
			if (watcher is not null)
			{
				watcher.EnableRaisingEvents = false;
				watcher.Dispose();
			}

			debounce?.Dispose();
			poll?.Dispose();
			this._cancellation.Dispose();
			this._examining.Dispose();
		}

		private static FileState Describe(Byte[] content)
		{
			return new FileState(true, Convert.ToHexString(SHA256.HashData(content)));
		}

		private void StartFileSystemWatcher()
		{
			try
			{
				if (!Directory.Exists(this._folder))
				{
					return;
				}

				FileSystemWatcher watcher = new FileSystemWatcher(this._folder, ProjectSettings.FileName)
				{
					NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
					IncludeSubdirectories = false,
				};
				watcher.Changed += this.OnFileSystemEvent;
				watcher.Created += this.OnFileSystemEvent;
				watcher.Deleted += this.OnFileSystemEvent;
				watcher.Renamed += this.OnFileSystemEvent;
				watcher.Error += this.OnFileSystemEvent;
				lock (this._sync)
				{
					if (this._disposed)
					{
						watcher.Dispose();
						return;
					}

					this._watcher = watcher;
				}

				watcher.EnableRaisingEvents = true;
			}
			catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException or PlatformNotSupportedException)
			{
				// Only the safety net remains.
				this._logger?.ProjectFolderNotWatched(ex, this._folder);
			}
		}

		private void OnFileSystemEvent(Object sender, EventArgs e)
		{
			this.Schedule(this._options.Debounce);
		}

		private void OnPoll(Object? state)
		{
			FileStamp stamp = FileStamp.Of(this._path);
			Boolean changed;
			lock (this._sync)
			{
				changed = !this._disposed && stamp != this._lastStamp;
			}

			if (changed)
			{
				this.Schedule(this._options.Debounce);
			}
		}

		private void Schedule(TimeSpan delay)
		{
			lock (this._sync)
			{
				if (!this._disposed)
				{
					try
					{
						this._debounce?.Change(delay, Timeout.InfiniteTimeSpan);
					}
					catch (ObjectDisposedException)
					{
						// Disposed meanwhile.
					}
				}
			}
		}

		private void OnDebounceElapsed(Object? state)
		{
			_ = this.ExamineSafelyAsync();
		}

		private async Task ExamineSafelyAsync()
		{
			try
			{
				await this.ExamineAsync(this._cancellation.Token).ConfigureAwait(false);
			}
			catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
			{
				// The watcher was disposed.
			}
			catch (Exception ex)
			{
				// A background task must never end the process; the next event starts over.
				this._logger?.ProjectWatchFailed(ex, this._path);
			}
		}

		/// <summary>Looks at the file until its state is settled and reports a change when there is one.</summary>
		private async Task ExamineAsync(CancellationToken cancellationToken)
		{
			await this._examining.WaitAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				FileStamp stamp = FileStamp.Of(this._path);
				Int32 settleAttempts = Math.Max(1, this._options.SettleAttempts);
				for (Int32 attempt = 0; ; attempt++)
				{
					ReadOutcome read = await this.ReadAsync(cancellationToken).ConfigureAwait(false);
					if (read.Failure is not null)
					{
						// Locked or unreadable: nothing is applied; the state is not settled, so the file is examined again later.
						this._logger?.ProjectFileLocked(this._path, read.Failure);
						this.Schedule(this._options.PollInterval > TimeSpan.Zero ? this._options.PollInterval : this._options.Debounce * 4);
						return;
					}

					FileState state = read.Content is null ? Absent : Describe(read.Content);
					if (this.IsKnown(state))
					{
						this.Settle(stamp);
						return;
					}

					ProjectFileChange? change = this.Classify(read.Content, attempt >= settleAttempts);
					if (change is not null)
					{
						this.Remember(state, stamp);
						this.Deliver(change);
						return;
					}

					// Not valid yet or gone: an editor may still be writing; look again after a pause that doubles each time.
					await Task.Delay(TimeSpan.FromTicks(this._options.RetryDelay.Ticks << Math.Min(attempt, 8)), cancellationToken).ConfigureAwait(false);
					stamp = FileStamp.Of(this._path);
				}
			}
			finally
			{
				_ = this._examining.Release();
			}
		}

		/// <summary>Decides what a content that differs from the known one means. Returns <see langword="null"/> when it should be looked at again (it may be a write in progress).</summary>
		private ProjectFileChange? Classify(Byte[]? content, Boolean final)
		{
			if (content is null)
			{
				return final ? new ProjectFileChange(ProjectFileChangeKind.Deleted) : null;
			}

			if (ProjectSettingsFile.TryParse(content, this._folder, out ProjectSettings? settings, out ProjectFileError? error))
			{
				return new ProjectFileChange(ProjectFileChangeKind.Modified, new ProjectFileReadResult(ProjectFileStatus.Loaded, settings, null, content));
			}

			if (!final)
			{
				return null;
			}

			return new ProjectFileChange(ProjectFileChangeKind.Invalid, null, error);
		}

		private async Task<ReadOutcome> ReadAsync(CancellationToken cancellationToken)
		{
			Int32 attempts = Math.Max(1, this._options.ReadAttempts);
			for (Int32 attempt = 1; ; attempt++)
			{
				try
				{
					if (!File.Exists(this._path))
					{
						return new ReadOutcome(null, null);
					}

					using FileStream stream = new FileStream(this._path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.Asynchronous);
					Byte[] content = new Byte[checked((Int32)stream.Length)];
					await stream.ReadExactlyAsync(content, cancellationToken).ConfigureAwait(false);
					return new ReadOutcome(content, null);
				}
				catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
				{
					return new ReadOutcome(null, null);
				}
				catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
				{
					if (attempt >= attempts)
					{
						return new ReadOutcome(null, ex.Message);
					}

					await Task.Delay(TimeSpan.FromTicks(this._options.RetryDelay.Ticks << Math.Min(attempt - 1, 8)), cancellationToken).ConfigureAwait(false);
				}
			}
		}

		private Boolean IsKnown(FileState state)
		{
			lock (this._sync)
			{
				return this._known is not null && this._known == state;
			}
		}

		private void Settle(FileStamp stamp)
		{
			lock (this._sync)
			{
				this._lastStamp = stamp;
			}
		}

		private void Remember(FileState state, FileStamp stamp)
		{
			lock (this._sync)
			{
				this._known = state;
				this._lastStamp = stamp;
			}
		}

		private void Deliver(ProjectFileChange change)
		{
			lock (this._sync)
			{
				if (this._disposed)
				{
					return;
				}
			}

			String what = change.Kind switch
			{
				ProjectFileChangeKind.Modified => "changed",
				ProjectFileChangeKind.Invalid => "changed to something invalid",
				_ => "deleted",
			};
			this._logger?.ProjectFileChangedOnDisk(this._path, what);
			this._onChange(change);
		}

		/// <summary>What the editor knows of the file: whether it exists and the hash of its content.</summary>
		private sealed record FileState(Boolean Exists, String Hash);

		/// <summary>The outcome of reading the file: its content (null when it does not exist) or why it could not be read.</summary>
		private sealed record ReadOutcome(Byte[]? Content, String? Failure);

		/// <summary>Size and time of the file, for the safety net.</summary>
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
					return default;
				}
			}
		}
	}
}
