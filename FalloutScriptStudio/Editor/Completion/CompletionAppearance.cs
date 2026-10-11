namespace FalloutScriptStudio.Editor.Completion
{
	using System;
	using System.Windows.Media;

	/// <summary>How the completion list is drawn: colors by kind and the editor font.</summary>
	/// <param name="Palette">The colors of the current theme.</param>
	/// <param name="Colored">Whether the entries are colored by kind (setting <c>autocompleteColor</c>).</param>
	/// <param name="FontFamily">The editor font.</param>
	/// <param name="FontSize">The editor font size in device independent pixels.</param>
	public sealed record CompletionAppearance(EditorPalette Palette, Boolean Colored, FontFamily FontFamily, Double FontSize);
}
