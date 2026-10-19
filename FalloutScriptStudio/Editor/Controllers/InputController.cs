namespace FalloutScriptStudio.Editor.Controllers
{
	using System;
	using System.Collections.Generic;
	using System.Windows;
	using System.Windows.Input;
	using FalloutScriptStudio.Core.TextTools;
	using FalloutScriptStudio.Editor.Text;
	using FalloutScriptStudio.Editor.Tips;
	using ICSharpCode.AvalonEdit;
	using ICSharpCode.AvalonEdit.Document;
	using ICSharpCode.AvalonEdit.Editing;

	/// <summary>
	/// Keyboard and text input behaviours: keyword block completion on Tab, automatically closed brackets and quotes,
	/// smart formatting after <c>;</c>, aligning a typed <c>end</c>, selecting template placeholders, and file drops.
	/// </summary>
	public sealed class InputController : IDisposable
	{
		private readonly EditorContext _context;
		private readonly CompletionController _completion;
		private readonly TipController _tips;
		private readonly SslIndentationStrategy _indentation;
		private readonly List<TextAnchor> _pairAnchors = [];
		private readonly KeywordExpander _expander = new();
		private readonly SnippetController _snippets;
		private readonly FalloutScriptStudio.Core.TextTools.CodeFormatter _formatter = new();
		private readonly KeyBinding _undoBinding = new(ApplicationCommands.Undo, Key.Back, ModifierKeys.Alt);
		private Boolean _editing;
		private Boolean _selectingPlaceholder;
		private Boolean _attached;

		public InputController(EditorContext context, CompletionController completion, TipController tips, SslIndentationStrategy indentation)
		{
			this._context = context;
			this._completion = completion;
			this._tips = tips;
			this._indentation = indentation;
			this._snippets = new SnippetController(context, completion);
			completion.Snippets = this._snippets;
		}

		/// <summary>Gets the controller of the snippets (Tab expansion, tab stops, Insert Snippet, Surround With).</summary>
		public SnippetController Snippets => this._snippets;

		public void Attach()
		{
			this._attached = true;
			this._snippets.Attach();
			TextArea area = this._context.TextArea;
			area.PreviewKeyDown += this.OnPreviewKeyDown;
			area.PreviewTextInput += this.OnPreviewTextInput;
			area.TextEntered += this.OnTextEntered;
			area.PreviewMouseLeftButtonDown += this.OnPreviewMouseLeftButtonDown;
			area.PreviewMouseLeftButtonUp += this.OnPreviewMouseLeftButtonUp;
			area.Caret.PositionChanged += this.OnCaretPositionChanged;
			this._context.Document.Changing += this.OnDocumentChanging;
			this._context.Document.UpdateFinished += this.OnDocumentUpdateFinished;

			// Legacy: Alt+Backspace undoes (as in old Windows editors).
			area.InputBindings.Add(this._undoBinding);

			this._context.Editor.AllowDrop = true;
			this._context.Editor.PreviewDragEnter += this.OnPreviewDragOver;
			this._context.Editor.PreviewDragOver += this.OnPreviewDragOver;
			this._context.Editor.PreviewDrop += this.OnPreviewDrop;
		}

		public void Dispose()
		{
			this._attached = false;
			this._snippets.Dispose();
			TextArea area = this._context.TextArea;
			area.PreviewKeyDown -= this.OnPreviewKeyDown;
			area.PreviewTextInput -= this.OnPreviewTextInput;
			area.TextEntered -= this.OnTextEntered;
			area.PreviewMouseLeftButtonDown -= this.OnPreviewMouseLeftButtonDown;
			area.PreviewMouseLeftButtonUp -= this.OnPreviewMouseLeftButtonUp;
			area.Caret.PositionChanged -= this.OnCaretPositionChanged;
			this._context.Document.Changing -= this.OnDocumentChanging;
			this._context.Document.UpdateFinished -= this.OnDocumentUpdateFinished;
			area.InputBindings.Remove(this._undoBinding);
			this._context.Editor.PreviewDragEnter -= this.OnPreviewDragOver;
			this._context.Editor.PreviewDragOver -= this.OnPreviewDragOver;
			this._context.Editor.PreviewDrop -= this.OnPreviewDrop;
			this._pairAnchors.Clear();
		}

		/// <summary>Expands the keyword before the caret into a code block (the Tab key action).</summary>
		/// <returns><see langword="true"/> when a block was inserted.</returns>
		public Boolean TryCompleteBlock()
		{
			if (!this._context.IsScript || this._context.TextArea.Selection.Length > 0)
			{
				return false;
			}

			TextDocument document = this._context.Document;
			Int32 caret = this._context.TextArea.Caret.Offset;
			if (!this._context.Lexical.Current.IsCodeAtCaret(caret))
			{
				return false;
			}

			KeywordExpansionOptions options = new()
			{
				TabSize = this._context.Options.TabSize,
				ConvertTabsToSpaces = this._context.Editor.Options.ConvertTabsToSpaces,
				NewLine = this._context.NewLine,
			};
			KeywordExpansion? expansion = this._expander.TryExpand(document.Text, caret, options);
			if (expansion is null)
			{
				return false;
			}

			DocumentEdits.Apply(document, new[] { expansion.Edit });
			if (expansion.Selection.IsEmpty)
			{
				this._context.TextArea.Caret.Offset = expansion.Selection.Offset;
			}
			else
			{
				this._context.TextArea.Caret.Offset = expansion.Selection.Offset;
				this._context.TextArea.Selection = Selection.Create(this._context.TextArea, expansion.Selection.Offset, expansion.Selection.End);
			}

			this._context.TextArea.Caret.BringCaretToView();
			return true;
		}

		/// <summary>Selects the template placeholder at an offset, if there is one.</summary>
		/// <param name="offset">The offset.</param>
		/// <param name="moveCaret">Whether the caret goes to the start of the placeholder; keyboard navigation leaves it where it is, so that the next key press moves on.</param>
		/// <returns><see langword="true"/> when a placeholder was selected.</returns>
		public Boolean TrySelectPlaceholder(Int32 offset, Boolean moveCaret = true)
		{
			if (!this._context.IsScript
				|| !PlaceholderFinder.TryFind(this._context.Lexical.Current, this._context.Document, offset, out Int32 start, out Int32 length))
			{
				return false;
			}

			this._selectingPlaceholder = true;
			try
			{
				if (moveCaret)
				{
					this._context.TextArea.Caret.Offset = start;
				}

				this._context.TextArea.Selection = Selection.Create(this._context.TextArea, start, start + length);
			}
			finally
			{
				this._selectingPlaceholder = false;
			}

			return true;
		}

		/// <summary>
		/// Selects the placeholder the caret has moved into, whatever moved it (legacy: <c>Caret_PositionChanged</c> calls
		/// <c>SelectedTextColorRegion</c>). Caret moves caused by editing, by an extended selection or by a drag with the mouse are left alone.
		/// </summary>
		private void OnCaretPositionChanged(Object? sender, EventArgs e)
		{
			if (this._editing
				|| this._selectingPlaceholder
				|| !this._context.IsScript
				|| this._context.TextArea.Selection.Length > 0
				|| (this._context.Services.Input.Modifiers & ModifierKeys.Shift) != 0
				|| this._context.Services.Input.IsLeftButtonPressed)
			{
				return;
			}

			this.TrySelectPlaceholder(this._context.TextArea.Caret.Offset, false);
		}

		private void OnDocumentChanging(Object? sender, DocumentChangeEventArgs e)
		{
			this._editing = true;
		}

		private void OnDocumentUpdateFinished(Object? sender, EventArgs e)
		{
			this._editing = false;
		}

		private void OnPreviewKeyDown(Object sender, KeyEventArgs e)
		{
			this._tips.OnPreviewKeyDown(e);

			if (this._snippets.TryHandleKey(e))
			{
				this._tips.HideAll();
				e.Handled = true;
			}
			else if (e.Key == Key.Tab && this._context.Services.Input.Modifiers == ModifierKeys.None && this.TryCompleteBlock())
			{
				this._completion.Close();
				this._tips.HideAll();
				e.Handled = true;
			}
			else if (e.Key == Key.Tab && this._context.Services.Input.Modifiers == ModifierKeys.None && this._snippets.TryExpandTrigger())
			{
				this._tips.HideAll();
				e.Handled = true;
			}
		}

		private void OnPreviewTextInput(Object sender, TextCompositionEventArgs e)
		{
			if (!this._context.IsScript || !this._context.Options.AutoInputPairedCharacters || e.Text.Length != 1
				|| this._context.TextArea.Selection.Length > 0)
			{
				return;
			}

			TextDocument document = this._context.Document;
			Int32 caret = this._context.TextArea.Caret.Offset;
			Char typed = e.Text[0];
			Char next = caret < document.TextLength ? document.GetCharAt(caret) : '\0';

			if (typed == '"' && next == '"' && this._context.Lexical.Current.TryGetSpanAt(caret, out LexicalSpan span)
				&& span.Kind == LexicalKind.String && span.IsClosed && span.End == caret + 1 && span.Start < caret)
			{
				this._context.TextArea.Caret.Offset = caret + 1;
				e.Handled = true;
			}
			else if ((typed == ')' || typed == ']') && next == typed && this.TryConsumePairAnchor(caret))
			{
				this._context.TextArea.Caret.Offset = caret + 1;
				e.Handled = true;
			}
		}

		private void OnTextEntered(Object sender, TextCompositionEventArgs e)
		{
			if (!this._context.IsScript || e.Text.Length != 1)
			{
				return;
			}

			TextDocument document = this._context.Document;
			Int32 caret = this._context.TextArea.Caret.Offset;
			Char typed = e.Text[0];
			Boolean inCode = this._context.Lexical.Current.IsCodeAtCaret(caret - 1) || typed == '"';

			switch (typed)
			{
				case '"':
					if (this._context.Options.AutoInputPairedCharacters)
					{
						this.PairQuote(document, caret);
					}

					break;
				case '(':
				case '[':
					if (!inCode)
					{
						break;
					}

					if (this._context.Options.AutoInputPairedCharacters)
					{
						this.PairBracket(document, caret, typed == '[' ? ']' : ')');
					}

					if (typed == '(')
					{
						this._tips.ShowParameterTip(caret - 1);
					}

					break;
				case ')':
				case ']':
					this._tips.HideCodeTip();
					break;
				case ';':
					if (inCode)
					{
						this.FormatLineAfterSemicolon(document, caret);
					}

					break;
				case 'd':
				case 'D':
					if (inCode)
					{
						this._indentation.AlignClosingEnd(document, document.GetLineByOffset(caret));
					}

					break;
				default:
					break;
			}

			if (WordFinder.IsWordChar(typed) && inCode)
			{
				this.ShowBlockKeywordHint(document, caret);
			}
		}

		private void ShowBlockKeywordHint(TextDocument document, Int32 caret)
		{
			if (!WordFinder.TryGetWordRange(document, caret - 1, out Int32 start, out Int32 length) || start + length != caret)
			{
				return;
			}

			if (TipResolver.IsBlockKeyword(document.GetText(start, length)))
			{
				this._tips.ShowHint(TipResolver.BlockKeywordHint, start, TimeSpan.FromSeconds(5));
			}
		}

		private void PairQuote(TextDocument document, Int32 caret)
		{
			Int32 quote = caret - 1;
			if (!this._context.Lexical.Current.TryGetSpanAt(quote, out LexicalSpan span) || span.Kind != LexicalKind.String
				|| span.Start != quote || span.IsClosed)
			{
				return;
			}

			Char before = quote > 0 ? document.GetCharAt(quote - 1) : '\0';
			Char after = caret < document.TextLength ? document.GetCharAt(caret) : '\0';
			Boolean emptyCall = before == '(' && after == ')';
			Boolean freeSpot = before != '"' && (after == '\0' || after == ' ' || after == '\t' || after == '\r' || after == '\n') && !Char.IsLetterOrDigit(before);
			if (emptyCall || freeSpot)
			{
				this.InsertCloser(document, caret, '"');
			}
		}

		private void PairBracket(TextDocument document, Int32 caret, Char closer)
		{
			Char after = caret < document.TextLength ? document.GetCharAt(caret) : '\0';
			if (after == '\0' || Char.IsWhiteSpace(after))
			{
				this.InsertCloser(document, caret, closer);
			}
		}

		private void InsertCloser(TextDocument document, Int32 caret, Char closer)
		{
			document.Insert(caret, closer.ToString());
			this._context.TextArea.Caret.Offset = caret;
			if (closer != '"')
			{
				TextAnchor anchor = document.CreateAnchor(caret);
				anchor.MovementType = AnchorMovementType.AfterInsertion;
				this._pairAnchors.Add(anchor);
				this._pairAnchors.RemoveAll(a => a.IsDeleted);
			}
		}

		private Boolean TryConsumePairAnchor(Int32 caret)
		{
			for (Int32 i = this._pairAnchors.Count - 1; i >= 0; i--)
			{
				TextAnchor anchor = this._pairAnchors[i];
				if (anchor.IsDeleted)
				{
					this._pairAnchors.RemoveAt(i);
				}
				else if (anchor.Offset == caret)
				{
					this._pairAnchors.RemoveAt(i);
					return true;
				}
			}

			return false;
		}

		private void FormatLineAfterSemicolon(TextDocument document, Int32 caret)
		{
			SmartFormatResult? result = this._formatter.FormatSmart(document.Text, caret, this._context.Services.GetFormatOptions());
			if (result is not null)
			{
				DocumentEdits.Apply(document, new[] { result.Edit });
				this._context.TextArea.Caret.Offset = result.CaretOffset;
			}
		}

		private void OnPreviewMouseLeftButtonDown(Object sender, MouseButtonEventArgs e)
		{
			if (e.ClickCount != 2 || !this._context.IsScript)
			{
				return;
			}

			Int32? offset = this.GetOffsetAtMouse(e);
			if (offset is Int32 position && this.TrySelectPlaceholder(position))
			{
				e.Handled = true;
			}
		}

		private void OnPreviewMouseLeftButtonUp(Object sender, MouseButtonEventArgs e)
		{
			if (!this._context.IsScript || this._context.TextArea.Selection.Length > 0 || this._context.Services.Input.Modifiers != ModifierKeys.None)
			{
				return;
			}

			// A plain click inside a template placeholder selects it, so that typing replaces it.
			this._context.TextArea.Dispatcher.BeginInvoke(new Action(() =>
			{
				if (this._attached && this._context.TextArea.Selection.Length == 0)
				{
					this.TrySelectPlaceholder(this._context.TextArea.Caret.Offset);
				}
			}));
		}

		private Int32? GetOffsetAtMouse(MouseEventArgs e)
		{
			Point point = e.GetPosition(this._context.TextView);
			TextViewPosition? position = this._context.TextView.GetPositionFloor(point + this._context.TextView.ScrollOffset);
			return position is null ? null : this._context.Document.GetOffset(position.Value.Location);
		}

		private void OnPreviewDragOver(Object sender, DragEventArgs e)
		{
			if (e.Data.GetDataPresent(DataFormats.FileDrop))
			{
				e.Effects = DragDropEffects.Link;
				e.Handled = true;
			}
		}

		/// <summary>
		/// Opens the files of a drop on the text. This is the same path as a drop on the rest of the window: the files go to the recent
		/// files and a file that is open already is offered as a copy.
		/// </summary>
		/// <param name="data">The dropped data.</param>
		/// <returns><see langword="true"/> when the data held files.</returns>
		public Boolean TryOpenDroppedFiles(IDataObject data)
		{
			if (data.GetData(DataFormats.FileDrop) is not String[] files)
			{
				return false;
			}

			this._context.RunInBackground(() => this._context.Services.Workspace.OpenDroppedFilesAsync(files));
			return true;
		}

		private void OnPreviewDrop(Object sender, DragEventArgs e)
		{
			if (this.TryOpenDroppedFiles(e.Data))
			{
				e.Handled = true;
			}
		}
	}
}