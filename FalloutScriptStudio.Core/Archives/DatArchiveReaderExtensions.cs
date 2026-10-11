namespace FalloutScriptStudio.Core.Archives
{
	using System;
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>
	/// Convenience operations on an <see cref="IDatArchiveReader"/>.
	/// </summary>
	public static class DatArchiveReaderExtensions
	{
		private const Int32 CopyBufferSize = 81920;

		/// <summary>
		/// Reads the whole decompressed content of a file.
		/// </summary>
		/// <param name="reader">The archive.</param>
		/// <param name="entry">A file of the archive.</param>
		/// <param name="cancellationToken">Cancels the read.</param>
		/// <returns>The content.</returns>
		/// <exception cref="DatException">The data is damaged.</exception>
		/// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
		public static async Task<Byte[]> ReadAllBytesAsync(this IDatArchiveReader reader, DatEntry entry, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(reader);
			ArgumentNullException.ThrowIfNull(entry);

			using Stream source = reader.OpenEntry(entry);
			using MemoryStream result = new MemoryStream(Math.Max(0, entry.UnpackedSize));
			await source.CopyToAsync(result, CopyBufferSize, cancellationToken).ConfigureAwait(false);
			return result.ToArray();
		}

		/// <summary>
		/// Writes a file of the archive to disk. A partly written file is deleted when the extraction fails.
		/// </summary>
		/// <param name="reader">The archive.</param>
		/// <param name="entry">A file of the archive.</param>
		/// <param name="destinationPath">The file to create or overwrite; its folder must exist.</param>
		/// <param name="cancellationToken">Cancels the extraction.</param>
		/// <exception cref="DatException">The data is damaged.</exception>
		/// <exception cref="IOException">The destination cannot be written.</exception>
		/// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
		public static async Task ExtractToFileAsync(this IDatArchiveReader reader, DatEntry entry, String destinationPath, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(reader);
			ArgumentNullException.ThrowIfNull(entry);
			ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

			using Stream source = reader.OpenEntry(entry);
			Boolean completed = false;
			try
			{
				using (FileStream target = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, CopyBufferSize, FileOptions.Asynchronous))
				{
					await source.CopyToAsync(target, CopyBufferSize, cancellationToken).ConfigureAwait(false);
				}

				completed = true;
			}
			finally
			{
				if (!completed)
				{
					TryDelete(destinationPath);
				}
			}
		}

		private static void TryDelete(String path)
		{
			try
			{
				File.Delete(path);
			}
			catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
			{
				// Nothing more can be done about a leftover partial file.
			}
		}
	}
}
