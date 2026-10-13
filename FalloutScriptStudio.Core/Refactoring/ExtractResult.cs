namespace FalloutScriptStudio.Core.Refactoring
{
	using System;
	using System.Collections.Generic;
	using FalloutScriptStudio.Core.TextTools;

	/// <summary>The edits that extract a block into a new procedure.</summary>
	/// <param name="Edits">The edits in the coordinates of the original text; they never overlap.</param>
	/// <param name="NewText">The text after applying the edits.</param>
	/// <param name="ProcedureLine">The 1-based line of the new procedure in <paramref name="NewText"/>.</param>
	/// <param name="CallLine">The 1-based line of the call that replaced the block in <paramref name="NewText"/>.</param>
	public sealed record ExtractResult(IReadOnlyList<TextEdit> Edits, String NewText, Int32 ProcedureLine, Int32 CallLine);
}
