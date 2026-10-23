namespace FalloutScriptStudio.Core.Messages
{
	using System;

	/// <summary>Something about a whole file of a comparison.</summary>
	/// <param name="Kind">What the note says.</param>
	/// <param name="Count">The number of lines or numbers it is about; 0 when it is not a count.</param>
	/// <param name="Detail">Extra text that is not translated (the names of the two encodings), or <see langword="null"/>.</param>
	public sealed record MessageComparisonNote(MessageComparisonNoteKind Kind, Int32 Count, String? Detail = null);
}