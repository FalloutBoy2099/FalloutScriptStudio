namespace FalloutScriptStudio.Core.Configuration
{
	using System;

	/// <summary>
	/// How the project file of the active project is watched. The tests use short delays and the real file system.
	/// </summary>
	public sealed record ProjectFileWatchOptions
	{
		/// <summary>Gets the options of the application: a change is examined 300 ms after the last event, the file is checked every 2 seconds as a safety net.</summary>
		public static ProjectFileWatchOptions Default { get; } = new ProjectFileWatchOptions();

		/// <summary>Gets options that do not watch anything (the project file is only read when the project is opened or reloaded).</summary>
		public static ProjectFileWatchOptions Disabled { get; } = new ProjectFileWatchOptions { Enabled = false };

		/// <summary>Gets a value indicating whether the file is watched at all.</summary>
		public Boolean Enabled { get; init; } = true;

		/// <summary>Gets a value indicating whether the file system is asked to report changes; with <see langword="false"/> only the safety net (<see cref="PollInterval"/>) notices them.</summary>
		public Boolean UseFileSystemEvents { get; init; } = true;

		/// <summary>Gets the quiet time after the last file system event before the file is examined: editors write a file in several steps (temporary file, delete, rename).</summary>
		public TimeSpan Debounce { get; init; } = TimeSpan.FromMilliseconds(300);

		/// <summary>Gets the interval of the safety net that compares the size and the time of the file in case the file system did not report a change (network folders); zero turns it off.</summary>
		public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(2);

		/// <summary>Gets how many times a file that is locked by another program is read before the attempt is given up until the next check.</summary>
		public Int32 ReadAttempts { get; init; } = 5;

		/// <summary>Gets the first pause between two attempts; it doubles each time (a bounded backoff, never a polling loop).</summary>
		public TimeSpan RetryDelay { get; init; } = TimeSpan.FromMilliseconds(100);

		/// <summary>Gets how many times a content that is not valid (or a file that has disappeared) is looked at again before it is reported: it may be a write in progress.</summary>
		public Int32 SettleAttempts { get; init; } = 3;
	}
}