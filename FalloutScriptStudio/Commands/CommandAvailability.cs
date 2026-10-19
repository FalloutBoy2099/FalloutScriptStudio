namespace FalloutScriptStudio.Commands
{
	/// <summary>Whether a command can run now and, when it cannot, why.</summary>
	public enum CommandAvailability
	{
		/// <summary>The command can run.</summary>
		Available,

		/// <summary>No document is open.</summary>
		NeedsDocument,

		/// <summary>The active document is not a script.</summary>
		NeedsScript,

		/// <summary>The active document is not a script or a header file.</summary>
		NeedsParsable,

		/// <summary>The active document is not a message file.</summary>
		NeedsMessage,

		/// <summary>There is nothing to undo.</summary>
		NothingToUndo,

		/// <summary>There is nothing to redo.</summary>
		NothingToRedo,

		/// <summary>There is no earlier caret position.</summary>
		NoPreviousPosition,

		/// <summary>There is no later caret position.</summary>
		NoNextPosition,

		/// <summary>No project is open.</summary>
		NeedsProject,

		/// <summary>The external decompiler is not selected.</summary>
		NeedsExternalDecompiler,

		/// <summary>The command cannot run at the moment (an operation is running, nothing to act on).</summary>
		NotNow,
	}
}