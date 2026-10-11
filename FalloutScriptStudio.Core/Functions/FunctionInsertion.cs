namespace FalloutScriptStudio.Core.Functions
{
	using System;
	using FalloutScriptStudio.Core.TextTools;

	/// <summary>
	/// The text inserted into the editor when a function of the tree is clicked (legacy <c>FunctionsTree_NodeMouseClick</c>).
	/// </summary>
	/// <param name="Text">The text to insert at the caret (replacing the selection, if any).</param>
	/// <param name="FirstPlaceholder">The first <c>{placeholder}</c> inside <see cref="Text"/> (to select after the insertion), or <c>null</c>.</param>
	public sealed record FunctionInsertion(String Text, TextSpan? FirstPlaceholder)
	{
		/// <summary>
		/// Builds the insertion for the code of a function.
		/// </summary>
		/// <param name="code">The code as stored in the ini file (<see cref="FunctionNode.Code"/>).</param>
		/// <param name="caretColumn">The zero-based column of the caret; the lines after a <c>&lt;cr&gt;</c> are indented by that many spaces.</param>
		/// <param name="hasSelection">Whether the editor has a selection (no separating spaces are added then).</param>
		/// <param name="charBefore">The character before the caret, or <c>'\0'</c> at the start of the text.</param>
		/// <param name="charAfter">The character after the caret, or <c>'\0'</c> at the end of the text.</param>
		/// <param name="newLine">The line break of the document.</param>
		/// <returns>
		/// The insertion. A space is put in front of the code when it would touch a letter or digit before the caret, and after
		/// a single-line code when it would touch one after the caret.
		/// </returns>
		public static FunctionInsertion Create(String code, Int32 caretColumn, Boolean hasSelection, Char charBefore, Char charAfter, String newLine = "\r\n")
		{
			ArgumentNullException.ThrowIfNull(code);
			ArgumentNullException.ThrowIfNull(newLine);

			Boolean multiLine = code.Contains(FunctionCatalogParser.LineBreakMarker, StringComparison.Ordinal);
			String text = multiLine
				? code.Replace(FunctionCatalogParser.LineBreakMarker, newLine + new String(' ', Math.Max(caretColumn, 0)), StringComparison.Ordinal)
				: code;

			if (!hasSelection)
			{
				if (Char.IsLetterOrDigit(charBefore))
				{
					text = " " + text;
				}

				if (!multiLine && Char.IsLetterOrDigit(charAfter))
				{
					text += " ";
				}
			}

			Int32 open = text.IndexOf('{', StringComparison.Ordinal);
			TextSpan? placeholder = null;
			if (open >= 0)
			{
				Int32 close = text.IndexOf('}', open);
				placeholder = close < 0 ? new TextSpan(open, 1) : new TextSpan(open, close - open + 1);
			}

			return new FunctionInsertion(text, placeholder);
		}
	}
}
