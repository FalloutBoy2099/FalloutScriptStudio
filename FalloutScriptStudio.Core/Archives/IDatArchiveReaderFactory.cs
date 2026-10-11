namespace FalloutScriptStudio.Core.Archives
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>
	/// Opens an archive file or a folder that replaces one.
	/// </summary>
	public interface IDatArchiveReaderFactory
	{
		/// <summary>
		/// Opens an archive. A folder is opened like an archive of the files it contains.
		/// </summary>
		/// <param name="path">The <c>.dat</c> file or the folder.</param>
		/// <returns>The reader.</returns>
		/// <exception cref="DatException">The path is not an archive that can be read.</exception>
		IDatArchiveReader Open(String path);

		/// <summary>
		/// Opens an archive without blocking the caller while its directory is read.
		/// </summary>
		/// <param name="path">The <c>.dat</c> file or the folder.</param>
		/// <param name="cancellationToken">Cancels the operation.</param>
		/// <returns>The reader.</returns>
		/// <exception cref="DatException">The path is not an archive that can be read.</exception>
		/// <exception cref="OperationCanceledException">The operation was cancelled.</exception>
		Task<IDatArchiveReader> OpenAsync(String path, CancellationToken cancellationToken = default);
	}
}
