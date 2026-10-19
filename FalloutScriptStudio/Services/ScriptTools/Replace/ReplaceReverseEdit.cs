namespace FalloutScriptStudio.Services.ScriptTools.Replace
{
	using System;

	/// <summary>The edit that puts a replaced text back: in the coordinates of the text after the replacement.</summary>
	/// <param name="Offset">The offset of the replacement in the new text.</param>
	/// <param name="Length">The length of the replacement.</param>
	/// <param name="OldText">The text that was replaced.</param>
	public readonly record struct ReplaceReverseEdit(Int32 Offset, Int32 Length, String OldText);
}