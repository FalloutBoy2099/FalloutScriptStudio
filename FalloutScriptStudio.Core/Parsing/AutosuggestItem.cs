namespace FalloutScriptStudio.Core.Parsing
{
	using System;

	/// <summary>
	/// One entry of the autocomplete list (replaces the legacy <c>"name|description|T"</c> strings).
	/// </summary>
	/// <param name="Name">The text to insert.</param>
	/// <param name="Description">The description shown in the tooltip next to the list.</param>
	/// <param name="Kind">What the entry represents.</param>
	public sealed record AutosuggestItem(String Name, String Description, AutosuggestKind Kind);
}
