namespace FalloutScriptStudio.Editor.Controllers
{
	using System;
	using System.Linq;
	using System.Windows;
	using System.Windows.Input;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Editor.Navigation;
	using FalloutScriptStudio.Editor.Text;
	using FalloutScriptStudio.Localization;
	using ICSharpCode.AvalonEdit;
	using ICSharpCode.AvalonEdit.Document;
	using ICSharpCode.AvalonEdit.Editing;

	/// <summary>
	/// Caret navigation: the Back / Forward history, go to line, go to declaration / definition / references /
	/// include, rename, the current procedure and Ctrl+click.
	/// </summary>
	public sealed class NavigationController : IDisposable
	{
		private readonly EditorContext _context;
		private readonly FoldingController _folding;
		private Boolean _navigating;
		private Int32 _deferredLine = -1;
		private Boolean _deferredScroll;
		private ScriptProcedure? _currentProcedure;

		public NavigationController(EditorContext context, FoldingController folding)
		{
			this._context = context;
			this._folding = folding;
		}

		/// <summary>Raised after the caret moved.</summary>
		public event EventHandler? CaretMoved;

		/// <summary>Raised when the procedure that contains the caret changed.</summary>
		public event EventHandler? CurrentProcedureChanged;

		/// <summary>Gets the procedure that contains the caret, or <see langword="null"/>.</summary>
		public ScriptProcedure? CurrentProcedure => this._currentProcedure;

		public Boolean CanGoBack => this._context.IsScript && this._context.ViewState.History.CanGoBack;

		public Boolean CanGoForward => this._context.IsScript && this._context.ViewState.History.CanGoForward;

		public void Attach()
		{
			this._context.TextArea.Caret.PositionChanged += this.OnCaretPositionChanged;
			this._context.TextArea.PreviewMouseLeftButtonDown += this.OnPreviewMouseLeftButtonDown;
			this._context.TextArea.PreviewMouseMove += this.OnPreviewMouseMove;
			this._context.TextArea.PreviewKeyDown += this.OnModifierChanged;
			this._context.TextArea.PreviewKeyUp += this.OnModifierChanged;
			this.UpdateCurrentProcedure();
		}

		public void Dispose()
		{
			this._context.TextArea.Caret.PositionChanged -= this.OnCaretPositionChanged;
			this._context.TextArea.PreviewMouseLeftButtonDown -= this.OnPreviewMouseLeftButtonDown;
			this._context.TextArea.PreviewMouseMove -= this.OnPreviewMouseMove;
			this._context.TextArea.PreviewKeyDown -= this.OnModifierChanged;
			this._context.TextArea.PreviewKeyUp -= this.OnModifierChanged;
			this._context.TextView.Cursor = null;
		}

		/// <summary>Moves the caret to a line and column and optionally selects text.</summary>
		/// <param name="line">1-based line.</param>
		/// <param name="column">1-based column or -1 for the start of the line.</param>
		/// <param name="selectLength">Characters to select, or -1 / 0 for none.</param>
		/// <param name="center">Center the line vertically when it was off screen.</param>
		public void GoTo(Int32 line, Int32 column = -1, Int32 selectLength = -1, Boolean center = true)
		{
			TextDocument document = this._context.Document;
			DocumentLine target = document.GetLineByNumber(Math.Clamp(line, 1, Math.Max(document.LineCount, 1)));
			Int32 offset = target.Offset + (column <= 0 ? 0 : Math.Min(column - 1, target.Length));
			this.GoToOffset(offset, selectLength, center);
		}

		/// <summary>Moves the caret to an offset and optionally selects text.</summary>
		/// <param name="offset">The offset.</param>
		/// <param name="selectLength">Characters to select, or -1 / 0 for none.</param>
		/// <param name="center">Center the line vertically when it was off screen.</param>
		/// <param name="record">Whether the move creates an entry of the Back / Forward history (searches do not).</param>
		public void GoToOffset(Int32 offset, Int32 selectLength = -1, Boolean center = true, Boolean record = true)
		{
			Boolean wasNavigating = this._navigating;
			this._navigating = !record || wasNavigating;
			try
			{
				this.MoveTo(offset, selectLength, center);
			}
			finally
			{
				this._navigating = wasNavigating;
			}
		}

