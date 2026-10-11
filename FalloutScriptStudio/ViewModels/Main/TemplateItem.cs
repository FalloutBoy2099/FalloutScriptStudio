namespace FalloutScriptStudio.ViewModels.Main
{
	using System;

	/// <summary>A script template offered by the New menu.</summary>
	/// <param name="Name">The displayed name (the file name without extension).</param>
	/// <param name="Path">The full path of the template file.</param>
	public sealed record TemplateItem(String Name, String Path);
}
