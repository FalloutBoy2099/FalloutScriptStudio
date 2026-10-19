namespace FalloutScriptStudio.Tests.Support
{
	using System;
	using System.Windows;
	using System.Windows.Input;

	/// <summary>
	/// A keyboard device whose key states are the ones a test chose: key events raised with it do not depend on the keys the developer holds
	/// down while the tests run (the real <see cref="Keyboard.PrimaryDevice"/> reads the state of the keyboard of the machine).
	/// </summary>
	internal sealed class FakeKeyboardDevice : KeyboardDevice
	{
		private readonly ModifierKeys _modifiers;

		/// <summary>Initializes a new instance of the <see cref="FakeKeyboardDevice"/> class.</summary>
		/// <param name="modifiers">The modifier keys that are down; none by default.</param>
		public FakeKeyboardDevice(ModifierKeys modifiers = ModifierKeys.None)
			: base(InputManager.Current)
		{
			this._modifiers = modifiers;
		}

		/// <inheritdoc />
		public override IInputElement? Target => null;

		/// <inheritdoc />
		public override PresentationSource? ActiveSource => null;

		/// <inheritdoc />
		protected override KeyStates GetKeyStatesFromSystem(Key key)
		{
			Boolean down = key switch
			{
				Key.LeftCtrl or Key.RightCtrl => (this._modifiers & ModifierKeys.Control) != 0,
				Key.LeftAlt or Key.RightAlt => (this._modifiers & ModifierKeys.Alt) != 0,
				Key.LeftShift or Key.RightShift => (this._modifiers & ModifierKeys.Shift) != 0,
				Key.LWin or Key.RWin => (this._modifiers & ModifierKeys.Windows) != 0,
				_ => false,
			};
			return down ? KeyStates.Down : KeyStates.None;
		}

		/// <summary>Creates a key down event for an element, raised with this device.</summary>
		/// <param name="source">The presentation source of the element.</param>
		/// <param name="key">The key.</param>
		/// <returns>The event arguments, with the bubbling <see cref="Keyboard.KeyDownEvent"/>.</returns>
		public KeyEventArgs CreateKeyDown(PresentationSource source, Key key)
		{
			return new KeyEventArgs(this, source, 0, key) { RoutedEvent = Keyboard.KeyDownEvent };
		}
	}
}