		private void MoveTo(Int32 offset, Int32 selectLength, Boolean center)
		{
			TextDocument document = this._context.Document;
			offset = Math.Clamp(offset, 0, document.TextLength);
			this._folding.Reveal(offset);

			TextArea area = this._context.TextArea;
			area.Caret.Offset = offset;
			if (selectLength > 0)
			{
				area.Selection = Selection.Create(area, offset, Math.Min(offset + selectLength, document.TextLength));
			}
			else
			{
				area.ClearSelection();
			}

			this.EnsureVisible(document.GetLineByOffset(offset).LineNumber, center);
		}

		/// <summary>Scrolls so that a line is visible, centering it when it is off screen (or when requested).</summary>
		/// <param name="line">1-based line.</param>
		/// <param name="forceCenter">Center even when the line is visible.</param>
		public void EnsureVisible(Int32 line, Boolean forceCenter)
		{
			ICSharpCode.AvalonEdit.Rendering.TextView view = this._context.TextView;
			if (view.ActualHeight <= 0)
			{
				this._context.TextArea.Caret.BringCaretToView();
				return;
			}

			Double top = view.GetVisualTopByDocumentLine(line);
			Double height = view.DefaultLineHeight;
			Boolean visible = top >= view.VerticalOffset && top + height <= view.VerticalOffset + view.ActualHeight;
			if (!visible || forceCenter)
			{
				this._context.Editor.ScrollToVerticalOffset(Math.Max(0, top - ((view.ActualHeight - height) / 2)));
			}

			this._context.TextArea.Caret.BringCaretToView();
		}

		/// <summary>Goes to the previous entry of the caret history.</summary>
		public void GoBack()
		{
			this.GoToHistory(this._context.ViewState.History.GoBack());
		}

		/// <summary>Goes to the next entry of the caret history.</summary>
		public void GoForward()
		{
			this.GoToHistory(this._context.ViewState.History.GoForward());
		}

		/// <summary>Finds the symbol at a position.</summary>
		/// <param name="offset">An offset in or next to the word.</param>
		/// <param name="word">Receives the word.</param>
		/// <returns>The symbol, or <see langword="null"/> when the word is unknown or the document has no parse result.</returns>
		public IScriptSymbol? FindSymbol(Int32 offset, out String word)
		{
			word = String.Empty;
			TextDocument document = this._context.Document;
			if (!this._context.IsScript || !WordFinder.TryGetWordRangeNearCaret(document, offset, out Int32 start, out Int32 length))
			{
				return null;
			}

			if (!this._context.Lexical.Current.IsCodeAt(start))
			{
				return null;
			}

			word = document.GetText(start, length);
			Int32 line = document.GetLineByOffset(start).LineNumber;
			return this._context.ProgramInfo?.Lookup(word, this._context.DocumentModel.FilePath ?? String.Empty, line);
		}

		/// <summary>Gets the include path of the <c>#include</c> directive on a line.</summary>
		/// <param name="line">The 1-based line.</param>
		/// <returns>The path as written, or <see langword="null"/> when the line is not an include directive.</returns>
		public String? GetIncludePath(Int32 line)
		{
			if (!this._context.IsScript || line < 1 || line > this._context.Document.LineCount)
			{
				return null;
			}

			String text = this._context.Document.GetText(this._context.Document.GetLineByNumber(line)).Trim();
			return text.StartsWith(SslKeywords.Include, StringComparison.OrdinalIgnoreCase) ? IncludeResolver.ExtractIncludePath(text) : null;
		}

