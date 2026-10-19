namespace FalloutScriptStudio.Editor.Text
{
	using System;
	using ICSharpCode.AvalonEdit.Document;

	/// <summary>
	/// A <see cref="LexicalMap"/> of a <see cref="TextDocument"/> that follows the edits: a single edit is applied to the map by scanning the
	/// text around it, anything else rebuilds the map lazily from the whole text (a copy of the whole text of a big document is a large
	/// object that costs a full collection of the heap now and then, which one keystroke after another turns into a stutter).
	/// </summary>
	public sealed class DocumentLexicalMap : IDisposable
	{
		private readonly TextDocument _document;
		private LexicalMap? _map;
		private (Int32 Offset, Int32 Removed, Int32 Inserted)? _pending;

		public DocumentLexicalMap(TextDocument document)
		{
			this._document = document;
			this._document.Changed += this.OnDocumentChanged;
		}

		/// <summary>Gets the up-to-date map of the document.</summary>
		public LexicalMap Current
		{
			get
			{
				LexicalMap? map = this._map;
				if (map is not null && this._pending is { } edit)
				{
					map = LexicalMap.TryUpdate(map, this._document, edit.Offset, edit.Removed, edit.Inserted);
				}
				else if (map is not null)
				{
					return map;
				}

				this._pending = null;
				this._map = map ?? LexicalMap.Build(this._document.Text);
				return this._map;
			}
		}

		public void Dispose()
		{
			this._document.Changed -= this.OnDocumentChanged;
			this._map = null;
			this._pending = null;
		}

		private void OnDocumentChanged(Object? sender, DocumentChangeEventArgs e)
		{
			if (this._map is null)
			{
				return;
			}

			if (this._pending is null)
			{
				this._pending = (e.Offset, e.RemovalLength, e.InsertionLength);
			}
			else
			{
				// Two edits since the map was current: the second one is in the coordinates of the first, so start again from the whole text.
				this._map = null;
				this._pending = null;
			}
		}
	}
}