namespace FalloutScriptStudio.Core.Refactoring
{
	using System;

	/// <summary>
	/// The text of one file taking part in a refactoring: an open editor tab or a project file read from disk.
	/// </summary>
	/// <param name="Path">The full path of the file (compared case-insensitively).</param>
	/// <param name="Text">The current text of the file.</param>
	/// <param name="IsOpen">Whether the file is open in an editor (the edits then go to the editor instead of the disk).</param>
	public sealed record RefactorDocument(String Path, String Text, Boolean IsOpen = false);
}
