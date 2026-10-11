namespace FalloutScriptStudio.Core.Archives
{
	/// <summary>
	/// The kinds of source a <see cref="IDatArchiveReader"/> reads from.
	/// </summary>
	public enum DatFormat
	{
		/// <summary>A Fallout 1 archive: big endian directory, LZSS compressed files.</summary>
		Fallout1 = 1,

		/// <summary>A Fallout 2 archive: footer and little endian directory at the end of the file, zlib compressed files.</summary>
		Fallout2 = 2,

		/// <summary>A plain folder used in place of an archive (the engine accepts a folder named like the <c>.dat</c> file).</summary>
		Folder = 3,
	}
}
