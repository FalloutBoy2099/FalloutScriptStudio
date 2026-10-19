namespace FalloutScriptStudio.Commands
{
	/// <summary>What has to be true for a command to be enabled: the same conditions the menu items of the main window use.</summary>
	public enum CommandRequirement
	{
		/// <summary>The command is always enabled.</summary>
		None,

		/// <summary>A document is open.</summary>
		Document,

		/// <summary>The active document is a script.</summary>
		Script,

		/// <summary>The active document is a script or a header file.</summary>
		Parsable,

		/// <summary>The active document is a message file.</summary>
		Message,

		/// <summary>The active editor can undo.</summary>
		Undo,

		/// <summary>The active editor can redo.</summary>
		Redo,

		/// <summary>The editor can go back in the caret history.</summary>
		GoBack,

		/// <summary>The editor can go forward in the caret history.</summary>
		GoForward,

		/// <summary>A project is open.</summary>
		Project,

		/// <summary>The last Replace in Files can be undone.</summary>
		UndoReplaceInFiles,

		/// <summary>The external decompiler is the one in use (only it has an old version).</summary>
		OldDecompiler,
	}
}