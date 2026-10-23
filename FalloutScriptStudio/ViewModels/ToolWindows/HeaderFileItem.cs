namespace FalloutScriptStudio.ViewModels.ToolWindows
{
	using System;

	/// <summary>
	/// A header file offered by the headers picker.
	/// </summary>
	/// <param name="DisplayName">The file name shown in the list.</param>
	/// <param name="FullPath">The full path of the file.</param>
	/// <param name="RelativePath">The path of the file relative to the headers folder.</param>
	public sealed record HeaderFileItem(String DisplayName, String FullPath, String RelativePath)
	{
		/// <summary>Gets the text a screen reader announces for the file (the automation name of a list item is the text of its object).</summary>
		/// <returns>The path relative to the headers folder.</returns>
		public override String ToString()
		{
			return this.RelativePath;
		}
	}
}