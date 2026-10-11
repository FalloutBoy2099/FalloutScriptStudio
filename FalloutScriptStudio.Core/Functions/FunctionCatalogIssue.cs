namespace FalloutScriptStudio.Core.Functions
{
	using System;

	/// <summary>
	/// A problem found while reading a functions ini file; the offending line is skipped.
	/// </summary>
	/// <param name="Source">The file with the problem.</param>
	/// <param name="Line">The 1-based line number.</param>
	/// <param name="Message">What is wrong.</param>
	public sealed record FunctionCatalogIssue(FunctionSource Source, Int32 Line, String Message);
}
