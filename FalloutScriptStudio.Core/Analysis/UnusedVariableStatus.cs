namespace FalloutScriptStudio.Core.Analysis
{
	/// <summary>How a declared variable is used.</summary>
	public enum UnusedVariableStatus
	{
		/// <summary>The variable is read somewhere.</summary>
		Used = 0,

		/// <summary>The variable is assigned but its value is never read.</summary>
		WriteOnly = 1,

		/// <summary>The variable is not named anywhere except in its declaration.</summary>
		Unused = 2,
	}
}