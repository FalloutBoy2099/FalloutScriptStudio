namespace FalloutScriptStudio.Core.Building
{
	using System;

	/// <summary>An <c>#include</c> whose file cannot be determined, so that the dependencies of a script are not known for sure.</summary>
	/// <param name="FromFile">The file that holds the directive (or the file that could not be read).</param>
	/// <param name="Line">The 1-based line of the directive, or 0 when the file itself could not be read.</param>
	/// <param name="Include">The path as written in the directive; <see langword="null"/> when the directive does not name a file literally or the file could not be read.</param>
	public sealed record UnresolvedInclude(String FromFile, Int32 Line, String? Include);
}