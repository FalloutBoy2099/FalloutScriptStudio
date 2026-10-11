namespace FalloutScriptStudio.Editor
{
	using System;
	using System.ComponentModel;
	using System.Windows.Input;
	using System.Windows.Threading;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Editor.Controllers;
	using FalloutScriptStudio.Editor.Text;
	using ICSharpCode.AvalonEdit;
	using ICSharpCode.AvalonEdit.Document;

	/// <summary>
	/// Everything that makes one <see cref="TextEditor"/> a script editor: the feature controllers, the wiring to the
	/// <see cref="DocumentViewModel"/> and the restoring / capturing of the view state. A session lives while the editor
	/// view is loaded.
	/// </summary>
	public sealed class EditorSession : IDisposable
	{
		private readonly Action _toggleSplit;
		private Boolean _started;
		private EditorPane _active;

		public EditorSession(TextEditor editor, DocumentViewModel document, ScriptEditorServices services, Action toggleSplit)
		{
			this._toggleSplit = toggleSplit;
			this.Context = new EditorContext(editor, document, services);
			this.Appearance = new AppearanceController(this.Context);
			this.Highlighting = new HighlightingController(this.Context);
			this.Folding = new FoldingController(this.Context);
			this.Diagnostics = new DiagnosticsController(this.Context);
			this.Occurrences = new OccurrenceController(this.Context);
			this.Brackets = new BracketController(this.Context);
			this.Tips = new TipController(this.Context, this.Diagnostics);
			this.Completion = new CompletionController(this.Context);
			this.Navigation = new NavigationController(this.Context, this.Folding);
			this.Indentation = new SslIndentationStrategy(() => this.Context.IndentUnit) { IsFoldStart = this.Folding.IsFoldStart };
			this.Input = new InputController(this.Context, this.Completion, this.Tips, this.Indentation);
			this.Commands = new EditorCommandHandler(
				this.Context,
				this.Folding,
				this.Navigation,
				this.Occurrences,
				this.Completion,
				toggleSplit,
				this.Brackets);
			this.Primary = new EditorPane(this.Context, this.Folding, this.Navigation, this.Brackets, this.Tips, this.Completion, this.Input, this.Commands, true);
			this._active = this.Primary;
		}

		/// <summary>Raised when the appearance (font, colors, options) was re-applied.</summary>
		public event EventHandler? AppearanceApplied;

		/// <summary>Raised when the shared zoom changed.</summary>
		public event EventHandler? ZoomChanged;

		/// <summary>Raised after the caret of either pane moved.</summary>
		public event EventHandler? CaretMoved;

		/// <summary>Raised when the procedure that contains the caret of either pane changed.</summary>
		public event EventHandler? CurrentProcedureChanged;

		public EditorContext Context { get; }

		public AppearanceController Appearance { get; }

		public HighlightingController Highlighting { get; }

		public FoldingController Folding { get; }

		public DiagnosticsController Diagnostics { get; }

		public OccurrenceController Occurrences { get; }

		public BracketController Brackets { get; }

		public TipController Tips { get; }

		public CompletionController Completion { get; }

		public NavigationController Navigation { get; }

		public SslIndentationStrategy Indentation { get; }

		public InputController Input { get; }

		public EditorCommandHandler Commands { get; }

		/// <summary>Gets the first pane (the one the controller properties of the session belong to).</summary>
		public EditorPane Primary { get; }

		/// <summary>Gets the second pane of a split view, or <see langword="null"/> while the document is not split.</summary>
		public EditorPane? Secondary { get; private set; }

		/// <summary>Gets the pane that has the keyboard focus, or had it last: the one the text commands of the application act on.</summary>
		public EditorPane ActivePane => this._active;

		/// <summary>Gets a value indicating whether the document is shown in two panes.</summary>
		public Boolean IsSplit => this.Secondary is not null;

		/// <summary>Attaches the controllers to the editor and restores the view state of the document.</summary>
		public void Start()
		{
			if (this._started)
			{
				return;
			}

			this._started = true;
			TextEditor editor = this.Context.Editor;
			DocumentViewModel document = this.Context.DocumentModel;

			editor.Document = document.TextDocument;
			editor.TextArea.IndentationStrategy = this.Context.IsMessage ? null : this.Indentation;

			this.Appearance.Applied += this.OnAppearanceApplied;
			this.Appearance.Attach();
			this.Highlighting.Attach();
			this.Folding.Attach();
			this.Diagnostics.Attach();
			this.Occurrences.Attach();
			this.Brackets.Attach();
			this.Tips.Attach();
			this.Completion.Attach();
			this.Navigation.Attach();
			this.Input.Attach();

			document.PropertyChanged += this.OnDocumentPropertyChanged;
			document.TextDocument.TextChanged += this.OnTextChanged;
			document.TextDocument.UndoStack.PropertyChanged += this.OnUndoStackPropertyChanged;
			this.Context.Services.ZoomChanged += this.OnZoomChanged;

			this.Navigation.CaretMoved += this.OnPaneCaretMoved;
			this.Navigation.CurrentProcedureChanged += this.OnPaneProcedureChanged;
			this.Context.TextArea.GotKeyboardFocus += this.OnPaneGotKeyboardFocus;

			this.Navigation.RestoreState();
			document.ViewState.HasBeenShown = true;
		}

		/// <summary>
		/// Shows the document in a second editor control (split view). The second pane has the same features as the first one:
		/// highlighting, tips, completion, auto-pairing, folding, brackets, navigation and the text commands.
		/// </summary>
		/// <param name="editor">The second control; it must not show another document.</param>
		/// <returns>The second pane.</returns>
		public EditorPane ShowSecondary(TextEditor editor)
		{
			ArgumentNullException.ThrowIfNull(editor);
			if (this.Secondary is not null)
			{
				return this.Secondary;
			}

			EditorContext context = this.Context.CreatePaneContext(editor);
			this.Context.LinkedEditors.Add(editor);
			editor.Document = this.Context.Document;
			editor.TextArea.IndentationStrategy = this.Context.IsMessage ? null : this.Indentation;
			this.Appearance.ApplyTo(editor);
			editor.SyntaxHighlighting = this.Highlighting.Definition;

			// The caret is placed before the controllers listen, so that this does not count as a navigation of the user.
			EditorViewState state = this.Context.ViewState;
			Int32 caret = state.SecondaryVerticalOffset >= 0 ? state.SecondaryCaretOffset : this.Context.TextArea.Caret.Offset;
			editor.TextArea.Caret.Offset = Math.Min(caret, this.Context.Document.TextLength);
			this.Diagnostics.AddView(context.TextView);
			this.Occurrences.AddView(context.TextView);

			FoldingController folding = new FoldingController(context);
			NavigationController navigation = new NavigationController(context, folding);
			BracketController brackets = new BracketController(context);
			TipController tips = new TipController(context, this.Diagnostics);
			CompletionController completion = new CompletionController(context);
			InputController input = new InputController(context, completion, tips, this.Indentation);
			EditorCommandHandler commands = new EditorCommandHandler(context, folding, navigation, this.Occurrences, completion, this._toggleSplit, brackets);
			EditorPane pane = new EditorPane(context, folding, navigation, brackets, tips, completion, input, commands, false);

			folding.Attach(this.Folding);
			brackets.Attach();
			tips.Attach();
			completion.Attach();
			navigation.Attach();
			input.Attach();
			navigation.CaretMoved += this.OnPaneCaretMoved;
			navigation.CurrentProcedureChanged += this.OnPaneProcedureChanged;
			editor.TextArea.GotKeyboardFocus += this.OnPaneGotKeyboardFocus;

			this.Secondary = pane;
			state.IsSplit = true;
			return pane;
		}

		/// <summary>Scrolls the second pane to where it was when the document was last shown, or to where the first pane is.</summary>
		/// <remarks>Scrolling needs a measured control, so call it after the first layout pass.</remarks>
		public void CompleteSecondaryRestore()
		{
			if (this.Secondary is not EditorPane pane)
			{
				return;
			}

			EditorViewState state = this.Context.ViewState;
			pane.Context.Editor.ScrollToVerticalOffset(state.SecondaryVerticalOffset >= 0 ? state.SecondaryVerticalOffset : this.Context.Editor.VerticalOffset);
		}

		/// <summary>Closes the second pane; the document is not split any more.</summary>
		public void HideSecondary()
		{
			this.DetachSecondary();
			this.Context.ViewState.IsSplit = false;
			this.Context.ViewState.SecondaryVerticalOffset = -1;
		}

		/// <summary>Makes a pane the one the commands act on.</summary>
		/// <param name="pane">A pane of this session.</param>
		public void Activate(EditorPane pane)
		{
			ArgumentNullException.ThrowIfNull(pane);
			if (Object.ReferenceEquals(pane, this.Primary) || Object.ReferenceEquals(pane, this.Secondary))
			{
				this._active = pane;
			}
		}

		/// <summary>Finds the pane that shows an editor control.</summary>
		/// <param name="editor">The control.</param>
		/// <returns>The pane, or <see langword="null"/> when the control does not belong to this session.</returns>
		public EditorPane? FindPane(TextEditor editor)
		{
			if (Object.ReferenceEquals(this.Primary.Context.Editor, editor))
			{
				return this.Primary;
			}

			return this.Secondary is { } secondary && Object.ReferenceEquals(secondary.Context.Editor, editor) ? secondary : null;
		}

		/// <summary>Captures the view state of the document and detaches everything from the editor.</summary>
		public void Stop()
		{
			if (!this._started)
			{
				return;
			}

			this._started = false;
			this.CommitViewState();
			this.DetachSecondary();

			DocumentViewModel document = this.Context.DocumentModel;
			document.PropertyChanged -= this.OnDocumentPropertyChanged;
			document.TextDocument.TextChanged -= this.OnTextChanged;
			document.TextDocument.UndoStack.PropertyChanged -= this.OnUndoStackPropertyChanged;
			this.Context.Services.ZoomChanged -= this.OnZoomChanged;
			this.Appearance.Applied -= this.OnAppearanceApplied;
			this.Navigation.CaretMoved -= this.OnPaneCaretMoved;
			this.Navigation.CurrentProcedureChanged -= this.OnPaneProcedureChanged;
			this.Context.TextArea.GotKeyboardFocus -= this.OnPaneGotKeyboardFocus;

			this.Input.Dispose();
			this.Navigation.Dispose();
			this.Completion.Dispose();
			this.Tips.Dispose();
			this.Brackets.Dispose();
			this.Occurrences.Dispose();
			this.Diagnostics.Dispose();
			this.Folding.Dispose();
			this.Highlighting.Dispose();
			this.Appearance.Dispose();
			this.Context.Editor.Document = new TextDocument();
		}

		/// <summary>Copies the caret, scroll position and folds into the view state of the document.</summary>
		public void CommitViewState()
		{
			this.Navigation.CaptureState();
			this.Folding.CaptureState();
			if (this.Secondary is not null)
			{
				this.CaptureSecondaryState();
			}
		}

		public void Dispose()
		{
			this.Stop();
			this.Context.Dispose();
		}

		private void DetachSecondary()
		{
			if (this.Secondary is not EditorPane pane)
			{
				return;
			}

			this.CaptureSecondaryState();
			TextEditor editor = pane.Context.Editor;
			if (Object.ReferenceEquals(this._active, pane))
			{
				this._active = this.Primary;
			}

			pane.Navigation.CaretMoved -= this.OnPaneCaretMoved;
			pane.Navigation.CurrentProcedureChanged -= this.OnPaneProcedureChanged;
			editor.TextArea.GotKeyboardFocus -= this.OnPaneGotKeyboardFocus;

			pane.Input.Dispose();
			pane.Navigation.Dispose();
			pane.Completion.Dispose();
			pane.Tips.Dispose();
			pane.Brackets.Dispose();
			pane.Folding.Dispose();
			this.Occurrences.RemoveView(pane.Context.TextView);
			this.Diagnostics.RemoveView(pane.Context.TextView);
			this.Context.LinkedEditors.Remove(editor);
			this.Appearance.Release(editor);
			editor.Document = new TextDocument();
			pane.Context.Dispose();
			this.Secondary = null;
		}

		private void CaptureSecondaryState()
		{
			if (this.Secondary is EditorPane pane)
			{
				this.Context.ViewState.SecondaryCaretOffset = pane.Context.TextArea.Caret.Offset;
				this.Context.ViewState.SecondaryVerticalOffset = pane.Context.Editor.VerticalOffset;
			}
		}

		private void OnPaneCaretMoved(Object? sender, EventArgs e)
		{
			this.CaretMoved?.Invoke(this, EventArgs.Empty);
		}

		private void OnPaneProcedureChanged(Object? sender, EventArgs e)
		{
			this.CurrentProcedureChanged?.Invoke(this, EventArgs.Empty);
		}

		private void OnPaneGotKeyboardFocus(Object sender, KeyboardFocusChangedEventArgs e)
		{
			if (sender is ICSharpCode.AvalonEdit.Editing.TextArea area)
			{
				if (Object.ReferenceEquals(this.Primary.Context.TextArea, area))
				{
					this._active = this.Primary;
				}
				else if (this.Secondary is { } secondary && Object.ReferenceEquals(secondary.Context.TextArea, area))
				{
					this._active = secondary;
				}
			}
		}

		private void OnAppearanceApplied(Object? sender, EventArgs e)
		{
			this.Highlighting.Update(true);
			this.Diagnostics.RefreshPalette();
			this.Diagnostics.Update();
			this.Occurrences.RefreshPalette();
			this.Brackets.RefreshPalette();
			this.Secondary?.Brackets.RefreshPalette();
			this.AppearanceApplied?.Invoke(this, EventArgs.Empty);
		}

		private void OnZoomChanged(Object? sender, EventArgs e)
		{
			// The appearance controller listens to the zoom itself and re-applies the font once.
			this.ZoomChanged?.Invoke(this, EventArgs.Empty);
		}

		private void OnDocumentPropertyChanged(Object? sender, PropertyChangedEventArgs e)
		{
			Dispatcher dispatcher = this.Context.Editor.Dispatcher;
			if (!dispatcher.CheckAccess())
			{
				dispatcher.BeginInvoke(new Action(() => this.OnDocumentPropertyChanged(sender, e)));
				return;
			}

			if (!this._started)
			{
				return;
			}

			switch (e.PropertyName)
			{
				case nameof(DocumentViewModel.ProgramInfo):
					this.Highlighting.Update();
					this.Folding.Update();
					this.Navigation.UpdateCurrentProcedure();
					this.Secondary?.Folding.Update();
					this.Secondary?.Navigation.UpdateCurrentProcedure();
					break;
				case nameof(DocumentViewModel.ParserErrors):
					this.Diagnostics.Update();
					this.Tips.HideHoverTip();
					this.Secondary?.Tips.HideHoverTip();
					break;
				case nameof(DocumentViewModel.FilePath):
					this.Highlighting.Update(true);
					this.Folding.Update();
					this.Secondary?.Folding.Update();
					this.Diagnostics.Update();
					break;
				case nameof(DocumentViewModel.Kind):
					this.Appearance.Apply();
					break;
				default:
					break;
			}
		}

		private void OnTextChanged(Object? sender, EventArgs e)
		{
			DocumentViewModel document = this.Context.DocumentModel;
			if (!document.IsModified)
			{
				document.IsModified = true;
			}

			if (document.IsParsable)
			{
				document.NeedsParse = true;
				this.Context.RunInBackground(() => this.Context.Services.Workspace.RequestParseAsync(document));
			}
		}

		private void OnUndoStackPropertyChanged(Object? sender, PropertyChangedEventArgs e)
		{
			UndoStack stack = this.Context.Document.UndoStack;
			if (e.PropertyName == nameof(UndoStack.IsOriginalFile) && stack.IsOriginalFile && this.Context.DocumentModel.IsModified)
			{
				this.Context.DocumentModel.IsModified = false;
			}
		}
	}
}
