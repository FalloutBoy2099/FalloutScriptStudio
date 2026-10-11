namespace FalloutScriptStudio.Core.Refactoring
{
	using System;
	using FalloutScriptStudio.Core.TextTools;

	/// <summary>
	/// One occurrence found by a rename preview (legacy <c>Refactor.PreviewMatch</c>).
	/// </summary>
	/// <param name="FilePath">The file that holds the occurrence.</param>
	/// <param name="IsOpenDocument">Whether the file is open in an editor.</param>
	/// <param name="Span">The occurrence in the text of the file.</param>
	/// <param name="Line">The 1-based line of the occurrence.</param>
	/// <param name="PreviewText">The text from the occurrence to the end of its line, without trailing white space.</param>
	public sealed record PreviewMatch(String FilePath, Boolean IsOpenDocument, TextSpan Span, Int32 Line, String PreviewText);
}
