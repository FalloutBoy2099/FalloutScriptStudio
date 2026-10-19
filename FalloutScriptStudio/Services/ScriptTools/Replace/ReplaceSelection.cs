namespace FalloutScriptStudio.Services.ScriptTools.Replace
{
	using System;
	using System.Collections.Generic;

	/// <summary>The replacements of one file that the user ticked in the preview.</summary>
	/// <param name="File">The file plan.</param>
	/// <param name="Matches">The ticked replacements (a subset of the matches of <paramref name="File"/>), in file order.</param>
	public sealed record ReplaceSelection(ReplaceFilePlan File, IReadOnlyList<ReplaceMatch> Matches);
}