namespace FalloutScriptStudio.Core.Archives
{
	using System;
	using System.Collections.Generic;
	using System.IO;

	/// <summary>
	/// Read access to the files of a Fallout <c>.dat</c> archive (or of a folder that replaces one).
	/// Only the directory of the archive is held in memory; the data of a file is streamed when it is opened.
	/// Paths inside the archive use backslashes, but slashes are accepted, and lookups ignore the case.
	/// </summary>
	public interface IDatArchiveReader
	{
		/// <summary>Gets the archive file, or the folder that stands in for it.</summary>
		String FilePath { get; }

		/// <summary>Gets the kind of source.</summary>
		DatFormat Format { get; }

		/// <summary>Gets all the files.</summary>
		IReadOnlyList<DatEntry> Entries { get; }

		/// <summary>
		/// Finds a file.
		/// </summary>
		/// <param name="path">The path inside the archive, for example <c>scripts\test.int</c>.</param>
		/// <returns>The entry, or <see langword="null"/> when there is no such file.</returns>
		DatEntry? Find(String path);

		/// <summary>
		/// Gets the files located directly in a folder.
		/// </summary>
		/// <param name="directory">The folder inside the archive; empty for the root.</param>
		/// <returns>The files of the folder.</returns>
		IReadOnlyList<DatEntry> GetEntries(String directory);

		/// <summary>
		/// Opens the decompressed content of a file. The caller disposes the stream; every call opens an independent one.
		/// </summary>
		/// <param name="entry">A file of this archive.</param>
		/// <returns>A forward-only (seekable when the data is stored without compression) read-only stream.</returns>
		/// <exception cref="DatException">The data is outside of the archive, or corrupted (the latter may also surface while reading).</exception>
		/// <exception cref="IOException">The archive cannot be read.</exception>
		Stream OpenEntry(DatEntry entry);
	}
}