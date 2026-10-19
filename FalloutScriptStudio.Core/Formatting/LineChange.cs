namespace FalloutScriptStudio.Core.Formatting
{
	using System;

	/// <summary>A group of lines that differs between two texts: some lines of the original were replaced by some lines of the new text.</summary>
	/// <param name="OldStart">The zero-based number of the first line of the original that changed (where the new lines go for a pure insertion).</param>
	/// <param name="OldCount">The number of lines of the original that were removed.</param>
	/// <param name="NewStart">The zero-based number of the first line of the new text.</param>
	/// <param name="NewCount">The number of lines of the new text that were added.</param>
	public readonly record struct LineChange(Int32 OldStart, Int32 OldCount, Int32 NewStart, Int32 NewCount);
}