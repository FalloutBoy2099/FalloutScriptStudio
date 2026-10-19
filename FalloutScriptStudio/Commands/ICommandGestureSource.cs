namespace FalloutScriptStudio.Commands
{
	using System;

	/// <summary>Tells the shortcut a command is bound to (the key bindings and the menu of the main window).</summary>
	public interface ICommandGestureSource
	{
		/// <summary>Gets the shortcut of a command.</summary>
		/// <param name="entry">The entry of the palette.</param>
		/// <returns>The shortcut as the menu shows it (<c>Ctrl+Shift+S</c>), or <see langword="null"/> when the command has none.</returns>
		String? GetGesture(CommandEntry entry);
	}
}