namespace FalloutScriptStudio.Workspace.Formatting
{
	using System;

	/// <summary>The progress of a preview or of the application of the formatting.</summary>
	/// <param name="Index">The number of files done.</param>
	/// <param name="Total">The number of files.</param>
	/// <param name="Name">The file being looked at.</param>
	public sealed record FormatProgress(Int32 Index, Int32 Total, String Name);
}