namespace FalloutScriptStudio.Editor.Text
{
	using System;
	using ICSharpCode.AvalonEdit.Document;

	/// <summary>
	/// A <see cref="LexicalMap"/> of a <see cref="TextDocument"/> that is rebuilt lazily after the document changed.
	/// </summary>
	public sealed class DocumentLexicalMap : IDisposable
	{
		private readonly TextDocument _document;
		private LexicalMap? _map;

		public DocumentLexicalMap(TextDocument document)
		{
			this._document = document;
			this._document.Changed += this.OnDocumentChanged;
		}

		/// <summary>Gets the up-to-date map of the document.</summary>
		public LexicalMap Current => this._map ??= LexicalMap.Build(this._document.Text);

		public void Dispose()
		{
			this._document.Changed -= this.OnDocumentChanged;
			this._map = null;
		}

		private void OnDocumentChanged(Object? sender, DocumentChangeEventArgs e)
		{
			this._map = null;
		}
	}
}
