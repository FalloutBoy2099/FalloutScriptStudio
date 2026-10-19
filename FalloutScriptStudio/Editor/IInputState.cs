namespace FalloutScriptStudio.Editor
{
	using System;
	using System.Windows.Input;

	/// <summary>
	/// The state of the keyboard modifiers and of the mouse button at the moment an editor handler runs. The editor reads it through this
	/// interface instead of <c>Keyboard.Modifiers</c> and <c>Mouse.LeftButton</c> so that tests do not depend on the keys the developer
	/// holds down while they run.
	/// </summary>
	public interface IInputState
	{
		/// <summary>Gets the modifier keys that are down.</summary>
		ModifierKeys Modifiers { get; }

		/// <summary>Gets a value indicating whether the left mouse button is down.</summary>
		Boolean IsLeftButtonPressed { get; }
	}
}