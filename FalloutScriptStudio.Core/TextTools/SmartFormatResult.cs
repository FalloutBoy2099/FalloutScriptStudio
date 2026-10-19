namespace FalloutScriptStudio.Core.TextTools
{
	using System;

	/// <summary>
	/// Result of formatting the part of the current line that precedes the caret.
	/// </summary>
	/// <param name="Edit">The replacement of the part of the line before the caret.</param>
	/// <param name="CaretOffset">The caret offset in the formatted text.</param>
	public sealed record SmartFormatResult(TextEdit Edit, Int32 CaretOffset);
}