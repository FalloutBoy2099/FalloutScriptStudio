namespace FalloutScriptStudio.Core.Archives
{
	/// <summary>
	/// What went wrong when an archive was opened or one of its files was read.
	/// </summary>
	public enum DatErrorKind
	{
		/// <summary>An error without a dedicated kind.</summary>
		Unknown = 0,

		/// <summary>The file or folder name is missing.</summary>
		InvalidFileName,

		/// <summary>The archive does not exist.</summary>
		FileNotFound,

		/// <summary>The archive cannot be read (access denied, sharing violation...).</summary>
		Io,

		/// <summary>The file has no content.</summary>
		EmptyFile,

		/// <summary>The file is not a DAT archive.</summary>
		NotADat,

		/// <summary>The archive is a kind this reader does not support (Arcanum).</summary>
		UnsupportedFormat,

		/// <summary>The end of the file is missing: the archive is incomplete.</summary>
		TruncatedFile,

		/// <summary>The list of files of the archive is damaged.</summary>
		InvalidTree,

		/// <summary>The data of a file lies outside of the archive.</summary>
		OutsideArchive,

		/// <summary>The data of a file cannot be decompressed.</summary>
		CorruptData,

		/// <summary>A path of the archive would leave the destination folder when it is extracted.</summary>
		PathNotAllowed,
	}
}
