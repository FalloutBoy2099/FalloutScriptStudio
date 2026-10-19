namespace FalloutScriptStudio.ViewModels.ToolWindows
{
	using System;

	/// <summary>
	/// One entry of a drop-down list: a value and the text that represents it.
	/// </summary>
	/// <typeparam name="T">The type of the value.</typeparam>
	/// <param name="Value">The value.</param>
	/// <param name="Text">The text shown to the user.</param>
	public sealed record Choice<T>(T Value, String Text);
}