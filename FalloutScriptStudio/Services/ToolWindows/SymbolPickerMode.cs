namespace FalloutScriptStudio.Services.ToolWindows
{
	/// <summary>How the symbol list is used.</summary>
	public enum SymbolPickerMode
	{
		/// <summary>"Go to symbol": the list is filtered with a text box while the user types.</summary>
		Search = 0,

		/// <summary>The user picks one of several places where a name is defined; there is no filter.</summary>
		Choose = 1,
	}
}