namespace FalloutScriptStudio.Core.Building
{
	using System;
	using System.IO;

	/// <summary>
	/// The identity of a file as far as the build cache is concerned: its size and its last write time. Two stamps are equal only when both
	/// match exactly, so a file that was replaced by an older one (a restore from a backup) is a change as well.
	/// </summary>
	/// <param name="Length">The size in bytes, or -1 when the file does not exist.</param>
	/// <param name="UtcTicks">The last write time as UTC ticks, or 0 when the file does not exist.</param>
	public readonly record struct FileStamp(Int64 Length, Int64 UtcTicks)
	{
		/// <summary>Gets the stamp of a file that does not exist.</summary>
		public static FileStamp Missing => new FileStamp(-1, 0);

		/// <summary>Gets a value indicating whether the file existed when the stamp was read.</summary>
		public Boolean Exists => this.Length >= 0;

		/// <summary>Reads the stamp of a file.</summary>
		/// <param name="path">The full path of the file.</param>
		/// <returns>The stamp; <see cref="Missing"/> when the file does not exist or cannot be examined.</returns>
		public static FileStamp Read(String path)
		{
			try
			{
				FileInfo info = new FileInfo(path);
				if (!info.Exists)
				{
					return Missing;
				}

				return new FileStamp(info.Length, info.LastWriteTimeUtc.Ticks);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
			{
				return Missing;
			}
		}

		/// <inheritdoc/>
		public override String ToString()
		{
			return this.Exists ? this.Length + " bytes, " + new DateTime(this.UtcTicks, DateTimeKind.Utc).ToString("O", System.Globalization.CultureInfo.InvariantCulture) : "missing";
		}
	}
}