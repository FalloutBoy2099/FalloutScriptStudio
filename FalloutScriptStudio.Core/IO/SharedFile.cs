namespace FalloutScriptStudio.Core.IO
{
	using System;
	using System.IO;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>
	/// Reads files that other programs (or the editor itself, on another thread) may be writing, replacing or deleting at the same moment. <c>File.ReadAllBytes</c>
	/// and its relatives open the file with <see cref="FileShare.Read"/>: that fails while somebody has the file open for writing, and, worse, it makes the
	/// other side fail while the read lasts (the save of another editor, or the <c>File.Move</c> that replaces a file with its new version). The background
	/// readers of the sources (the index, the searches, the analysis of the includes) use these methods instead: they share the file with everybody.
	/// </summary>
	public static class SharedFile
	{
		private const Int32 BufferSize = 4096;

		/// <summary>Reads all the bytes of a file without keeping other programs from writing, replacing or deleting it.</summary>
		/// <param name="path">The file.</param>
		/// <returns>The content.</returns>
		public static Byte[] ReadAllBytes(String path)
		{
			using FileStream stream = Open(path, FileOptions.SequentialScan);
			Byte[] buffer = new Byte[checked((Int32)stream.Length)];
			Int32 total = 0;
			while (total < buffer.Length)
			{
				Int32 read = stream.Read(buffer, total, buffer.Length - total);
				if (read == 0)
				{
					// The file got shorter while it was read.
					Array.Resize(ref buffer, total);
					break;
				}

				total += read;
			}

			return buffer;
		}

		/// <summary>Reads all the bytes of a file without keeping other programs from writing, replacing or deleting it.</summary>
		/// <param name="path">The file.</param>
		/// <param name="cancellationToken">Cancels the read.</param>
		/// <returns>The content.</returns>
		public static async Task<Byte[]> ReadAllBytesAsync(String path, CancellationToken cancellationToken = default)
		{
			await using FileStream stream = Open(path, FileOptions.Asynchronous | FileOptions.SequentialScan);
			Byte[] buffer = new Byte[checked((Int32)stream.Length)];
			Int32 total = 0;
			while (total < buffer.Length)
			{
				Int32 read = await stream.ReadAsync(buffer.AsMemory(total), cancellationToken).ConfigureAwait(false);
				if (read == 0)
				{
					Array.Resize(ref buffer, total);
					break;
				}

				total += read;
			}

			return buffer;
		}

		/// <summary>Reads the text of a file without keeping other programs from writing, replacing or deleting it; a byte order mark wins over <paramref name="encoding"/>.</summary>
		/// <param name="path">The file.</param>
		/// <param name="encoding">The encoding used when the file has no byte order mark.</param>
		/// <returns>The text.</returns>
		public static String ReadAllText(String path, Encoding encoding)
		{
			ArgumentNullException.ThrowIfNull(encoding);
			using FileStream stream = Open(path, FileOptions.SequentialScan);
			using StreamReader reader = new(stream, encoding, true);
			return reader.ReadToEnd();
		}

		/// <summary>Reads the text of a file without keeping other programs from writing, replacing or deleting it; a byte order mark wins over <paramref name="encoding"/>.</summary>
		/// <param name="path">The file.</param>
		/// <param name="encoding">The encoding used when the file has no byte order mark.</param>
		/// <param name="cancellationToken">Cancels the read.</param>
		/// <returns>The text.</returns>
		public static async Task<String> ReadAllTextAsync(String path, Encoding encoding, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(encoding);
			await using FileStream stream = Open(path, FileOptions.Asynchronous | FileOptions.SequentialScan);
			using StreamReader reader = new(stream, encoding, true);
			return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
		}

		private static FileStream Open(String path, FileOptions options)
		{
			return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, BufferSize, options);
		}
	}
}