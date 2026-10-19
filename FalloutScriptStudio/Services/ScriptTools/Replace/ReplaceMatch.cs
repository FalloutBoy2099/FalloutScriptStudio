namespace FalloutScriptStudio.Services.ScriptTools.Replace
{
	using System;

	/// <summary>One replacement of a preview: where the match is, what it is, and the line around it before and after the replacement.</summary>
	/// <param name="Id">The position of the match in its file plan.</param>
	/// <param name="Line">The 1-based line number.</param>
	/// <param name="Column">The 1-based column of the match.</param>
	/// <param name="Offset">The 0-based offset of the match in the text of the file.</param>
	/// <param name="Length">The length of the match.</param>
	/// <param name="Matched">The text that is replaced.</param>
	/// <param name="Replacement">The text that replaces it (the groups of a regular expression expanded).</param>
	/// <param name="Prefix">The part of the line before the match (cut when very long).</param>
	/// <param name="Suffix">The part of the line after the match (cut when very long).</param>
	public sealed record ReplaceMatch(Int32 Id, Int32 Line, Int32 Column, Int32 Offset, Int32 Length, String Matched, String Replacement, String Prefix, String Suffix)
	{
		/// <summary>Gets the line as it is now.</summary>
		public String Before => this.Prefix + this.Matched + this.Suffix;

		/// <summary>Gets the line as it will be after the replacement.</summary>
		public String After => this.Prefix + this.Replacement + this.Suffix;
	}
}