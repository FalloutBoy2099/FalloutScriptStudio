namespace FalloutScriptStudio.Core.Configuration
{
	/// <summary>What the code formatter indents with.</summary>
	public enum IndentStyle
	{
		/// <summary>The setting of the editor (<see cref="EditorSettings.TabsToSpaces"/> and <see cref="EditorSettings.TabSize"/>).</summary>
		FromEditor = 0,

		/// <summary>Spaces.</summary>
		Spaces = 1,

		/// <summary>Tabs.</summary>
		Tabs = 2,
	}
}