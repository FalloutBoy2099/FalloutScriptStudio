namespace FalloutScriptStudio.Workspace
{
	using System;
	using System.Text;
	using FalloutScriptStudio.Documents;
	using ICSharpCode.AvalonEdit.Document;

	/// <summary>Creates document view models and keeps their modified flag in sync with the undo stack.</summary>
	internal static class DocumentFactory
	{
		/// <summary>Creates a document for a path (or an unsaved one when <paramref name="path"/> is <see langword="null"/>).</summary>
		public static DocumentViewModel Create(String? path, String text, Encoding encoding)
		{
			TextDocument textDocument = new TextDocument(text);
			textDocument.UndoStack.ClearAll();
			DocumentViewModel document = new DocumentViewModel(path, DocumentViewModel.KindFromPath(path), textDocument)
			{
				Encoding = encoding,
			};
			textDocument.UndoStack.MarkAsOriginalFile();
			textDocument.UndoStack.PropertyChanged += (_, e) =>
			{
				if (e.PropertyName == nameof(UndoStack.IsOriginalFile))
				{
					UpdateModified(document);
				}
			};
			textDocument.TextChanged += (_, _) => UpdateModified(document);
			return document;
		}

		private static void UpdateModified(DocumentViewModel document)
		{
			document.IsModified = document.FilePath is null || !document.TextDocument.UndoStack.IsOriginalFile;
		}
	}
}
