namespace FalloutScriptStudio.Core.Analysis
{
	using System;

	/// <summary>A variable that is declared but never read, or never used at all.</summary>
	/// <param name="Name">The name as written.</param>
	/// <param name="Kind">What the variable is.</param>
	/// <param name="Status">Whether it is never used or only assigned.</param>
	/// <param name="ProcedureName">The procedure that declares it, or <see langword="null"/> for a script level variable.</param>
	/// <param name="Offset">The offset of the name in the declaration.</param>
	/// <param name="Line">The 1-based line of the name.</param>
	/// <param name="Column">The 1-based column of the name.</param>
	/// <param name="Length">The length of the name.</param>
	/// <param name="CanRemove">Whether the declaration can be removed without changing what the script does.</param>
	public sealed record UnusedVariable(
		String Name,
		VariableDeclarationKind Kind,
		UnusedVariableStatus Status,
		String? ProcedureName,
		Int32 Offset,
		Int32 Line,
		Int32 Column,
		Int32 Length,
		Boolean CanRemove);
}
