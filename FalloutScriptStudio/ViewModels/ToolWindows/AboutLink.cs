namespace FalloutScriptStudio.ViewModels.ToolWindows
{
	using System;

	/// <summary>
	/// A web link shown in the About window.
	/// </summary>
	/// <param name="Title">The text of the link.</param>
	/// <param name="Url">The address.</param>
	public sealed record AboutLink(String Title, Uri Url)
	{
		/// <summary>Gets the text a screen reader announces for the link (the automation name of a list item is the text of its object).</summary>
		/// <returns>The text of the link.</returns>
		public override String ToString()
		{
			return this.Title;
		}
	}
}