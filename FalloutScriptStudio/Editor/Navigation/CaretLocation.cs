namespace FalloutScriptStudio.Editor.Navigation
{
	using System;

	/// <summary>A caret position in the history: 1-based line and column.</summary>
	/// <param name="Line">The line.</param>
	/// <param name="Column">The column.</param>
	public readonly record struct CaretLocation(Int32 Line, Int32 Column);
}