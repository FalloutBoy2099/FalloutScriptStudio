namespace FalloutScriptStudio.Core.Configuration
{
	using System;

	/// <summary>
	/// Why the content of a project file could not be used: the explanation of the JSON parser and where it stopped.
	/// </summary>
	/// <param name="Message">The explanation, without the technical position suffix of the parser.</param>
	/// <param name="Line">The 1-based line, or -1 when it is not known.</param>
	/// <param name="Column">The 1-based column, or -1 when it is not known.</param>
	public sealed record ProjectFileError(String Message, Int32 Line = -1, Int32 Column = -1);
}
