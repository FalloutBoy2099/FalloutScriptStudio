namespace FalloutScriptStudio.Workspace
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using ICSharpCode.AvalonEdit.Document;
	using Core.TextTools;

	/// <summary>Applies the text edits computed by the Core text tools to an AvalonEdit document.</summary>
	public static class TextDocumentEdits
	{
		/// <summary>
		/// Applies non-overlapping edits as one undoable step. The edits refer to the text the document has now.
		/// </summary>
		/// <param name="document">The document.</param>
		/// <param name="edits">The edits.</param>
		public static void ApplyEdits(this TextDocument document, IReadOnlyList<TextEdit> edits)
		{
			ArgumentNullException.ThrowIfNull(document);
			ArgumentNullException.ThrowIfNull(edits);
			if (edits.Count == 0)
			{
				return;
			}

			document.UndoStack.StartUndoGroup();
			try
			{
				foreach (TextEdit edit in edits.OrderByDescending(e => e.Offset))
				{
					document.Replace(edit.Offset, edit.Length, edit.NewText);
				}
			}
			finally
			{
				document.UndoStack.EndUndoGroup();
			}
		}
	}
}