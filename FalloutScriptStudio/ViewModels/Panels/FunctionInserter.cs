namespace FalloutScriptStudio.ViewModels.Panels
{
	using System;
	using FalloutScriptStudio.Core.Functions;
	using FalloutScriptStudio.Documents;
	using ICSharpCode.AvalonEdit.Document;

	/// <summary>Inserts the code of a function of the functions tree into the active editor at the caret (the Functions tab and the Help tab use it).</summary>
	public static class FunctionInserter
	{
		/// <summary>Inserts the code at the caret, or over the selection, and selects its first placeholder.</summary>
		/// <param name="editor">The active editor.</param>
		/// <param name="document">The active document.</param>
		/// <param name="code">The code as the functions tree stores it (<c>&lt;cr&gt;</c> stands for a line break).</param>
		/// <returns><see langword="true"/> when the code was inserted.</returns>
		public static Boolean Insert(IEditorSurface? editor, DocumentViewModel? document, String code)
		{
			ArgumentNullException.ThrowIfNull(code);
			if (editor is null || document is null || code.Length == 0)
			{
				return false;
			}

			TextDocument text = document.TextDocument;
			Int32 offset = editor.CaretOffset;
			Char before = offset > 0 && offset <= text.TextLength ? text.GetCharAt(offset - 1) : '\0';
			Char after = offset < text.TextLength ? text.GetCharAt(offset) : '\0';
			Boolean hasSelection = editor.SelectionLength > 0;
			FunctionInsertion insertion = FunctionInsertion.Create(code, editor.CaretColumn - 1, hasSelection, before, after);
			Int32 start = hasSelection ? editor.SelectionStart : offset;
			if (hasSelection)
			{
				editor.ReplaceSelection(insertion.Text);
			}
			else
			{
				editor.InsertText(insertion.Text);
			}

			if (insertion.FirstPlaceholder is { } placeholder)
			{
				editor.Select(start + placeholder.Offset, placeholder.Length);
			}

			editor.FocusEditor();
			return true;
		}
	}
}