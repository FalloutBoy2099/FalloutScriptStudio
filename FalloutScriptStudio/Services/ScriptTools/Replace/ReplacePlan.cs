namespace FalloutScriptStudio.Services.ScriptTools.Replace
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;

	/// <summary>The outcome of the first phase of "Replace in Files": every replacement that would be made, grouped by file.</summary>
	/// <param name="Files">The files with matches, in path order (the open documents first).</param>
	/// <param name="Skipped">The files with matches that are left out.</param>
	/// <param name="FilesSearched">The number of texts and files that were looked at.</param>
	/// <param name="Truncated">Whether the run stopped listing because <see cref="ReplaceInFilesRequest.MaxMatches"/> was reached.</param>
	/// <param name="FallbackEncoding">The encoding the files that are not Unicode were read with; the second phase reads them the same way.</param>
	public sealed record ReplacePlan(IReadOnlyList<ReplaceFilePlan> Files, IReadOnlyList<ReplaceSkip> Skipped, Int32 FilesSearched, Boolean Truncated, Encoding FallbackEncoding)
	{
		/// <summary>Gets the number of replacements in the preview.</summary>
		public Int32 MatchCount => this.Files.Sum(file => file.Matches.Count);
	}
}