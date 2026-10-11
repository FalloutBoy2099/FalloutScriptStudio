namespace FalloutScriptStudio.Editor.Highlighting
{
	using System;

	/// <summary>
	/// Visual style of one syntax token category (colors are WPF color strings: names or <c>#RRGGBB</c>).
	/// </summary>
	public sealed record TokenStyle(String? Foreground = null, String? Background = null, Boolean Bold = false, Boolean Italic = false);
}
