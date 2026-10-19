namespace FalloutScriptStudio.Editor
{
	using System;
	using System.Windows.Input;

	/// <summary>The real keyboard and mouse state of the machine.</summary>
	public sealed class SystemInputState : IInputState
	{
		/// <inheritdoc />
		public ModifierKeys Modifiers => Keyboard.Modifiers;

		/// <inheritdoc />
		public Boolean IsLeftButtonPressed => Mouse.LeftButton == MouseButtonState.Pressed;
	}
}