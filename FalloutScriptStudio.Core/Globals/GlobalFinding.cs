namespace FalloutScriptStudio.Core.Globals
{
	using System;

	/// <summary>A suspicious use of a global variable, located at one of its uses.</summary>
	/// <param name="Kind">What is suspicious.</param>
	/// <param name="Severity">How serious it is.</param>
	/// <param name="Variable">The text to show for the variable (<see cref="GlobalVariable.DisplayName"/>).</param>
	/// <param name="Other">The other variable of a pair (the name to suggest for <see cref="GlobalFindingKind.SimilarName"/>, the colliding name for <see cref="GlobalFindingKind.PrefixCollision"/>), or <see langword="null"/>.</param>
	/// <param name="Number">A number the message states: the length of the name for <see cref="GlobalFindingKind.NameLength"/>, the number of uses otherwise.</param>
	/// <param name="File">The full path of the file of the use the finding points at.</param>
	/// <param name="Line">The 1-based line of that use.</param>
	/// <param name="Procedure">The procedure of that use, or <see langword="null"/>.</param>
	public sealed record GlobalFinding(
		GlobalFindingKind Kind,
		GlobalFindingSeverity Severity,
		String Variable,
		String? Other,
		Int32 Number,
		String File,
		Int32 Line,
		String? Procedure);
}