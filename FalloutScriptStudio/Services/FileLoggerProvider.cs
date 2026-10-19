namespace FalloutScriptStudio.Services
{
	using System;
	using System.Globalization;
	using System.IO;
	using Microsoft.Extensions.Logging;

	/// <summary>
	/// Writes log messages to a text file (the legacy <c>sse.log</c>). When the file grows over <see cref="DefaultMaxFileBytes"/> it is
	/// rotated: it becomes <c>sse.log.1</c> (replacing the previous copy) and a new file is started.
	/// </summary>
	public sealed class FileLoggerProvider : ILoggerProvider
	{
		/// <summary>The size over which the log file is rotated: 1 MB.</summary>
		public const Int64 DefaultMaxFileBytes = 1024 * 1024;

		private readonly Object _gate = new();
		private readonly String _path;
		private readonly Int64 _maxFileBytes;

		/// <summary>Initializes the provider; the file is created lazily.</summary>
		/// <param name="path">The log file.</param>
		/// <param name="maxFileBytes">The size over which the file is rotated before the next line is written.</param>
		public FileLoggerProvider(String path, Int64 maxFileBytes = DefaultMaxFileBytes)
		{
			ArgumentNullException.ThrowIfNull(path);
			this._path = path;
			this._maxFileBytes = maxFileBytes;
		}

		/// <summary>Gets the path the previous log file is moved to when the log is rotated.</summary>
		public String RotatedPath => this._path + ".1";

		/// <summary>Writes a line to the log file; failures to write are ignored.</summary>
		public void Write(String line)
		{
			lock (this._gate)
			{
				try
				{
					this.RotateIfNeeded();
					File.AppendAllText(this._path, DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + " " + line + Environment.NewLine);
				}
				catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
				{
					// The log must never take the application down.
				}
			}
		}

		public ILogger CreateLogger(String categoryName)
		{
			return new FileLogger(this, categoryName);
		}

		public void Dispose()
		{
		}

		private void RotateIfNeeded()
		{
			FileInfo file = new(this._path);
			if (file.Exists && file.Length > this._maxFileBytes)
			{
				File.Move(this._path, this.RotatedPath, true);
			}
		}

		private sealed class FileLogger : ILogger
		{
			private readonly FileLoggerProvider _provider;
			private readonly String _category;

			public FileLogger(FileLoggerProvider provider, String category)
			{
				this._provider = provider;
				this._category = category;
			}

			public IDisposable? BeginScope<TState>(TState state)
				where TState : notnull
			{
				return null;
			}

			public Boolean IsEnabled(LogLevel logLevel)
			{
				return logLevel >= LogLevel.Information;
			}

			public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, String> formatter)
			{
				if (!this.IsEnabled(logLevel))
				{
					return;
				}

				String text = "[" + logLevel + "] " + this._category + ": " + formatter(state, exception);
				if (exception is not null)
				{
					text += Environment.NewLine + exception;
				}

				this._provider.Write(text);
			}
		}
	}
}