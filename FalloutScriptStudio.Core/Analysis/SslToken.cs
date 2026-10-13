namespace FalloutScriptStudio.Core.Analysis
{
	using System;

	/// <summary>A token of an SSL script: a range of the text with its kind. Comments and white space are not tokens.</summary>
	/// <param name="Kind">The kind of the token.</param>
	/// <param name="Offset">The zero-based offset of the first character.</param>
	/// <param name="Length">The number of characters.</param>
	/// <param name="Line">The 1-based line of the first character.</param>
	/// <param name="InDirective">Whether the token belongs to a preprocessor line (the directive token itself and everything up to the end of the logical line).</param>
	public readonly record struct SslToken(SslTokenKind Kind, Int32 Offset, Int32 Length, Int32 Line, Boolean InDirective)
	{
		/// <summary>Gets the offset just after the last character.</summary>
		public Int32 End => this.Offset + this.Length;
	}
}
