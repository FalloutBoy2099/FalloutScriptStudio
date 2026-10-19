namespace FalloutScriptStudio.Services.ScriptTools.Replace
{
	using System;

	/// <summary>A file that has matches but is left out of the preview.</summary>
	/// <param name="Path">The full path of the file.</param>
	/// <param name="Reason">Why it is left out.</param>
	/// <param name="Matches">The number of matches it has, or 0 when they are not known (the file could not be read).</param>
	public sealed record ReplaceSkip(String Path, ReplaceSkipReason Reason, Int32 Matches);
}