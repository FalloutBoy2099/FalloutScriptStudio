namespace FalloutScriptStudio.ViewModels.ToolWindows
{
	using System;

	/// <summary>
	/// A web link shown in the About window.
	/// </summary>
	/// <param name="Title">The text of the link.</param>
	/// <param name="Url">The address.</param>
	public sealed record AboutLink(String Title, Uri Url);
}
