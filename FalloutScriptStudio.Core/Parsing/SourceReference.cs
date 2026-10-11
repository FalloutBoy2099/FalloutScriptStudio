namespace FalloutScriptStudio.Core.Parsing
{
	using System;
	using System.IO;

	/// <summary>
	/// A location inside a source file.
	/// </summary>
	/// <param name="File">Full path of the file, or <c>null</c> when unknown.</param>
	/// <param name="Line">1-based line number, or -1 when unknown.</param>
	public sealed record SourceReference(String? File, Int32 Line)
	{
		/// <summary>
		/// Creates a reference whose file name is normalized to a full path (legacy <c>Reference</c> behaviour).
		/// </summary>
		/// <param name="file">File path, may be <c>null</c> or empty.</param>
		/// <param name="line">1-based line number.</param>
		public static SourceReference Create(String? file, Int32 line)
		{
			if (String.IsNullOrEmpty(file))
			{
				return new SourceReference(file, line);
			}

			try
			{
				return new SourceReference(Path.GetFullPath(file), line);
			}
			catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
			{
				return new SourceReference(file, line);
			}
		}
	}
}
