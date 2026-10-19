namespace FalloutScriptStudio.Services.ToolWindows
{
	using System;
	using System.Collections.Generic;
	using FalloutScriptStudio.Core.Snippets;

	/// <summary>What the list of snippets shows.</summary>
	/// <param name="Snippets">The snippets to offer.</param>
	/// <param name="Surround">Whether the list is the one of Surround With (the snippets that use the selected text).</param>
	public sealed record SnippetPickerRequest(IReadOnlyList<Snippet> Snippets, Boolean Surround);
}