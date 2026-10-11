namespace FalloutScriptStudio.Core.TextTools
{
	using System;

	/// <summary>
	/// Result of pasting an <c>#include</c> line.
	/// </summary>
	/// <param name="Edit">The insertion.</param>
	/// <param name="Line">The 1-based line of the new <c>#include</c> in the edited text.</param>
	public sealed record IncludePasteResult(TextEdit Edit, Int32 Line);
}
