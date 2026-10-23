namespace FalloutScriptStudio.ViewModels.ToolWindows
{
	using System;

	/// <summary>
	/// A component or a person credited in the About window.
	/// </summary>
	/// <param name="Name">The name of the component.</param>
	/// <param name="Description">What it is used for and who owns it.</param>
	public sealed record AboutCredit(String Name, String Description)
	{
		/// <summary>Gets the text a screen reader announces for the credit (the automation name of a list item is the text of its object).</summary>
		/// <returns>The name and the description.</returns>
		public override String ToString()
		{
			return this.Name + ": " + this.Description;
		}
	}
}