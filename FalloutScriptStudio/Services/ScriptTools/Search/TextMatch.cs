namespace FalloutScriptStudio.Services.ScriptTools.Search
{
	using System;

	/// <summary>
	/// A match inside a whole text.
	/// </summary>
	/// <param name="Offset">The 0-based position in the text.</param>
	/// <param name="Length">The length of the match.</param>
	/// <param name="Line">The 1-based line number.</param>
	/// <param name="Column">The 1-based column.</param>
	public readonly record struct TextMatch(Int32 Offset, Int32 Length, Int32 Line, Int32 Column);
}