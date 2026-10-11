namespace FalloutScriptStudio.Core.Parsing
{
	using System;

	/// <summary>
	/// A region of a script that can be collapsed in the editor.
	/// </summary>
	/// <remarks>
	/// Lines are 1-based and inclusive: the region starts at the beginning of <paramref name="StartLine"/> and ends at the end
	/// of <paramref name="EndLine"/> (the same convention as editor document lines).
	/// </remarks>
	/// <param name="StartLine">First line of the region.</param>
	/// <param name="EndLine">Last line of the region.</param>
	/// <param name="Kind">Kind of the region.</param>
	/// <param name="Title">Text displayed in place of the collapsed region. Procedure titles are the upper-cased name with a space on each side.</param>
	public sealed record FoldingRegion(Int32 StartLine, Int32 EndLine, FoldingKind Kind, String Title);
}
