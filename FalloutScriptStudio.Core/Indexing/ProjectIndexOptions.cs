namespace FalloutScriptStudio.Core.Indexing
{
	using System;
	using System.Collections.Generic;
	using System.Text;
	using FalloutScriptStudio.Core.IO;

	/// <summary>How the project index reads files and how much it keeps.</summary>
	public sealed record ProjectIndexOptions
	{
		/// <summary>The default largest file that is indexed, in bytes.</summary>
		public const Int64 DefaultMaxFileBytes = 5L * 1024 * 1024;

		/// <summary>The default largest number of symbols the index keeps (about 120 bytes each).</summary>
		public const Int32 DefaultMaxSymbols = 300_000;

		/// <summary>Gets the code page used for files that are neither Unicode nor valid UTF-8.</summary>
		public Encoding Encoding { get; init; } = TextEncodings.Ansi;

		/// <summary>Gets the size above which a file is not indexed.</summary>
		public Int64 MaxFileBytes { get; init; } = DefaultMaxFileBytes;

		/// <summary>Gets the number of symbols above which the remaining files are not indexed (the memory bound).</summary>
		public Int32 MaxSymbols { get; init; } = DefaultMaxSymbols;

		/// <summary>Gets the number of files read and scanned at the same time.</summary>
		public Int32 MaxDegreeOfParallelism { get; init; } = Math.Clamp(Environment.ProcessorCount / 2, 1, 4);

		/// <summary>Gets the extensions of the files that are indexed (with the dot).</summary>
		public IReadOnlyList<String> Extensions { get; init; } = new[] { ".ssl", ".h" };
	}
}
