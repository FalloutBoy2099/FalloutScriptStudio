namespace FalloutScriptStudio.Editor.Text
{
	using System;

	/// <summary>The offsets of two matching brackets.</summary>
	/// <param name="OpenOffset">Offset of the opening bracket.</param>
	/// <param name="CloseOffset">Offset of the closing bracket.</param>
	public readonly record struct BracketPair(Int32 OpenOffset, Int32 CloseOffset);
}
