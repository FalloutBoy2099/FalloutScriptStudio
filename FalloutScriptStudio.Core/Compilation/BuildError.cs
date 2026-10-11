namespace FalloutScriptStudio.Core.Compilation
{
	using System;

	/// <summary>
	/// A single entry of a build log, a parser log or a message file check.
	/// </summary>
	/// <param name="Type">The kind of the entry.</param>
	/// <param name="Message">The text of the entry.</param>
	/// <param name="FileName">The file the entry refers to (<c>none</c> when the tool did not report a file).</param>
	/// <param name="Line">The line number as reported by the tool (1-based).</param>
	/// <param name="Column">The column as reported by the tool, or -1 when unknown.</param>
	/// <param name="Length">The length of the affected text, or -1 when unknown.</param>
	public sealed record BuildError(
		BuildErrorType Type,
		String Message,
		String FileName,
		Int32 Line,
		Int32 Column = -1,
		Int32 Length = -1)
	{
		/// <summary>The file name the tools use when an entry has no file.</summary>
		public const String NoFile = "none";

		/// <inheritdoc/>
		public override String ToString()
		{
			return this.Message;
		}
	}
}
