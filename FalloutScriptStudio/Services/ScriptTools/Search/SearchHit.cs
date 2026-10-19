namespace FalloutScriptStudio.Services.ScriptTools.Search
{
	using System;
	using Core.Compilation;

	/// <summary>
	/// A line that contains a match.
	/// </summary>
	/// <param name="FilePath">The full path of the file, or <see cref="BuildError.NoFile"/> for a document that was never saved.</param>
	/// <param name="Line">The 1-based line number.</param>
	/// <param name="Column">The 1-based column of the first match in the line.</param>
	/// <param name="Length">The length of the first match.</param>
	/// <param name="LineText">The text of the line.</param>
	public sealed record SearchHit(String FilePath, Int32 Line, Int32 Column, Int32 Length, String LineText)
	{
		/// <summary>The longest line text that is reported.</summary>
		public const Int32 MaxMessageLength = 500;

		/// <summary>Gets the text of the line without leading and trailing white space, shortened when very long.</summary>
		public String Message
		{
			get
			{
				String trimmed = this.LineText.Trim();
				return trimmed.Length > MaxMessageLength ? String.Concat(trimmed.AsSpan(0, MaxMessageLength), "...") : trimmed;
			}
		}

		/// <summary>Converts the hit to the entry shown in the output panel.</summary>
		/// <returns>A <see cref="BuildErrorType.Search"/> entry.</returns>
		public BuildError ToBuildError()
		{
			return new BuildError(BuildErrorType.Search, this.Message, this.FilePath, this.Line, this.Column, this.Length);
		}
	}
}