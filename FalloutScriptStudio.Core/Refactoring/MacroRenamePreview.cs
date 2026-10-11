namespace FalloutScriptStudio.Core.Refactoring
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using FalloutScriptStudio.Core.Parsing;

	/// <summary>
	/// The occurrences of a macro name in a set of files, to be reviewed before a (global) macro is renamed.
	/// </summary>
	/// <param name="Macro">The macro.</param>
	/// <param name="NewName">The new name.</param>
	/// <param name="Documents">The files that contain at least one occurrence.</param>
	/// <param name="Matches">The occurrences, grouped by file in file order.</param>
	public sealed record MacroRenamePreview(ScriptMacro Macro, String NewName, IReadOnlyList<RefactorDocument> Documents, IReadOnlyList<PreviewMatch> Matches)
	{
		/// <summary>Gets the occurrences of one file.</summary>
		/// <param name="path">The full path of the file.</param>
		/// <returns>The occurrences in text order.</returns>
		public IEnumerable<PreviewMatch> GetMatches(String path)
		{
			return this.Matches.Where(m => String.Equals(m.FilePath, path, StringComparison.OrdinalIgnoreCase));
		}
	}
}
