namespace FalloutScriptStudio.Editor.Controllers
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using FalloutScriptStudio.Core.TextTools;
	using FalloutScriptStudio.Editor.Rendering;
	using FalloutScriptStudio.Editor.Text;
	using ICSharpCode.AvalonEdit.Rendering;

	/// <summary>
	/// Highlights all occurrences of the selected text or word (legacy middle click / "Highlight" command) and shows
	/// the search matches reported by the application.
	/// </summary>
	public sealed class OccurrenceController : IDisposable
	{
		private readonly EditorContext _context;
		private readonly HighlightColorizer _colorizer;
		private readonly List<TextView> _views = [];
		private String? _highlighted;

		public OccurrenceController(EditorContext context)
		{
			this._context = context;
			this._colorizer = new HighlightColorizer(context.Document, context.Palette);
		}

		/// <summary>Gets the text whose occurrences are highlighted, or <see langword="null"/>.</summary>
		public String? HighlightedText => this._highlighted;

		/// <summary>Gets the number of highlighted occurrences.</summary>
		public Int32 OccurrenceCount => this._colorizer.OccurrenceCount;

		/// <summary>Gets the number of marked search matches.</summary>
		public Int32 MatchCount => this._colorizer.MatchCount;

		/// <summary>Gets the number of ranges that are marked as just inserted.</summary>
		public Int32 FlashCount => this._colorizer.FlashCount;

		public void Attach()
		{
			this.AddView(this._context.TextView);
			this._context.Document.TextChanged += this.OnTextChanged;

			String? remembered = this._context.ViewState.HighlightedText;
			if (!String.IsNullOrEmpty(remembered))
			{
				this.Highlight(remembered, remembered.All(c => WordFinder.IsWordChar(c)));
			}
		}

		public void Dispose()
		{
			this._context.Document.TextChanged -= this.OnTextChanged;
			foreach (TextView view in this._views)
			{
				view.LineTransformers.Remove(this._colorizer);
			}

			this._views.Clear();
			this._context.ViewState.HighlightedText = this._highlighted;
		}

		/// <summary>Shows the marks in one more text view over the same document (the second pane of a split view).</summary>
		/// <param name="view">The text view.</param>
		public void AddView(TextView view)
		{
			if (!this._views.Contains(view))
			{
				this._views.Add(view);
				view.LineTransformers.Add(this._colorizer);
				view.Redraw();
			}
		}

		/// <summary>Stops showing the marks in a text view.</summary>
		/// <param name="view">The text view.</param>
		public void RemoveView(TextView view)
		{
			if (this._views.Remove(view))
			{
				view.LineTransformers.Remove(this._colorizer);
			}
		}

		/// <summary>Applies new colors after a theme change.</summary>
		public void RefreshPalette()
		{
			this._colorizer.SetPalette(this._context.Palette);
			this.Redraw();
		}

		/// <summary>
		/// Highlights the occurrences of the selection, or of the word at an offset when nothing is selected;
		/// when that text is highlighted already the highlight is removed.
		/// </summary>
		/// <param name="offset">The offset of the word to use when nothing is selected, or -1 for the caret.</param>
		/// <param name="source">The pane whose selection and caret are used (the second pane of a split view); this controller's own pane by default.</param>
		public void ToggleHighlight(Int32 offset = -1, EditorContext? source = null)
		{
			EditorContext pane = source ?? this._context;
			String text;
			Boolean wholeWord;
			if (pane.Editor.SelectionLength > 0)
			{
				text = pane.Editor.SelectedText.Trim();
				wholeWord = text.Length > 0 && IsWholeWordSelection(pane);
			}
			else
			{
				Int32 position = offset >= 0 ? offset : pane.TextArea.Caret.Offset;
				if (!WordFinder.TryGetWordRangeNearCaret(this._context.Document, position, out Int32 start, out Int32 length))
				{
					this.Clear();
					return;
				}

				text = this._context.Document.GetText(start, length);
				wholeWord = true;
			}

			if (String.Equals(text, this._highlighted, StringComparison.Ordinal))
			{
				this.Clear();
			}
			else if (OccurrenceFinder.IsHighlightable(text))
			{
				this.Highlight(text, wholeWord);
			}
			else
			{
				this.Clear();
			}

			pane.TextArea.ClearSelection();
		}

		/// <summary>Highlights the occurrences of a text.</summary>
		/// <param name="text">The text.</param>
		/// <param name="wholeWord">Only highlight whole identifiers.</param>
		public void Highlight(String text, Boolean wholeWord)
		{
			this._highlighted = text;
			this._colorizer.SetFlash(Array.Empty<TextSpan>());
			this._colorizer.SetOccurrences(OccurrenceFinder.Find(this._context.Document.Text, text, wholeWord));
			this.Redraw();
		}

		/// <summary>Removes the highlighted occurrences and the mark of inserted text.</summary>
		public void Clear()
		{
			if (this._highlighted is null && this._colorizer.OccurrenceCount == 0 && this._colorizer.FlashCount == 0)
			{
				return;
			}

			this._highlighted = null;
			this._colorizer.SetOccurrences(Array.Empty<TextSpan>());
			this._colorizer.SetFlash(Array.Empty<TextSpan>());
			this.Redraw();
		}

		/// <summary>
		/// Marks a range of the text with the "inserted text" background (legacy <c>IncludeHighlight</c> after an <c>#include</c> was pasted).
		/// The mark replaces the highlighted occurrences and goes away with the next edit or highlight.
		/// </summary>
		/// <param name="offset">The offset of the range.</param>
		/// <param name="length">The length of the range.</param>
		public void HighlightInserted(Int32 offset, Int32 length)
		{
			this._highlighted = null;
			this._colorizer.SetOccurrences(Array.Empty<TextSpan>());
			this._colorizer.SetFlash(length > 0 ? new[] { new TextSpan(offset, length) } : Array.Empty<TextSpan>());
			this.Redraw();
		}

		/// <summary>Replaces the search match marks.</summary>
		/// <param name="matches">The matching ranges.</param>
		public void SetSearchMatches(IReadOnlyList<TextSpan> matches)
		{
			this._colorizer.SetMatches(matches);
			this.Redraw();
		}

		/// <summary>Removes the search match marks.</summary>
		public void ClearSearchMatches()
		{
			this._colorizer.SetMatches(Array.Empty<TextSpan>());
			this.Redraw();
		}

		private static Boolean IsWholeWordSelection(EditorContext pane)
		{
			Int32 start = pane.Editor.SelectionStart;
			Int32 length = pane.Editor.SelectionLength;
			if (!WordFinder.TryGetWordRange(pane.Document, start, out Int32 wordStart, out Int32 wordLength))
			{
				return false;
			}

			return wordStart == start && wordLength == length;
		}

		private void Redraw()
		{
			foreach (TextView view in this._views)
			{
				view.Redraw();
			}
		}

		private void OnTextChanged(Object? sender, EventArgs e)
		{
			this.Clear();
		}
	}
}