		/// <summary>Goes to the declaration of the symbol at the caret.</summary>
		public void GoToDeclaration()
		{
			Int32 caret = this._context.TextArea.Caret.Offset;
			IScriptSymbol? symbol = this.FindSymbol(caret, out String word);
			if (symbol?.Declaration is SourceReference declaration && declaration.Line > 0)
			{
				this.GoToReference(declaration, word);
			}
		}

		/// <summary>Goes to the body of the procedure at the caret (or to the declaration of other symbols).</summary>
		public void GoToDefinition()
		{
			Int32 caret = this._context.TextArea.Caret.Offset;
			IScriptSymbol? symbol = this.FindSymbol(caret, out String word);
			if (symbol is null)
			{
				return;
			}

			SourceReference? definition = symbol.Kind == SymbolKind.Procedure ? this._context.ProgramInfo?.LookupDefinition(word) : null;
			SourceReference? target = definition ?? symbol.Declaration;
			if (target is not null && target.Line > 0)
			{
				this.GoToReference(target, word);
			}
		}

		/// <summary>Lists the references of the symbol at the caret.</summary>
		public void FindReferences()
		{
			IScriptSymbol? symbol = this.FindSymbol(this._context.TextArea.Caret.Offset, out String word);
			if (symbol is null)
			{
				return;
			}

			if (symbol.References.Count == 0)
			{
				this._context.Services.Workspace.ReportStatus(Loc.Format("Str.Editor.Status.NoReferences", word));
				return;
			}

			this._context.RunInBackground(() => this._context.Services.Workspace.ShowReferencesAsync(word, symbol.References));
		}

		/// <summary>Starts the rename refactoring for the symbol at the caret.</summary>
		public void Rename()
		{
			IScriptSymbol? symbol = this.FindSymbol(this._context.TextArea.Caret.Offset, out _);
			if (symbol is not null)
			{
				this._context.RunInBackground(() => this._context.Services.Workspace.RenameSymbolAsync(this._context.DocumentModel, symbol));
			}
		}

		/// <summary>Opens the file of the <c>#include</c> directive on the caret line.</summary>
		public void OpenInclude()
		{
			this.OpenIncludeOnLine(this._context.TextArea.Caret.Line);
		}

		/// <summary>Opens the file of the <c>#include</c> directive on a line.</summary>
		/// <param name="line">The 1-based line.</param>
		/// <returns><see langword="true"/> when the line is an include directive.</returns>
		public Boolean OpenIncludeOnLine(Int32 line)
		{
			String? path = this.GetIncludePath(line);
			if (path is null)
			{
				return false;
			}

			this._context.RunInBackground(() => this._context.Services.Workspace.OpenIncludeAsync(this._context.DocumentModel, path));
			return true;
		}

		/// <summary>Navigates to the symbol or include under a position (Ctrl+click).</summary>
		/// <param name="offset">The offset of the clicked character.</param>
		/// <returns><see langword="true"/> when something was navigated to.</returns>
		public Boolean TryNavigateAt(Int32 offset)
		{
			TextDocument document = this._context.Document;
			if (offset < 0 || offset > document.TextLength || !this._context.IsScript)
			{
				return false;
			}

			Int32 line = document.GetLineByOffset(offset).LineNumber;
			if (this._context.Lexical.Current.GetKindAt(offset) == LexicalKind.String && this.OpenIncludeOnLine(line))
			{
				return true;
			}

			IScriptSymbol? symbol = this.FindSymbol(offset, out _);
			if (symbol is null)
			{
				return false;
			}

			this.GoToOffset(offset, -1, false);
			this.GoToDefinition();
			return true;
		}

		/// <summary>Copies the caret and scroll position into the view state.</summary>
		public void CaptureState()
		{
			EditorViewState state = this._context.ViewState;
			state.CaretOffset = this._context.TextArea.Caret.Offset;
			state.CaretLine = this._context.TextArea.Caret.Line;
			state.SelectionStart = this._context.Editor.SelectionStart;
			state.SelectionLength = this._context.Editor.SelectionLength;
			state.VerticalOffset = this._context.Editor.VerticalOffset;
			state.HorizontalOffset = this._context.Editor.HorizontalOffset;
		}

