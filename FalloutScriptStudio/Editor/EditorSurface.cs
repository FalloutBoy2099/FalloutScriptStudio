namespace FalloutScriptStudio.Editor
{
	using System;
	using ICSharpCode.AvalonEdit;
	using ICSharpCode.AvalonEdit.Document;
	using ICSharpCode.AvalonEdit.Editing;
	using ICSharpCode.AvalonEdit.Rendering;
	using System.Collections.Generic;
	using Core.Parsing;
	using Core.TextTools;
	using Documents;
	using Text;

	/// <summary>
	/// The <see cref="IEditorSurface"/> of an editor session: what the rest of the application may ask of the text editor.
	/// While the document is split the commands, the caret and the selection are those of the pane that has the focus.
	/// </summary>
	public sealed class EditorSurface : IEditorSurface
	{
		private readonly EditorSession _session;

		public EditorSurface(EditorSession session)
		{
			this._session = session;
			session.CaretMoved += (_, _) => this.CaretMoved?.Invoke(this, EventArgs.Empty);
			session.CurrentProcedureChanged += (_, _) => this.CurrentProcedureChanged?.Invoke(this, EventArgs.Empty);
			session.ZoomChanged += (_, _) => this.ZoomChanged?.Invoke(this, EventArgs.Empty);
		}

		/// <inheritdoc />
		public event EventHandler? CaretMoved;

		/// <inheritdoc />
		public event EventHandler? ZoomChanged;

		/// <inheritdoc />
		public event EventHandler? CurrentProcedureChanged;

		private EditorPane Pane => this._session.ActivePane;

		private TextEditor Editor => this.Pane.Context.Editor;

		private TextDocument Document => this._session.Context.Document;

		/// <inheritdoc />
		public Int32 CaretOffset
		{
			get => this.Editor.TextArea.Caret.Offset;
			set => this.Pane.Navigation.GoToOffset(value, -1, false);
		}

		/// <inheritdoc />
		public Int32 CaretLine => this.Editor.TextArea.Caret.Line;

		/// <inheritdoc />
		public Int32 CaretColumn
		{
			get
			{
				// Behind the end of a line (virtual space, like the legacy AllowCaretBeyondEOL) the column counts on past the last character.
				Caret caret = this.Editor.TextArea.Caret;
				Int32 column = caret.Column;
				DocumentLine line = this.Document.GetLineByNumber(caret.Line);
				if (caret.Offset == line.EndOffset && this.Editor.TextArea.TextView.GetVisualLine(caret.Line) is VisualLine visual)
				{
					Int32 end = visual.GetVisualColumn(line.EndOffset - visual.FirstDocumentLine.Offset);
					column += Math.Max(0, caret.Position.VisualColumn - end);
				}

				return column;
			}
		}

		/// <inheritdoc />
		public Int32 SelectionStart => this.Editor.SelectionStart;

		/// <inheritdoc />
		public Int32 SelectionLength => this.Editor.SelectionLength;

		/// <inheritdoc />
		public String SelectedText => this.Editor.SelectedText;

		/// <inheritdoc />
		public Int32 ZoomPercent
		{
			get => this._session.Context.Services.ZoomPercent;
			set => this._session.Context.Services.ZoomPercent = value;
		}

		/// <inheritdoc />
		public Boolean CanUndo => this.Document.UndoStack.CanUndo;

		/// <inheritdoc />
		public Boolean CanRedo => this.Document.UndoStack.CanRedo;

		/// <inheritdoc />
		public Boolean CanNavigateBack => this.Pane.Navigation.CanGoBack;

		/// <inheritdoc />
		public Boolean CanNavigateForward => this.Pane.Navigation.CanGoForward;

		/// <inheritdoc />
		public ScriptProcedure? CurrentProcedure => this.Pane.Navigation.CurrentProcedure;

		/// <inheritdoc />
		public String WordAtCaret
		{
			get
			{
				if (this.Editor.SelectionLength > 0)
				{
					return this.Editor.SelectedText;
				}

				return WordFinder.GetWordNearCaret(this.Document, this.Editor.TextArea.Caret.Offset);
			}
		}

		/// <inheritdoc />
		public Int32 LineCount => this.Document.LineCount;

		/// <inheritdoc />
		public void FocusEditor()
		{
			this.Editor.TextArea.Focus();
		}

		/// <inheritdoc />
		public void Undo()
		{
			this.Editor.Undo();
		}

		/// <inheritdoc />
		public void Redo()
		{
			this.Editor.Redo();
		}

		/// <inheritdoc />
		public void Cut()
		{
			this.Editor.Cut();
		}

		/// <inheritdoc />
		public void Copy()
		{
			this.Editor.Copy();
		}

		/// <inheritdoc />
		public void Paste()
		{
			this.Editor.Paste();
		}

		/// <inheritdoc />
		public void SelectAll()
		{
			this.Editor.SelectAll();
		}

		/// <inheritdoc />
		public void Select(Int32 offset, Int32 length)
		{
			this.Pane.Navigation.GoToOffset(offset, length, true, false);
		}

		/// <inheritdoc />
		public void GoTo(Int32 line, Int32 column = -1, Int32 selectLength = -1, Boolean center = true)
		{
			this.Pane.Navigation.GoTo(line, column, selectLength, center);
		}

		/// <inheritdoc />
		public void ReplaceSelection(String text)
		{
			this.Editor.TextArea.Selection.ReplaceSelectionWithText(text);
		}

		/// <inheritdoc />
		public void InsertText(String text)
		{
			this.Editor.TextArea.Selection.ReplaceSelectionWithText(text);
		}

		/// <inheritdoc />
		public Boolean CanExecute(EditorCommand command)
		{
			return this.Pane.Commands.CanExecute(command);
		}

		/// <inheritdoc />
		public void Execute(EditorCommand command)
		{
			this.Pane.Commands.Execute(command);
		}

		/// <inheritdoc />
		public void NavigateBack()
		{
			this.Pane.Navigation.GoBack();
		}

		/// <inheritdoc />
		public void NavigateForward()
		{
			this.Pane.Navigation.GoForward();
		}

		/// <inheritdoc />
		public void SetSearchMatches(IReadOnlyList<TextSpan> matches)
		{
			this._session.Occurrences.SetSearchMatches(matches);
		}

		/// <inheritdoc />
		public void ClearSearchMatches()
		{
			this._session.Occurrences.ClearSearchMatches();
		}

		/// <inheritdoc />
		public void HighlightInsertedText(Int32 offset, Int32 length)
		{
			this._session.Occurrences.HighlightInserted(offset, length);
		}

		/// <inheritdoc />
		public void CommitViewState()
		{
			this._session.CommitViewState();
		}
	}
}