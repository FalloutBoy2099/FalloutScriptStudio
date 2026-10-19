namespace FalloutScriptStudio.Core.Configuration
{
	using System;
	using System.IO;
	using System.Text.RegularExpressions;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>
	/// Writes files so that a crash or a full disk never leaves a half-written file behind: the content goes to a
	/// temporary file in the same folder which then replaces the target.
	/// </summary>
	public static class AtomicFile
	{
		/// <summary>The temporary files are named <c>target.&lt;32 hex digits&gt;.tmp</c>.</summary>
		private static readonly Regex _temporaryName = new Regex(@"\.[0-9a-f]{32}\.tmp$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

		public static void WriteAllBytes(String path, Byte[] content)
		{
			String temporary = PrepareTemporaryPath(path);
			Boolean moved = false;
			try
			{
				using (FileStream stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
				{
					stream.Write(content, 0, content.Length);

					// The data must be on the disk before the move makes it the real file: after a power loss the target would otherwise be empty.
					stream.Flush(true);
				}

				File.Move(temporary, path, true);
				moved = true;
			}
			finally
			{
				if (!moved)
				{
					TryDelete(temporary);
				}
			}
		}

		public static async Task WriteAllBytesAsync(String path, Byte[] content, CancellationToken cancellationToken)
		{
			String temporary = PrepareTemporaryPath(path);
			Boolean moved = false;
			try
			{
				await using (FileStream stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
				{
					await stream.WriteAsync(content, cancellationToken).ConfigureAwait(false);
					await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
					stream.Flush(true);
				}

				File.Move(temporary, path, true);
				moved = true;
			}
			finally
			{
				if (!moved)
				{
					TryDelete(temporary);
				}
			}
		}

		/// <summary>
		/// Deletes the temporary files an interrupted write left in a folder (a crash between the creation of the temporary file and
		/// the move). A recent file may belong to a write of another instance that is still running, so it is kept.
		/// </summary>
		/// <param name="directory">The folder with the files.</param>
		/// <param name="minimumAge">How long ago a temporary file must have been written to be considered abandoned.</param>
		public static void DeleteOrphans(String directory, TimeSpan minimumAge)
		{
			try
			{
				if (!Directory.Exists(directory))
				{
					return;
				}

				DateTime limit = DateTime.UtcNow - minimumAge;
				foreach (String file in Directory.GetFiles(directory, "*.tmp"))
				{
					if (_temporaryName.IsMatch(file) && File.GetLastWriteTimeUtc(file) <= limit)
					{
						TryDelete(file);
					}
				}
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				// Garbage collection of garbage: nothing to report.
			}
		}

		private static String PrepareTemporaryPath(String path)
		{
			String? directory = Path.GetDirectoryName(Path.GetFullPath(path));
			if (!String.IsNullOrEmpty(directory))
			{
				Directory.CreateDirectory(directory);
			}

			return path + "." + Guid.NewGuid().ToString("N") + ".tmp";
		}

		private static void TryDelete(String path)
		{
			try
			{
				File.Delete(path);
			}
			catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
			{
				// The temporary file is garbage anyway; the original error is more important.
			}
		}
	}
}