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

		/// <summary>Opens a file in the editor (at the line of the problem).</summary>
		OpenFile = 3,

		/// <summary>Opens again the documents a project had open when it was left.</summary>
		RestoreDocuments = 4,

		/// <summary>Removes the declaration of an unused variable from its script.</summary>
		RemoveUnusedVariable = 5,
	}
}
