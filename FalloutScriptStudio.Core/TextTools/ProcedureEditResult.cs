namespace FalloutScriptStudio.Core.TextTools
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// Result of a procedure level text operation (insert, delete, move).
	/// </summary>
	/// <param name="Edits">The edits, in the coordinates of the original text; they never overlap.</param>
	/// <param name="NewText">The text after applying the edits.</param>
	public sealed record ProcedureEditResult(IReadOnlyList<TextEdit> Edits, String NewText)
	{
		/// <summary>Gets the 1-based line in <see cref="NewText"/> where the caret should be placed (the first body line of an inserted procedure), or -1.</summary>
		public Int32 FocusLine { get; init; } = -1;

		/// <summary>Gets the forward declaration line that was removed together with the procedure, or <c>null</c>.</summary>
		public String? RemovedDeclaration { get; init; }

		/// <summary>Gets the removed text of the procedure (from its <c>procedure</c> line to its <c>end</c> line), or <c>null</c>.</summary>
		public String? RemovedBody { get; init; }
	}
}