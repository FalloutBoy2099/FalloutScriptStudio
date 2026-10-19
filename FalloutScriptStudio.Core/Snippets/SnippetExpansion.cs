namespace FalloutScriptStudio.Core.Snippets
{
	using System;
	using System.Collections.Generic;

	/// <summary>The text a snippet expands to, with the places the caret goes to.</summary>
	/// <param name="Text">The text to insert: indented and with the line breaks of the document.</param>
	/// <param name="Stops">The tab stops in the order they are visited (the numbers 1, 2, 3...; the final position <c>$0</c> is not one of them).</param>
	/// <param name="FinalOffset">Where the caret ends: the place of <c>$0</c>, or the end of the text when the body has none.</param>
	public sealed record SnippetExpansion(String Text, IReadOnlyList<SnippetStop> Stops, Int32 FinalOffset);
}