namespace FalloutScriptStudio.Editor.Controllers
{
	using System;
	using System.Collections.Generic;
	using System.Reflection;
	using ICSharpCode.AvalonEdit.Document;
	using ICSharpCode.AvalonEdit.Folding;
	using ICSharpCode.AvalonEdit.Rendering;

	/// <summary>
	/// Stops AvalonEdit from drawing a whole procedure again every time a character is typed in it.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A <see cref="FoldingSection"/> that contains an edit is told that it changed, and then asks the text views to redraw all its
	/// lines, folded or not. Typing in a procedure that is longer than the window therefore lays out every visible line again instead
	/// of the one line that was edited: 30 to 40 ms per keystroke instead of 5. Unfolded sections need no such redraw, because the
	/// text view already lays out the lines that changed and the folding margin refreshes itself whenever the visual lines change.
	/// </para>
	/// <para>
	/// The folding manager keeps the text views it draws in a private list. While a document change is applied and no folded section is
	/// affected by it, this class takes the views out of that list, and puts them back when the change is complete. When the list cannot be found (another
	/// version of the library) nothing is done and the library behaves as usual, only slower.
	/// </para>
	/// </remarks>
	internal sealed class FoldingRedrawGate : IDisposable
	{
		private static readonly FieldInfo? _viewsField = typeof(FoldingManager).GetField("textViews", BindingFlags.Instance | BindingFlags.NonPublic);

		private readonly FoldingManager _manager;
		private readonly TextDocument _document;
		private readonly List<TextView> _views;
		private TextView[]? _held;

		private FoldingRedrawGate(FoldingManager manager, TextDocument document, List<TextView> views)
		{
			this._manager = manager;
			this._document = document;
			this._views = views;
			this._document.Changing += this.OnChanging;
			this._document.UpdateFinished += this.OnUpdateFinished;
		}

		/// <summary>Gets a value indicating whether the views are held back at this moment (for tests).</summary>
		internal Boolean IsHolding => this._held is not null;

		/// <summary>Starts watching the changes of a document.</summary>
		/// <param name="manager">The folding manager of a text area that shows the document.</param>
		/// <param name="document">The document.</param>
		/// <returns>The gate, or <see langword="null"/> when the folding manager cannot be controlled.</returns>
		public static FoldingRedrawGate? TryCreate(FoldingManager manager, TextDocument document)
		{
			return _viewsField?.GetValue(manager) is List<TextView> views ? new FoldingRedrawGate(manager, document, views) : null;
		}

		/// <summary>Stops watching; the views are given back to the folding manager.</summary>
		public void Dispose()
		{
			this._document.Changing -= this.OnChanging;
			this._document.UpdateFinished -= this.OnUpdateFinished;
			this.Release();
		}

		private void OnChanging(Object? sender, DocumentChangeEventArgs e)
		{
			if (this._held is not null || this._views.Count == 0 || this.TouchesFoldedSection(e))
			{
				return;
			}

			this._held = this._views.ToArray();
			this._views.Clear();
		}

		private void OnUpdateFinished(Object? sender, EventArgs e)
		{
			this.Release();
		}

		private void Release()
		{
			TextView[]? held = this._held;
			this._held = null;
			if (held is not null)
			{
				this._views.AddRange(held);
			}
		}

		/// <summary>Determines whether a change is made in, or reaches into, a folded section (the lines hidden by it have to be redrawn then).</summary>
		private Boolean TouchesFoldedSection(DocumentChangeEventArgs e)
		{
			Int32 start = e.Offset;
			Int32 end = e.Offset + e.RemovalLength;
			foreach (FoldingSection section in this._manager.GetFoldingsContaining(start))
			{
				if (section.IsFolded)
				{
					return true;
				}
			}

			Int32 offset = start;
			while (this._manager.GetNextFolding(offset) is { } next && next.StartOffset <= end)
			{
				if (next.IsFolded)
				{
					return true;
				}

				offset = next.StartOffset + 1;
			}

			return false;
		}
	}
}