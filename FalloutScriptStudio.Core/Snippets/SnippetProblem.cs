namespace FalloutScriptStudio.Core.Snippets
{
	using System;

	/// <summary>A mistake in the body of a snippet. The expansion treats the mistaken part as plain text, so a snippet with a problem still works.</summary>
	/// <param name="Kind">What is wrong.</param>
	/// <param name="Offset">Where it is in the body (zero-based offset).</param>
	/// <param name="Length">How many characters it covers.</param>
	/// <param name="Text">The text of the mistaken part (the unknown variable name, for example).</param>
	public sealed record SnippetProblem(SnippetProblemKind Kind, Int32 Offset, Int32 Length, String Text);
}