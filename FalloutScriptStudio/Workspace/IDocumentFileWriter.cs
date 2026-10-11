namespace FalloutScriptStudio.Workspace
{
	using System;
	using System.Threading.Tasks;

	/// <summary>Puts the bytes of a document on the disk (a seam so that the write can be slowed down or made to fail in tests).</summary>
	public interface IDocumentFileWriter
	{
		/// <summary>Replaces the content of a file; the file keeps its previous content when the write fails.</summary>
		/// <param name="path">The file.</param>
		/// <param name="content">The new content.</param>
		/// <returns>A task that completes when the file holds the new content.</returns>
		/// <exception cref="System.IO.IOException">The file cannot be written.</exception>
		/// <exception cref="UnauthorizedAccessException">The file is read-only or access is denied.</exception>
		Task WriteAsync(String path, Byte[] content);
	}
}
