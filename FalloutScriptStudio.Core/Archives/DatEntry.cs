namespace FalloutScriptStudio.Core.Archives
{
	using System;

	/// <summary>
	/// A file stored in a DAT archive (or found in a folder that stands in for one).
	/// </summary>
	public sealed class DatEntry
	{
		internal DatEntry(String directory, String name)
		{
			this.Directory = directory;
			this.Name = name;
		}

		/// <summary>Gets the folder of the file, in the original case, ending with a backslash (empty for the root).</summary>
		public String Directory { get; }

		/// <summary>Gets the file name in the original case.</summary>
		public String Name { get; }

		/// <summary>Gets the folder and the name; the separator is a backslash.</summary>
		public String FullPath => this.Directory + this.Name;

		/// <summary>Gets the size of the file once decompressed.</summary>
		public Int32 UnpackedSize { get; internal init; }

		/// <summary>Gets the size of the stored data; it differs from <see cref="UnpackedSize"/> only for compressed entries.</summary>
		public Int32 PackedSize { get; internal init; }

		/// <summary>Gets a value indicating whether the stored data is compressed.</summary>
		public Boolean IsCompressed { get; internal init; }

		/// <summary>Gets the position of the stored data in the archive file.</summary>
		internal UInt32 Offset { get; init; }

		/// <summary>Gets the file on disk for an entry of a folder; <see langword="null"/> for an entry of an archive file.</summary>
		internal String? SourceFile { get; init; }

		/// <inheritdoc/>
		public override String ToString()
		{
			return this.FullPath;
		}
	}
}