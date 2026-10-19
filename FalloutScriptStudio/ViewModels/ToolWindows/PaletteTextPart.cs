namespace FalloutScriptStudio.ViewModels.ToolWindows
{
	using System;

	/// <summary>A piece of a text shown by the command palette: the part of a name that the filter matched is emphasized.</summary>
	/// <param name="Text">The characters of the piece.</param>
	/// <param name="IsMatch">Whether the filter matched these characters.</param>
	public sealed record PaletteTextPart(String Text, Boolean IsMatch);
}