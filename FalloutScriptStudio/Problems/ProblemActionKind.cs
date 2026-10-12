namespace FalloutScriptStudio.Problems
{
	/// <summary>What a <see cref="ProblemAction"/> does.</summary>
	public enum ProblemActionKind
	{
		/// <summary>Opens the settings window on one page.</summary>
		OpenSettings = 0,

		/// <summary>Opens a folder in Explorer.</summary>
		OpenFolder = 1,

		/// <summary>Makes a folder the active project.</summary>
		SwitchProject = 2,
	}
}
