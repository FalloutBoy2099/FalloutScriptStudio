namespace FalloutScriptStudio.Editor.Controllers
{
	using System;
	using FalloutScriptStudio.Editor.Rendering;
	using FalloutScriptStudio.Editor.Text;
	using ICSharpCode.AvalonEdit.Rendering;

	/// <summary>Highlights the bracket next to the caret together with its partner.</summary>
	public sealed class BracketController : IDisposable
	{
		private readonly EditorContext _context;
		private readonly BracketHighlightRenderer _renderer;

		public BracketController(EditorContext context)
		{
			this._context = context;
			this._renderer = new BracketHighlightRenderer(context.Palette);
		}

		/// <summary>Gets the currently highlighted pair.</summary>
		public BracketPair? Pair => this._renderer.Pair;

		public void Attach()
		{
			this._context.TextView.BackgroundRenderers.Add(this._renderer);
			this._context.TextArea.Caret.PositionChanged += this.OnCaretPositionChanged;
			this._context.Document.TextChanged += this.OnCaretPositionChanged;
			this.Update();
		}

		public void Dispose()
		{
			this._context.TextArea.Caret.PositionChanged -= this.OnCaretPositionChanged;
			this._context.Document.TextChanged -= this.OnCaretPositionChanged;
			this._context.TextView.BackgroundRenderers.Remove(this._renderer);
		}

		/// <summary>Applies new colors after a theme change.</summary>
		public void RefreshPalette()
		{
			this._renderer.SetPalette(this._context.Palette);
			this._context.TextView.InvalidateLayer(KnownLayer.Selection);
		}

		/// <summary>
		/// Moves the caret to the bracket that matches the one next to it (legacy <c>GotoMatchingBrace</c>): to the place after the closing
		/// bracket, or, when the caret is already there, to the place after the opening bracket.
		/// </summary>
		/// <returns><see langword="true"/> when the caret was moved.</returns>
		public Boolean GoToMatchingBracket()
		{
			if (this._context.IsMessage)
			{
				return false;
			}

			Int32 caret = this._context.TextArea.Caret.Offset;
			BracketPair? pair = BracketMatcher.FindPair(this._context.Document, () => this._context.Lexical.Current, caret);
			if (pair is not BracketPair found)
			{
				return false;
			}

			this._context.TextArea.ClearSelection();
			this._context.TextArea.Caret.Offset = caret == found.CloseOffset + 1 ? found.OpenOffset + 1 : found.CloseOffset + 1;
			this._context.TextArea.Caret.BringCaretToView();
			return true;
		}

		/// <summary>Recomputes the highlighted pair for the current caret.</summary>
		public void Update()
		{
			BracketPair? pair = null;
			if (!this._context.IsMessage)
			{
				pair = BracketMatcher.FindPair(this._context.Document, () => this._context.Lexical.Current, this._context.TextArea.Caret.Offset);
			}

			if (this._renderer.SetPair(pair))
			{
				this._context.TextView.InvalidateLayer(KnownLayer.Selection);
			}
		}

		private void OnCaretPositionChanged(Object? sender, EventArgs e)
		{
			this.Update();
		}
	}
}
