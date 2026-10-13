namespace FalloutScriptStudio.Core.Analysis
{
	using System;

	/// <summary>A place in a file where a name is written.</summary>
	/// <param name="File">The full path of the file.</param>
	/// <param name="Offset">The offset of the name in the text that was scanned.</param>
	/// <param name="Line">The 1-based line.</param>
	/// <param name="Column">The 1-based column.</param>
	/// <param name="Length">The length of the name.</param>
	/// <param name="Kind">What the occurrence is.</param>
	public sealed record SymbolOccurrence(String File, Int32 Offset, Int32 Line, Int32 Column, Int32 Length, SymbolOccurrenceKind Kind);
}
