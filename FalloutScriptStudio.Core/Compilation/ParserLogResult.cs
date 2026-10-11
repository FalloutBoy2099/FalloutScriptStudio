namespace FalloutScriptStudio.Core.Compilation
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// The processed output of the external script parser.
	/// </summary>
	/// <param name="Text">The log text prepared for display.</param>
	/// <param name="Errors">The parser errors and (if requested) warnings.</param>
	/// <param name="IncludeNotes">The "from file:line" notes that tell where an error in an included header was triggered.</param>
	public sealed record ParserLogResult(
		String Text,
		IReadOnlyList<BuildError> Errors,
		IReadOnlyList<BuildError> IncludeNotes);
}