		/// <summary>Restores the caret and scroll position from the view state, or performs a pending navigation.</summary>
		public void RestoreState()
		{
			EditorViewState state = this._context.ViewState;
			this._deferredLine = -1;
			this._deferredScroll = false;
			if (state.PendingLine > 0)
			{
				Int32 line = state.PendingLine;
				state.PendingLine = 0;
				this.GoTo(line, state.PendingColumn, state.PendingSelectLength, true);
				this._deferredLine = this._context.TextArea.Caret.Line;
			}
			else if (state.HasBeenShown)
			{
				this._navigating = true;
				try
				{
					Int32 length = this._context.Document.TextLength;
					this._context.TextArea.Caret.Offset = Math.Min(state.CaretOffset, length);
					if (state.SelectionLength > 0 && state.SelectionStart + state.SelectionLength <= length)
					{
						this._context.TextArea.Selection = Selection.Create(
							this._context.TextArea,
							state.SelectionStart,
							state.SelectionStart + state.SelectionLength);
					}

					this._context.Editor.ScrollToVerticalOffset(state.VerticalOffset);
					this._context.Editor.ScrollToHorizontalOffset(state.HorizontalOffset);
					this._deferredScroll = true;
				}
				finally
				{
					this._navigating = false;
				}
			}

			this.UpdateCurrentProcedure();
		}

		/// <summary>
		/// Finishes <see cref="RestoreState"/> once the control has been measured: scrolls to a pending navigation target
		/// or restores the scroll position, which cannot be done before the first layout pass.
		/// </summary>
		public void CompleteRestoreAfterLayout()
		{
			EditorViewState state = this._context.ViewState;
			if (this._deferredLine > 0)
			{
				this.EnsureVisible(Math.Min(this._deferredLine, this._context.Document.LineCount), true);
			}
			else if (this._deferredScroll)
			{
				this._context.Editor.ScrollToVerticalOffset(state.VerticalOffset);
				this._context.Editor.ScrollToHorizontalOffset(state.HorizontalOffset);
			}

			this._deferredLine = -1;
			this._deferredScroll = false;
		}

		/// <summary>Recomputes the current procedure (call after the parse result changed).</summary>
		public void UpdateCurrentProcedure()
		{
			ScriptProcedure? procedure = null;
			ProgramInfo? info = this._context.ProgramInfo;
			if (info is not null && this._context.IsScript)
			{
				Int32 line = this._context.TextArea.Caret.Line;
				String path = this._context.DocumentModel.FilePath ?? String.Empty;
				procedure = info.Procedures.FirstOrDefault(p => p.StartLine > 0 && line >= p.StartLine && line <= p.EndLine && p.IsDefinedIn(path));
			}

			if (!Object.ReferenceEquals(procedure, this._currentProcedure))
			{
				this._currentProcedure = procedure;
				this.CurrentProcedureChanged?.Invoke(this, EventArgs.Empty);
			}
		}

		private void GoToHistory(CaretLocation? location)
		{
			if (location is not CaretLocation target)
			{
				return;
			}

			this._navigating = true;
			try
			{
				TextDocument document = this._context.Document;
				Int32 line = Math.Clamp(target.Line, 1, document.LineCount);
				DocumentLine documentLine = document.GetLineByNumber(line);
				Int32 offset = documentLine.Offset + Math.Clamp(target.Column - 1, 0, documentLine.Length);
				this._folding.Reveal(offset);
				this._context.TextArea.Caret.Offset = offset;
				this._context.TextArea.ClearSelection();
				ICSharpCode.AvalonEdit.Rendering.TextView view = this._context.TextView;
				Double top = view.GetVisualTopByDocumentLine(line);
				Boolean nearEdge = top <= view.VerticalOffset + view.DefaultLineHeight || top >= view.VerticalOffset + view.ActualHeight - (2 * view.DefaultLineHeight);
				this.EnsureVisible(line, nearEdge);
			}
			finally
			{
				this._navigating = false;
			}
		}

