namespace FalloutScriptStudio.Core.TextTools
{
	using System;
	using System.Collections.Generic;
	using Formatting;

	/// <summary>
	/// Formats SSL code. Without options it puts spaces around operators and after commas (legacy <c>Utilities.FormattingCode</c>); with
	/// <see cref="FormatOptions"/> it can also re-indent, place <c>begin</c> / <c>else</c>, wrap long argument lists and more.
	/// </summary>
	public interface ICodeFormatter
	{
		/// <summary>Formats a piece of code.</summary>
		/// <param name="text">The code; lines may end with <c>\n</c> or <c>\r\n</c>.</param>
		/// <returns>The formatted code.</returns>
		String Format(String text);

		/// <summary>Formats a piece of code in a given style.</summary>
		/// <param name="text">The code; lines may end with <c>\n</c> or <c>\r\n</c>.</param>
		/// <param name="options">The style.</param>
		/// <returns>The formatted code; formatting it again changes nothing.</returns>
		String Format(String text, FormatOptions options);

		/// <summary>
		/// Formats the selected text, or the line of the caret when nothing is selected.
		/// </summary>
		/// <param name="text">The whole document text.</param>
		/// <param name="selection">The selection; an empty span is the caret position.</param>
		/// <returns>The edit, or <c>null</c> when formatting changes nothing.</returns>
		TextEdit? FormatSelectionOrLine(String text, TextSpan selection);

		/// <summary>
		/// Formats the selected lines, or the line of the caret when nothing is selected, in a given style. The structure of the whole
		/// document is taken into account (the indentation of a line depends on the blocks around it) and only the changes that touch the
		/// selected lines are returned.
		/// </summary>
		/// <param name="text">The whole document text.</param>
		/// <param name="selection">The selection; an empty span is the caret position.</param>
		/// <param name="options">The style.</param>
		/// <returns>The edits in the coordinates of <paramref name="text"/>; empty when formatting changes nothing.</returns>
		IReadOnlyList<TextEdit> GetSelectionEdits(String text, TextSpan selection, FormatOptions options);

		/// <summary>Formats a whole document and returns the lines that change.</summary>
		/// <param name="text">The document text.</param>
		/// <param name="options">The style.</param>
		/// <returns>The edits in the coordinates of <paramref name="text"/>; empty when formatting changes nothing.</returns>
		IReadOnlyList<TextEdit> GetDocumentEdits(String text, FormatOptions options);

		/// <summary>
		/// Formats the part of the current line before the caret; used when the user types <c>;</c> (legacy <c>FormattingCodeSmart</c>).
		/// </summary>
		/// <param name="text">The whole document text.</param>
		/// <param name="caretOffset">The caret offset.</param>
		/// <returns>The edit and the new caret offset, or <c>null</c> when the text did not grow.</returns>
		SmartFormatResult? FormatSmart(String text, Int32 caretOffset);

		/// <summary>Formats the part of the current line before the caret with the spacing rules of a style.</summary>
		/// <param name="text">The whole document text.</param>
		/// <param name="caretOffset">The caret offset.</param>
		/// <param name="options">The style (only its spacing rules count).</param>
		/// <returns>The edit and the new caret offset, or <c>null</c> when the text did not grow.</returns>
		SmartFormatResult? FormatSmart(String text, Int32 caretOffset, FormatOptions options);
	}
}