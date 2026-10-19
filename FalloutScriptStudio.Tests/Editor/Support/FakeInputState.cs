namespace FalloutScriptStudio.Tests.Editor.Support
{
	using System;
	using System.Windows.Input;
	using FalloutScriptStudio.Editor;

	/// <summary>The modifier keys and mouse button state a test chose, instead of the ones of the machine the tests run on.</summary>
	public sealed class FakeInputState : IInputState
	{
		/// <summary>Gets or sets the modifier keys that are down; none by default.</summary>
		public ModifierKeys Modifiers { get; set; }

		/// <summary>Gets or sets a value indicating whether the left mouse button is down; not by default.</summary>
		public Boolean IsLeftButtonPressed { get; set; }
	}
}