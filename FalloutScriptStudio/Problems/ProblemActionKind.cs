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

		/// <summary>Applies the quick fix of a finding of the semantic analysis of a script (a rename, a forward declaration, the removal of a duplicate).</summary>
		ApplyQuickFix = 8,

		/// <summary>Creates (or finds) the message file of a script and associates it with the script.</summary>
		CreateMessageFile = 6,

		/// <summary>Appends the message numbers a script uses and its message file lacks to the message file.</summary>
		AddMissingMessages = 7,
	}
}