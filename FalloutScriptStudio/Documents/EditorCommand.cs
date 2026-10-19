namespace FalloutScriptStudio.Documents
{
	/// <summary>
	/// Text commands the main window (menus, toolbar, shortcuts) can run on the active editor through
	/// <see cref="IEditorSurface.Execute"/>.
	/// </summary>
	public enum EditorCommand
	{
		CommentSelection,
		UncommentSelection,
		ToggleBlockComment,
		AlignSelectionLeft,
		DecreaseIndent,
		IncreaseIndent,
		FormatDocument,
		FormatSelection,
		CapitalizeWord,
		UpperCase,
		LowerCase,
		ConvertLeadingTabsToSpaces,
		ConvertAllTabsToSpaces,
		TrimTrailingSpaces,
		ConvertHexDec,

		/// <summary>Collapse all procedures / expand all (outline toggle).</summary>
		ToggleOutline,
		CollapseAll,
		ExpandAll,

		/// <summary>Select and highlight every occurrence of the word under the caret.</summary>
		HighlightWord,

		GoToDeclaration,
		GoToDefinition,
		FindReferences,
		RenameSymbol,
		OpenInclude,

		/// <summary>Show the auto-completion list at the caret.</summary>
		ShowCompletion,

		/// <summary>Jump to the begin of the current procedure.</summary>
		GoToProcedureBegin,

		/// <summary>Delete the line(s) touched by the caret / selection.</summary>
		DeleteLine,

		/// <summary>Split the document view in two panes (toggle).</summary>
		SplitDocument,

		/// <summary>Comment the lines, or uncomment them when all are commented (the toolbar comment button).</summary>
		ToggleLineComment,

		ConvertLeadingSpacesToTabs,
		ConvertAllSpacesToTabs,

		/// <summary>Select the identifier at the caret.</summary>
		SelectWord,

		/// <summary>Delete the text from the caret to the end of the line (Ctrl+Shift+D).</summary>
		DeleteToLineEnd,

		/// <summary>Move the caret to the bracket that matches the one at the caret (Ctrl+B); again to go back.</summary>
		GoToMatchingBracket,

		/// <summary>Collapse every procedure except the one that contains the caret and expand that one ("only procedure" mode; no routed command).</summary>
		ShowOnlyCurrentProcedure,

		/// <summary>Move the selected statements into a new procedure and replace them by a call (Ctrl+Shift+E).</summary>
		ExtractProcedure,

		/// <summary>Delete the procedure that contains the caret when nothing uses it.</summary>
		DeleteUnusedProcedure,

		/// <summary>List the variables of the script that are declared but never read or never used in the list of problems.</summary>
		FindUnusedVariables,

		/// <summary>Remove the declaration of the unused variable under the caret.</summary>
		RemoveUnusedVariable,

		/// <summary>Open the dialogue preview at the dialogue node (the procedure) that contains the caret (Ctrl+Shift+P).</summary>
		PreviewDialogueFromNode,

		/// <summary>Apply the quick fix of the finding of the semantic analysis under the caret: the nearest name of a typo, a forward declaration, the removal of a duplicate (Ctrl+.).</summary>
		QuickFix,

		/// <summary>Choose a snippet in a list and insert it at the caret (Ctrl+J).</summary>
		InsertSnippet,

		/// <summary>Choose a snippet that uses the selected text and surround the selection with it (Ctrl+Shift+J).</summary>
		SurroundWith,
	}
}