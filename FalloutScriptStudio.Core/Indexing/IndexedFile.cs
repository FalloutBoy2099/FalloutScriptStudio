namespace FalloutScriptStudio.Core.Indexing
{
	using System;

	/// <summary>What the index remembers about one file: its time stamp and the symbols and includes found in it (never its text).</summary>
	internal sealed class IndexedFile
	{
		internal IndexedFile(String path, Int64 length, Int64 lastWriteTicks, Int64 indexedAtTicks, IndexedSymbol[] symbols, String[] includes)
		{
			this.Path = path;
			this.Length = length;
			this.LastWriteTicks = lastWriteTicks;
			this.IndexedAtTicks = indexedAtTicks;
			this.Symbols = symbols;
			this.Includes = includes;
		}

		/// <summary>Gets the full path.</summary>
		internal String Path { get; }

		/// <summary>Gets the size when the file was read.</summary>
		internal Int64 Length { get; }

		/// <summary>Gets the last write time (UTC ticks) when the file was read.</summary>
		internal Int64 LastWriteTicks { get; }

		/// <summary>Gets the time (UTC ticks) when the file was read.</summary>
		internal Int64 IndexedAtTicks { get; }

		/// <summary>Gets the symbols of the file.</summary>
		internal IndexedSymbol[] Symbols { get; }

		/// <summary>Gets the paths of the <c>#include</c> directives as written.</summary>
		internal String[] Includes { get; }
	}
}