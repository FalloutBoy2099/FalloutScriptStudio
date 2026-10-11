namespace FalloutScriptStudio.Tests.Shell.Support
{
	using System;
	using System.Collections.Generic;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Core.TextTools;
	using FalloutScriptStudio.Documents;
	using ICSharpCode.AvalonEdit.Document;

	/// <summary>An editor surface backed directly by the text document of a view model.</summary>
	internal sealed class FakeEditorSurface : IEditorSurface
	{
		private readonly TextDocument _document;

		public FakeEditorSurface(TextDocument document)
		{
			this._document = document;
		}

		public event EventHandler? CaretMoved;

		public event EventHandler? ZoomChanged;

		public event EventHandler? CurrentProcedureChanged;

		public Int32 CaretOffset { get; set; }

		public Int32 CaretLine => this._document.GetLineByOffset(Math.Min(this.CaretOffset, this._document.TextLength)).LineNumber;

		public Int32 CaretColumn => this.CaretOffset - this._document.GetLineByOffset(Math.Min(this.CaretOffset, this._document.TextLength)).Offset + 1;

		public Int32 SelectionStart { get; private set; }

		public Int32 SelectionLength { get; private set; }

		public String SelectedText => this.SelectionLength == 0 ? String.Empty : this._document.GetText(this.SelectionStart, this.SelectionLength);

		public Int32 ZoomPercent { get; set; } = 100;

		public Boolean CanUndo => this._document.UndoStack.CanUndo;

		public Boolean CanRedo => this._document.UndoStack.CanRedo;

		public Boolean CanNavigateBack { get; set; }

		public Boolean CanNavigateForward { get; set; }

		public ScriptProcedure? CurrentProcedure { get; set; }

		public String WordAtCaret { get; set; } = String.Empty;

		public Int32 LineCount => this._document.LineCount;

		public List<String> Calls { get; } = new List<String>();

		public List<TextSpan> Matches { get; } = new List<TextSpan>();

		public TextSpan? InsertedHighlight { get; private set; }

		public (Int32 Line, Int32 Column, Int32 Length)? LastGoTo { get; private set; }

		public void FocusEditor()
		{
			this.Calls.Add("focus");
		}

		public void Undo()
		{
			this._document.UndoStack.Undo();
		}

		public void Redo()
		{
			this._document.UndoStack.Redo();
		}

		public void Cut()
		{
			this.Calls.Add("cut");
		}

		public void Copy()
		{
			this.Calls.Add("copy");
		}

		public void Paste()
		{
			this.Calls.Add("paste");
		}

		public void SelectAll()
		{
			this.Select(0, this._document.TextLength);
		}

		public void Select(Int32 offset, Int32 length)
		{
			this.SelectionStart = offset;
			this.SelectionLength = length;
			this.CaretOffset = offset + length;
			this.CaretMoved?.Invoke(this, EventArgs.Empty);
		}

		public void GoTo(Int32 line, Int32 column = -1, Int32 selectLength = -1, Boolean center = true)
		{
			this.LastGoTo = (line, column, selectLength);
			this.CaretOffset = this._document.GetLineByNumber(Math.Min(line, this._document.LineCount)).Offset;
			this.CaretMoved?.Invoke(this, EventArgs.Empty);
		}

		public void ReplaceSelection(String text)
		{
			this._document.Replace(this.SelectionStart, this.SelectionLength, text);
			this.CaretOffset = this.SelectionStart + text.Length;
			this.SelectionLength = 0;
		}

		public void InsertText(String text)
		{
			this._document.Insert(this.CaretOffset, text);
			this.CaretOffset += text.Length;
		}

		public Boolean CanExecute(EditorCommand command)
		{
			return true;
		}

		public void Execute(EditorCommand command)
		{
			this.Calls.Add("execute:" + command);
		}

		public void NavigateBack()
		{
			this.Calls.Add("back");
		}

		public void NavigateForward()
		{
			this.Calls.Add("forward");
		}

		public void SetSearchMatches(IReadOnlyList<TextSpan> matches)
		{
			this.Matches.Clear();
			this.Matches.AddRange(matches);
		}

		public void ClearSearchMatches()
		{
			this.Matches.Clear();
		}

		public void HighlightInsertedText(Int32 offset, Int32 length)
		{
			this.InsertedHighlight = new TextSpan(offset, length);
		}

		public void CommitViewState()
		{
			this.Calls.Add("commit");
		}

		public void RaiseProcedureChanged()
		{
			this.CurrentProcedureChanged?.Invoke(this, EventArgs.Empty);
		}

		public void RaiseZoomChanged()
		{
			this.ZoomChanged?.Invoke(this, EventArgs.Empty);
		}
	}
}
