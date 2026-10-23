namespace FalloutScriptStudio.Core.Globals
{
	/// <summary>How a global variable is named in the script.</summary>
	public enum GlobalKeyKind
	{
		/// <summary>By a string (the name of an sfall global).</summary>
		Name = 0,

		/// <summary>By a number (the index of a game, map or local variable, or the number of an sfall global).</summary>
		Id = 1,

		/// <summary>By a name that could not be resolved to a string or a number (a macro of a header the project does not contain).</summary>
		Symbol = 2,
	}
}