namespace FalloutScriptStudio.Core.Dialogue
{
	using System;

	/// <summary>
	/// A piece of code the user saved to paste into a node (the "custom code" menu of the legacy node code editor).
	/// </summary>
	/// <param name="Name">The name shown in the menu.</param>
	/// <param name="Code">The code; it may span several lines.</param>
	public sealed record NodeCodeSnippet(String Name, String Code);
}
