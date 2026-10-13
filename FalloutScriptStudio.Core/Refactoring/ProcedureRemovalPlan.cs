namespace FalloutScriptStudio.Core.Refactoring
{
	using System;
	using System.Collections.Generic;
	using FalloutScriptStudio.Core.TextTools;

	/// <summary>What removing a procedure from a script would do.</summary>
	/// <param name="Name">The name of the procedure as written.</param>
	/// <param name="FirstLine">The 1-based first line that is removed (the first line of the comment block above the procedure, or its <c>procedure</c> line).</param>
	/// <param name="LastLine">The 1-based line of the closing <c>end</c>.</param>
	/// <param name="PreviewText">The text that would be removed: the comment block and the procedure.</param>
	/// <param name="DeclarationText">The forward declaration that would be removed too, or <see langword="null"/> when there is none.</param>
	/// <param name="IsExported">Whether the procedure is <c>export</c>ed, so that other scripts may use it.</param>
	/// <param name="Edits">The edits in the coordinates of the text the plan was made for.</param>
	public sealed record ProcedureRemovalPlan(
		String Name,
		Int32 FirstLine,
		Int32 LastLine,
		String PreviewText,
		String? DeclarationText,
		Boolean IsExported,
		IReadOnlyList<TextEdit> Edits);
}
