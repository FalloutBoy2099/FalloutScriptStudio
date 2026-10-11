namespace FalloutScriptStudio.Core.Configuration
{
	using System;
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>
	/// Writes files so that a crash or a full disk never leaves a half-written file behind: the content goes to a
	/// temporary file in the same folder which then replaces the target.
	/// </summary>
	internal static class AtomicFile
	{
		public static void WriteAllBytes(String path, Byte[] content)
		{
			String temporary = PrepareTemporaryPath(path);
			Boolean moved = false;
			try
			{
				File.WriteAllBytes(temporary, content);
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
				await File.WriteAllBytesAsync(temporary, content, cancellationToken).ConfigureAwait(false);
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
