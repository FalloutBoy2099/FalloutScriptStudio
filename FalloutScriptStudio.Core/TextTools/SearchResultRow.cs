namespace FalloutScriptStudio.Core.TextTools
{
	using System;

	/// <summary>
	/// One row of the "find all" result list: the first match on a line.
	/// </summary>
	/// <param name="File">The file (or script name) searched.</param>
	/// <param name="Line">1-based line number.</param>
	/// <param name="Column">1-based column of the match.</param>
	/// <param name="Length">Length of the match.</param>
	/// <param name="LineText">The trimmed text of the line.</param>
	public sealed record SearchResultRow(String File, Int32 Line, Int32 Column, Int32 Length, String LineText);
}