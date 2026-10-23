namespace FalloutScriptStudio.ViewModels.ToolWindows
{
	using System;
	using Wpf.Ui.Controls;

	/// <summary>
	/// An entry of the page list of the settings window.
	/// </summary>
	/// <param name="Page">The page.</param>
	/// <param name="Title">The caption.</param>
	/// <param name="Icon">The icon.</param>
	public sealed record SettingsPageItem(SettingsPage Page, String Title, SymbolRegular Icon)
	{
		/// <summary>Gets the text a screen reader announces for the entry (the automation name of a list item is the text of its object).</summary>
		/// <returns>The caption.</returns>
		public override String ToString()
		{
			return this.Title;
		}
	}
}