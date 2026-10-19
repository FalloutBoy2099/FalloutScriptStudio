namespace FalloutScriptStudio.Documents
{
	using System;
	using System.Collections.Generic;
	using Core.Parsing;
	using Core.TextTools;

	/// <summary>
	/// What the rest of the application (view models, panels, tool windows) may ask of the text editor control of
	/// one document. Implemented by the editor view and attached to <see cref="DocumentViewModel.Editor"/> when the
	/// tab's view is loaded; it is <see langword="null"/> while the tab was never shown.
	/// All line / column numbers are 1-based, offsets are 0-based.
	/// </summary>
	/// <remarks>
	/// Members may be ADDED to this interface by the editor implementation, never renamed or removed.
	/// </remarks>
	public interface IEditorSurface
	{
		/// <summary>Raised after the caret position changed.</summary>
		event EventHandler? CaretMoved;

		/// <summary>Raised after the user changed the zoom (font scale) of this editor.</summary>
		event EventHandler? ZoomChanged;

		Int32 CaretOffset { get; set; }

		Int32 CaretLine { get; }

		Int32 CaretColumn { get; }

		Int32 SelectionStart { get; }

		Int32 SelectionLength { get; }

		String SelectedText { get; }

		/// <summary>Gets the zoom factor in percent (100 = normal).</summary>
		Int32 ZoomPercent { get; set; }

		Boolean CanUndo { get; }

		Boolean CanRedo { get; }

		/// <summary>Gives the keyboard focus to the editor.</summary>
		void FocusEditor();

		void Undo();

		void Redo();

		void Cut();

		void Copy();

		void Paste();

		void SelectAll();

		/// <summary>Selects a range and scrolls it into view.</summary>
		void Select(Int32 offset, Int32 length);

		/// <summary>Moves the caret to a line (and optional column), scrolls it into view and optionally selects text.</summary>
		/// <param name="line">1-based line.</param>
		/// <param name="column">1-based column or -1 for the start of the line.</param>
		/// <param name="selectLength">Number of characters to select, or -1 / 0 for none.</param>
		/// <param name="center">Scroll so that the line is vertically centred when it was off screen.</param>
		void GoTo(Int32 line, Int32 column = -1, Int32 selectLength = -1, Boolean center = true);

		/// <summary>Replaces the current selection (or inserts at the caret) as a single undoable operation.</summary>
		void ReplaceSelection(String text);

		/// <summary>Inserts text at the caret as a single undoable operation and moves the caret after it.</summary>
		void InsertText(String text);

		/// <summary>Determines whether a text command can run now (selection, document kind, parse state…).</summary>
		Boolean CanExecute(EditorCommand command);

		/// <summary>Runs a text command as one undoable operation.</summary>
		void Execute(EditorCommand command);

		/// <summary>Navigates to the previous position of the caret history (Back).</summary>
		void NavigateBack();

		/// <summary>Navigates to the next position of the caret history (Forward).</summary>
		void NavigateForward();

		Boolean CanNavigateBack { get; }

		Boolean CanNavigateForward { get; }

		/// <summary>Raised when the procedure that contains the caret changed (see <see cref="CurrentProcedure"/>).</summary>
		event EventHandler? CurrentProcedureChanged;

		/// <summary>Gets the procedure whose body contains the caret, or <see langword="null"/>.</summary>
		ScriptProcedure? CurrentProcedure { get; }

		/// <summary>Gets the selected text or, when nothing is selected, the identifier at the caret (for "send to search").</summary>
		String WordAtCaret { get; }

		/// <summary>Gets the number of lines of the document.</summary>
		Int32 LineCount { get; }

		/// <summary>Marks the given ranges of the text as search matches (replaces the previous ones).</summary>
		void SetSearchMatches(IReadOnlyList<TextSpan> matches);

		/// <summary>Removes the search match marks.</summary>
		void ClearSearchMatches();

		/// <summary>
		/// Marks a range of the text as just inserted (a pasted <c>#include</c> line). The mark replaces the highlighted word
		/// and disappears with the next edit or highlight.
		/// </summary>
		/// <param name="offset">The offset of the range.</param>
		/// <param name="length">The length of the range.</param>
		void HighlightInsertedText(Int32 offset, Int32 length);

		/// <summary>
		/// Copies the caret, scroll position and folded regions into <see cref="DocumentViewModel.ViewState"/> and remembers
		/// the collapsed procedures of the script. Call it before a document is closed or the application exits.
		/// </summary>
		void CommitViewState();
	}
}