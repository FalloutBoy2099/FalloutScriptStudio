namespace FalloutScriptStudio.Core.Archives
{
	using System;
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>
	/// <see cref="IDatArchiveReaderFactory"/> implementation: a folder is read as a <see cref="DatFolderReader"/>,
	/// a file as a <see cref="DatArchiveReader"/>.
	/// </summary>
	public sealed class DatArchiveReaderFactory : IDatArchiveReaderFactory
	{
		/// <inheritdoc/>
		public IDatArchiveReader Open(String path)
		{
			if (String.IsNullOrWhiteSpace(path))
			{
				throw new DatException(DatErrorKind.InvalidFileName, "The archive name is empty.");
			}

			return Directory.Exists(path) ? DatFolderReader.Open(path) : DatArchiveReader.Open(path);
		}

		/// <inheritdoc/>
		public Task<IDatArchiveReader> OpenAsync(String path, CancellationToken cancellationToken = default)
		{
			return Task.Run(() => this.Open(path), cancellationToken);
		}
	}
}
