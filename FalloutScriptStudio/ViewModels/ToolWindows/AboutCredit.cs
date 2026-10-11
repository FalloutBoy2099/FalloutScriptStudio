namespace FalloutScriptStudio.ViewModels.ToolWindows
{
	using System;

	/// <summary>
	/// A component or a person credited in the About window.
	/// </summary>
	/// <param name="Name">The name of the component.</param>
	/// <param name="Description">What it is used for and who owns it.</param>
	public sealed record AboutCredit(String Name, String Description);
}
