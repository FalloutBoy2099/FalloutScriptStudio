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
	}
}
