namespace FalloutScriptStudio.ViewModels.MessageEditor
{
	using System;

	/// <summary>
	/// Identifies a version of a file on disk; used to detect changes made by other programs.
	/// </summary>
	/// <param name="LastWriteUtc">The last write time in UTC.</param>
	/// <param name="Length">The length of the file in bytes.</param>
	public readonly record struct FileStamp(DateTime LastWriteUtc, Int64 Length);
}