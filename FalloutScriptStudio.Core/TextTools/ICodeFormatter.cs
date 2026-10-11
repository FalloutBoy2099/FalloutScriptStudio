namespace FalloutScriptStudio.Core.TextTools
{
	using System;

	/// <summary>
	/// Formats SSL code: puts spaces around operators and after commas (legacy <c>Utilities.FormattingCode</c>).
	/// </summary>
	public interface ICodeFormatter
	{
		/// <summary>Formats a piece of code.</summary>
		/// <param name="text">The code; lines may end with <c>\n</c> or <c>\r\n</c>.</param>
		/// <returns>The formatted code.</returns>
		String Format(String text);

		/// <summary>
		/// Formats the selected text, or the line of the caret when nothing is selected.
		/// </summary>
		/// <param name="text">The whole document text.</param>
		/// <param name="selection">The selection; an empty span is the caret position.</param>
		/// <returns>The edit, or <c>null</c> when formatting changes nothing.</returns>
		TextEdit? FormatSelectionOrLine(String text, TextSpan selection);

		/// <summary>
		/// Formats the part of the current line before the caret; used when the user types <c>;</c> (legacy <c>FormattingCodeSmart</c>).
		/// </summary>
		/// <param name="text">The whole document text.</param>
		/// <param name="caretOffset">The caret offset.</param>
		/// <returns>The edit and the new caret offset, or <c>null</c> when the text did not grow.</returns>
		SmartFormatResult? FormatSmart(String text, Int32 caretOffset);
	}
}
