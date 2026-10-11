namespace FalloutScriptStudio.Core.Parsing
{
	using System;

	/// <summary>
	/// A range of lines in a text. Lines are 1-based and inclusive; -1 means "not found".
	/// </summary>
	/// <param name="StartLine">First line of the range.</param>
	/// <param name="EndLine">Last line of the range.</param>
	public readonly record struct LineRange(Int32 StartLine, Int32 EndLine)
	{
		/// <summary>Gets a value indicating whether both ends of the range were found.</summary>
		public Boolean IsValid => this.StartLine > 0 && this.EndLine >= this.StartLine;
	}
}
