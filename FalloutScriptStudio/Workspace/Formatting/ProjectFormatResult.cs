namespace FalloutScriptStudio.Workspace.Formatting
{
	using System;

	/// <summary>The outcome of the application of the formatting.</summary>
	/// <param name="Formatted">The number of files that were formatted.</param>
	/// <param name="Skipped">The number of files that were left alone (each one is a problem in the list of problems).</param>
	/// <param name="Cancelled">Whether the user cancelled before every file was done.</param>
	public sealed record ProjectFormatResult(Int32 Formatted, Int32 Skipped, Boolean Cancelled);
}