		private void GoToReference(SourceReference reference, String word)
		{
			String current = this._context.DocumentModel.FilePath ?? String.Empty;
			Boolean local = String.IsNullOrEmpty(reference.File) || String.Equals(reference.File, current, StringComparison.OrdinalIgnoreCase);
			if (local)
			{
				Int32 line = Math.Clamp(reference.Line, 1, this._context.Document.LineCount);
				String text = this._context.Document.GetText(this._context.Document.GetLineByNumber(line));
				Int32 column = text.IndexOf(word, StringComparison.OrdinalIgnoreCase);
				this.GoTo(line, column >= 0 ? column + 1 : -1, column >= 0 ? word.Length : -1, true);
				return;
			}

			this._context.RunInBackground(() => this._context.Services.Workspace.GoToAsync(reference.File, reference.Line));
		}

		private void OnCaretPositionChanged(Object? sender, EventArgs e)
		{
			TextLocation location = this._context.TextArea.Caret.Location;
			this._context.ViewState.CaretLine = location.Line;
			if (this._context.IsScript)
			{
				this._context.ViewState.History.OnCaretMoved(new CaretLocation(location.Line, location.Column), !this._navigating);
			}

			this.UpdateCurrentProcedure();
			this.CaretMoved?.Invoke(this, EventArgs.Empty);
		}

		private void OnPreviewMouseLeftButtonDown(Object sender, MouseButtonEventArgs e)
		{
			if (Keyboard.Modifiers != ModifierKeys.Control || e.ClickCount != 1 || !this._context.IsScript)
			{
				return;
			}

			Int32? offset = this.GetOffsetUnderMouse(e);
			if (offset is Int32 position && this.IsNavigable(position) && this.TryNavigateAt(position))
			{
				e.Handled = true;
			}
		}

		private void OnPreviewMouseMove(Object sender, MouseEventArgs e)
		{
			this.UpdateCursor(e);
		}

		private void OnModifierChanged(Object sender, KeyEventArgs e)
		{
			if (e.Key is Key.LeftCtrl or Key.RightCtrl || e.SystemKey is Key.LeftCtrl or Key.RightCtrl)
			{
				this._context.TextView.Cursor = null;
			}
		}

		private void UpdateCursor(MouseEventArgs e)
		{
			ICSharpCode.AvalonEdit.Rendering.TextView view = this._context.TextView;
			if (Keyboard.Modifiers == ModifierKeys.Control && this._context.IsScript && this.GetOffsetUnderMouse(e) is Int32 offset && this.IsNavigable(offset))
			{
				view.Cursor = Cursors.Hand;
			}
			else if (view.Cursor == Cursors.Hand)
			{
				view.Cursor = null;
			}
		}

		private Boolean IsNavigable(Int32 offset)
		{
			if (this._context.Lexical.Current.GetKindAt(offset) == LexicalKind.String)
			{
				return this.GetIncludePath(this._context.Document.GetLineByOffset(offset).LineNumber) is not null;
			}

			return this.FindSymbol(offset, out _) is not null;
		}

		private Int32? GetOffsetUnderMouse(MouseEventArgs e)
		{
			ICSharpCode.AvalonEdit.Rendering.TextView view = this._context.TextView;
			Point point = e.GetPosition(view);
			TextViewPosition? position = view.GetPositionFloor(point + view.ScrollOffset);
			if (position is null)
			{
				return null;
			}

			Int32 offset = this._context.Document.GetOffset(position.Value.Location);
			return offset < this._context.Document.TextLength && WordFinder.IsWordChar(this._context.Document.GetCharAt(offset)) || this._context.Lexical.Current.GetKindAt(offset) == LexicalKind.String
				? offset
				: null;
		}
	}
}
