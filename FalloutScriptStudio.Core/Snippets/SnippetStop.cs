namespace FalloutScriptStudio.Core.Snippets
{
	using System;
	using System.Collections.Generic;
	using TextTools;

	/// <summary>A tab stop of an expanded snippet: the places the caret goes to with Tab, in the coordinates of <see cref="SnippetExpansion.Text"/>.</summary>
	/// <param name="Index">The number of the stop (1, 2, 3...); the stops are visited in this order.</param>
	/// <param name="Occurrences">The places of the stop. The first one is the one the user edits; the others are mirrors that follow it.</param>
	/// <param name="DefaultText">The text the stop holds when the snippet is inserted.</param>
	public sealed record SnippetStop(Int32 Index, IReadOnlyList<TextSpan> Occurrences, String DefaultText)
	{
		/// <summary>Gets the place the user edits.</summary>
		public TextSpan Primary => this.Occurrences[0];
	}
}