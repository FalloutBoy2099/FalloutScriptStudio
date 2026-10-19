namespace FalloutScriptStudio.Workspace
{
	using System;
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;
	using Core.Configuration;

	/// <summary>
	/// Writes a document through a temporary file that replaces the target, so a failure never leaves a truncated script behind.
	/// A read-only target is refused (as the plain write did) and the other attributes of the target (hidden, archive...) are kept.
	/// </summary>
	public sealed class AtomicDocumentFileWriter : IDocumentFileWriter
	{
		/// <inheritdoc/>
		public async Task WriteAsync(String path, Byte[] content)
		{
			ArgumentException.ThrowIfNullOrEmpty(path);
			ArgumentNullException.ThrowIfNull(content);

			String? directory = Path.GetDirectoryName(Path.GetFullPath(path));
			if (!String.IsNullOrEmpty(directory) && !Directory.Exists(directory))
			{
				// Saving must not silently re-create a folder that was deleted or renamed.
				throw new DirectoryNotFoundException(directory);
			}

			FileAttributes? previous = null;
			if (File.Exists(path))
			{
				previous = File.GetAttributes(path);
				if ((previous.Value & FileAttributes.ReadOnly) != 0)
				{
					throw new UnauthorizedAccessException(path);
				}
			}

			await AtomicFile.WriteAllBytesAsync(path, content, CancellationToken.None);
			if (previous is { } attributes && attributes != FileAttributes.Normal)
			{
				File.SetAttributes(path, attributes);
			}
		}
	}
}