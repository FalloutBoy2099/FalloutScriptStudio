namespace FalloutScriptStudio.Services.ToolWindows
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// Lists the editor fonts that ship in the fonts folder of the program.
	/// </summary>
	public interface IEditorFontCatalog
	{
		/// <summary>
		/// Gets the family names of the fonts in a folder, ordered by name. The position in this list (1-based) is the font index stored in the settings.
		/// </summary>
		/// <param name="fontsDirectory">The fonts folder.</param>
		/// <returns>The family names; empty when the folder does not exist.</returns>
		IReadOnlyList<String> GetFamilyNames(String fontsDirectory);
	}
}