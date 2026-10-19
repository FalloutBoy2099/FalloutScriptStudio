namespace FalloutScriptStudio.Editor.Controllers
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;
	using System.Windows.Input;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Core.Snippets;
	using FalloutScriptStudio.Core.TextTools;
	using FalloutScriptStudio.Editor.Text;
	using FalloutScriptStudio.Services.ToolWindows;
	using ICSharpCode.AvalonEdit.Document;
	using ICSharpCode.AvalonEdit.Editing;

	/// <summary>
	/// Expands snippets in the editor: the trigger word followed by Tab, a choice in the completion list or in the "Insert Snippet" list, or
	/// the selected code surrounded by a snippet. A snippet with tab stops starts a session: Tab and Shift+Tab move between the stops, typing in a
	/// stop updates its mirrors, and Esc (or leaving the text of the snippet) ends it. The whole expansion is one undo step.
	/// </summary>
	public sealed class SnippetController : IDisposable
	{
		private readonly EditorContext _context;
		private readonly CompletionController _completion;
		private readonly List<Stop> _stops = [];
		private TextAnchor? _sessionStart;
		private TextAnchor? _sessionEnd;
		private TextAnchor? _final;
		private Int32 _current = -1;
		private Boolean _attached;
		private Boolean _updating;
		private Boolean _selecting;

		/// <summary>Initializes a new instance of the <see cref="SnippetController"/> class.</summary>
		/// <param name="context">The editor.</param>
		/// <param name="completion">The completion list, which is closed when a snippet is expanded.</param>
		public SnippetController(EditorContext context, CompletionController completion)
		{
			ArgumentNullException.ThrowIfNull(context);
			ArgumentNullException.ThrowIfNull(completion);
			this._context = context;
			this._completion = completion;
		}

		/// <summary>Gets a value indicating whether a snippet is being filled in (Tab goes to its next stop).</summary>
		public Boolean IsActive => this._sessionStart is not null;

		/// <summary>Gets the number of the tab stop the caret is in, or -1 outside a session.</summary>
		public Int32 CurrentStopIndex => this.IsActive && this._current >= 0 && this._current < this._stops.Count ? this._stops[this._current].Index : -1;

		/// <summary>Gets the number of the tab stops of the session.</summary>
		public Int32 StopCount => this._stops.Count;

		/// <summary>Starts following the editor.</summary>
		public void Attach()
		{
			this._attached = true;
			this._context.Document.TextChanged += this.OnTextChanged;
			this._context.TextArea.Caret.PositionChanged += this.OnCaretPositionChanged;
		}

		/// <summary>Stops following the editor and ends the session.</summary>
		public void Dispose()
		{
			this._attached = false;
			this._context.Document.TextChanged -= this.OnTextChanged;
			this._context.TextArea.Caret.PositionChanged -= this.OnCaretPositionChanged;
			this.End();
		}

		/// <summary>Handles Tab, Shift+Tab and Esc while a snippet is being filled in.</summary>
		/// <param name="e">The key press (Preview).</param>
		/// <returns><see langword="true"/> when the key was used.</returns>
		public Boolean TryHandleKey(KeyEventArgs e)
		{
			ArgumentNullException.ThrowIfNull(e);

			if (!this.IsActive)
			{
				return false;
			}

			if (e.Key == Key.Escape && this._context.Services.Input.Modifiers == ModifierKeys.None)
			{
				this.End();
				return true;
			}

			if (e.Key != Key.Tab || (this._context.Services.Input.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) != 0)
			{
				return false;
			}

			if (this._completion.IsOpen && this._completion.Window?.CompletionList.SelectedItem is not null)
			{
				// The list is open: Tab chooses its entry, as everywhere else.
				return false;
			}

			return (this._context.Services.Input.Modifiers & ModifierKeys.Shift) != 0 ? this.MoveToPreviousStop() : this.MoveToNextStop();
		}

		/// <summary>Moves to the next tab stop of the session (Tab); after the last one the caret goes to the final position and the session ends.</summary>
		/// <returns><see langword="true"/> when a session was active.</returns>
		public Boolean MoveToNextStop()
		{
			if (!this.IsActive)
			{
				return false;
			}

			this.GoToStop(this._current + 1, true);
			return true;
		}

		/// <summary>Moves back to the previous tab stop of the session (Shift+Tab).</summary>
		/// <returns><see langword="true"/> when a session was active.</returns>
		public Boolean MoveToPreviousStop()
		{
			if (!this.IsActive)
			{
				return false;
			}

			this.GoToStop(this._current - 1, false);
			return true;
		}

		/// <summary>Expands the word before the caret when it is the trigger of a snippet (Tab).</summary>
		/// <returns><see langword="true"/> when a snippet was inserted.</returns>
		public Boolean TryExpandTrigger()
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

			String word = CompletionSourceWord(document, caret, out Int32 start);
			if (word.Length == 0 || SnippetValidator.IsReserved(word) || WordFinder.IsWordChar(CharAt(document, caret)))
			{
				return false;
			}

			Snippet? snippet = SnippetCatalog.Find(this._context.Services.Snippets.GetSnippets(), word);
			if (snippet is null)
			{
				return false;
			}

			if (this._completion.IsOpen && this._completion.Window?.CompletionList.SelectedItem is { } selected
				&& !String.Equals(selected.Text, word, StringComparison.OrdinalIgnoreCase))
			{
				// Another entry of the list is chosen: Tab belongs to the list.
				return false;
			}

			this._completion.Close();
			this.Insert(snippet, start, caret - start, null);
			return true;
		}

		/// <summary>Inserts a snippet in place of a range of the text.</summary>
		/// <param name="snippet">The snippet.</param>
		/// <param name="offset">The offset of the text it replaces (the typed trigger, or the caret).</param>
		/// <param name="length">The length of that text.</param>
		/// <param name="selection">The selected text for the variable <c>$SELECTION</c>, or <see langword="null"/>.</param>
		public void Insert(Snippet snippet, Int32 offset, Int32 length, String? selection)
		{
			ArgumentNullException.ThrowIfNull(snippet);

			this.End();
			TextDocument document = this._context.Document;
			offset = Math.Clamp(offset, 0, document.TextLength);
			length = Math.Clamp(length, 0, document.TextLength - offset);

			// A snippet that starts with a # (#define) takes the # the user typed before the trigger.
			if (length > 0 && offset > 0 && snippet.Body.StartsWith('#') && document.GetCharAt(offset - 1) == '#')
			{
				offset--;
				length++;
			}

			SnippetContext context = this.CreateContext(offset, selection);
			SnippetExpansion expansion = SnippetExpander.Expand(snippet.Body, context);
			using (document.RunUpdate())
			{
				document.Replace(offset, length, expansion.Text);
			}

			this.Begin(expansion, offset);
		}

		/// <summary>Surrounds the selected text with a snippet that uses <c>$SELECTION</c>.</summary>
		/// <param name="snippet">The snippet.</param>
		/// <returns><see langword="true"/> when there was a selection to surround.</returns>
		public Boolean Surround(Snippet snippet)
		{
			ArgumentNullException.ThrowIfNull(snippet);

			TextArea area = this._context.TextArea;
			if (area.Selection.IsEmpty)
			{
				return false;
			}

			TextDocument document = this._context.Document;
			Int32 start = Math.Min(area.Selection.SurroundingSegment.Offset, document.TextLength);
			Int32 end = Math.Min(area.Selection.SurroundingSegment.EndOffset, document.TextLength);

			// Whole lines: the indentation in front of the selection stays where it is, and a line break at its end stays after the snippet.
			DocumentLine first = document.GetLineByOffset(start);
			Int32 indentEnd = first.Offset;
			while (indentEnd < first.EndOffset && (document.GetCharAt(indentEnd) is ' ' or '\t'))
			{
				indentEnd++;
			}

			if (start <= indentEnd)
			{
				start = indentEnd;
			}

			while (end > start && (document.GetCharAt(end - 1) is '\r' or '\n'))
			{
				end--;
			}

			if (end <= start)
			{
				return false;
			}

			this.Insert(snippet, start, end - start, document.GetText(start, end - start));
			return true;
		}

		/// <summary>Lets the user choose a snippet and inserts it (the commands Insert Snippet and Surround With).</summary>
		/// <param name="surround">Whether only the snippets that surround code are offered, and the selection is required.</param>
		/// <returns>A task that completes when the snippet was inserted or the choice was cancelled.</returns>
		public async Task InsertChosenAsync(Boolean surround)
		{
			IReadOnlyList<Snippet> snippets = this._context.Services.Snippets.GetSnippets();
			if (surround)
			{
				snippets = snippets.Where(snippet => snippet.SurroundsSelection).ToList();
			}

			Snippet? chosen = await this._context.Services.Workspace.PickSnippetAsync(new SnippetPickerRequest(snippets, surround));
			if (chosen is null || !this._attached)
			{
				return;
			}

			TextArea area = this._context.TextArea;
			if (!area.Selection.IsEmpty && (surround || chosen.SurroundsSelection))
			{
				if (this.Surround(chosen))
				{
					return;
				}
			}

			Int32 offset = area.Selection.IsEmpty ? area.Caret.Offset : area.Selection.SurroundingSegment.Offset;
			Int32 length = area.Selection.IsEmpty ? 0 : area.Selection.SurroundingSegment.Length;
			this.Insert(chosen, offset, length, null);
		}

		/// <summary>Ends the session: Tab is a normal key again and the stops are forgotten.</summary>
		public void End()
		{
			foreach (Stop stop in this._stops)
			{
				stop.Release();
			}

			this._stops.Clear();
			this._sessionStart = null;
			this._sessionEnd = null;
			this._final = null;
			this._current = -1;
		}

		private static String CompletionSourceWord(TextDocument document, Int32 caret, out Int32 start)
		{
			start = caret;
			while (start > 0 && WordFinder.IsWordChar(document.GetCharAt(start - 1)))
			{
				start--;
			}

			return document.GetText(start, caret - start);
		}

		private static Char CharAt(TextDocument document, Int32 offset)
		{
			return offset >= 0 && offset < document.TextLength ? document.GetCharAt(offset) : '\0';
		}

		private SnippetContext CreateContext(Int32 offset, String? selection)
		{
			TextDocument document = this._context.Document;
			DocumentLine line = document.GetLineByOffset(Math.Min(offset, document.TextLength));
			Int32 indentEnd = line.Offset;
			while (indentEnd < Math.Min(offset, line.EndOffset) && (document.GetCharAt(indentEnd) is ' ' or '\t'))
			{
				indentEnd++;
			}

			String fileName = this._context.DocumentModel.FilePath is { } path ? System.IO.Path.GetFileName(path) : String.Empty;
			String procedure = String.Empty;
			if (this._context.ProgramInfo is { } info)
			{
				String file = this._context.DocumentModel.FilePath ?? String.Empty;
				procedure = info.Procedures.FirstOrDefault(p => p.StartLine > 0 && line.LineNumber >= p.StartLine && line.LineNumber <= p.EndLine && p.IsDefinedIn(file))?.Name ?? String.Empty;
			}

			return new SnippetContext
			{
				IndentUnit = this._context.IndentUnit,
				LineIndent = document.GetText(line.Offset, indentEnd - line.Offset),
				NewLine = this._context.NewLine,
				Selection = selection,
				CurrentProcedure = procedure,
				FileName = fileName,
				ScriptName = System.IO.Path.GetFileNameWithoutExtension(fileName),
				Now = DateTime.Now,
			};
		}

		/// <summary>Follows the stops of an expansion that was inserted at an offset.</summary>
		private void Begin(SnippetExpansion expansion, Int32 offset)
		{
			TextDocument document = this._context.Document;
			TextArea area = this._context.TextArea;
			if (expansion.Stops.Count == 0)
			{
				this.MoveCaret(offset + expansion.FinalOffset);
				return;
			}

			foreach (SnippetStop stop in expansion.Stops)
			{
				this._stops.Add(new Stop(document, stop, offset));
			}

			this._sessionStart = document.CreateAnchor(offset);
			this._sessionStart.MovementType = AnchorMovementType.BeforeInsertion;
			this._sessionEnd = document.CreateAnchor(offset + expansion.Text.Length);
			this._sessionEnd.MovementType = AnchorMovementType.AfterInsertion;
			this._final = document.CreateAnchor(offset + expansion.FinalOffset);
			this._final.MovementType = AnchorMovementType.AfterInsertion;
			this._current = -1;
			this.GoToStop(0, true);
			area.Caret.BringCaretToView();
		}

		private void GoToStop(Int32 index, Boolean forward)
		{
			Int32 step = forward ? 1 : -1;
			for (Int32 i = Math.Max(index, 0); i >= 0 && i < this._stops.Count; i += step)
			{
				if (this._stops[i].IsAlive)
				{
					this._current = i;
					this.Select(this._stops[i].Primary);
					return;
				}
			}

			if (forward)
			{
				Int32 final = this._final is { IsDeleted: false } anchor ? anchor.Offset : this._context.TextArea.Caret.Offset;
				this.End();
				this.MoveCaret(final);
			}
			else if (this._current >= 0)
			{
				this.Select(this._stops[this._current].Primary);
			}
		}

		private void Select(StopRange range)
		{
			this._selecting = true;
			try
			{
				TextArea area = this._context.TextArea;
				Int32 start = range.Start;
				Int32 end = range.End;
				area.Caret.Offset = end;
				area.Selection = start == end ? Selection.Create(area, start, start) : Selection.Create(area, start, end);
				if (start == end)
				{
					area.Caret.Offset = start;
				}
			}
			finally
			{
				this._selecting = false;
			}
		}

		private void MoveCaret(Int32 offset)
		{
			this._selecting = true;
			try
			{
				TextArea area = this._context.TextArea;
				area.Selection = Selection.Create(area, offset, offset);
				area.Caret.Offset = offset;
				area.Caret.BringCaretToView();
			}
			finally
			{
				this._selecting = false;
			}
		}

		/// <summary>After an edit, the mirrors of a stop take the text of the stop the user edits.</summary>
		private void OnTextChanged(Object? sender, EventArgs e)
		{
			if (!this.IsActive || this._updating)
			{
				return;
			}

			this._updating = true;
			try
			{
				TextDocument document = this._context.Document;

				// The mirrors change in the undo step of the typing that caused it.
				document.UndoStack.StartContinuedUndoGroup();
				try
				{
					foreach (Stop stop in this._stops)
					{
						if (!stop.IsAlive)
						{
							continue;
						}

						String text = document.GetText(stop.Primary.Start, stop.Primary.End - stop.Primary.Start);
						if (String.Equals(text, stop.LastText, StringComparison.Ordinal))
						{
							continue;
						}

						stop.LastText = text;
						for (Int32 i = stop.Occurrences.Count - 1; i >= 1; i--)
						{
							StopRange mirror = stop.Occurrences[i];
							if (mirror.IsDeleted)
							{
								continue;
							}

							if (!String.Equals(document.GetText(mirror.Start, mirror.End - mirror.Start), text, StringComparison.Ordinal))
							{
								document.Replace(mirror.Start, mirror.End - mirror.Start, text);
							}
						}
					}
				}
				finally
				{
					document.UndoStack.EndUndoGroup();
				}
			}
			finally
			{
				this._updating = false;
			}
		}

		private void OnCaretPositionChanged(Object? sender, EventArgs e)
		{
			if (!this.IsActive || this._selecting || this._updating)
			{
				return;
			}

			if (this._sessionStart is not { IsDeleted: false } start || this._sessionEnd is not { IsDeleted: false } end)
			{
				this.End();
				return;
			}

			Int32 caret = this._context.TextArea.Caret.Offset;
			if (caret < start.Offset || caret > end.Offset)
			{
				// The caret left the text of the snippet: the session is over.
				this.End();
			}
		}

		/// <summary>The range of one occurrence of a stop, followed through the edits.</summary>
		private sealed class StopRange
		{
			public StopRange(TextDocument document, Int32 start, Int32 end)
			{
				this.StartAnchor = document.CreateAnchor(start);
				this.StartAnchor.MovementType = AnchorMovementType.BeforeInsertion;
				this.EndAnchor = document.CreateAnchor(end);
				this.EndAnchor.MovementType = AnchorMovementType.AfterInsertion;
			}

			public TextAnchor StartAnchor { get; }

			public TextAnchor EndAnchor { get; }

			public Boolean IsDeleted => this.StartAnchor.IsDeleted || this.EndAnchor.IsDeleted;

			public Int32 Start => this.StartAnchor.Offset;

			public Int32 End => Math.Max(this.EndAnchor.Offset, this.StartAnchor.Offset);
		}

		private sealed class Stop
		{
			public Stop(TextDocument document, SnippetStop stop, Int32 offset)
			{
				this.Index = stop.Index;
				this.LastText = stop.DefaultText;
				foreach (TextSpan span in stop.Occurrences)
				{
					this.Occurrences.Add(new StopRange(document, offset + span.Offset, offset + span.End));
				}
			}

			public Int32 Index { get; }

			public String LastText { get; set; }

			public List<StopRange> Occurrences { get; } = [];

			public StopRange Primary => this.Occurrences[0];

			public Boolean IsAlive => !this.Primary.IsDeleted;

			public void Release()
			{
				this.Occurrences.Clear();
			}
		}
	}
}