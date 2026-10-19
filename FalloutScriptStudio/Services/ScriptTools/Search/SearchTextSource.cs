namespace FalloutScriptStudio.Services.ScriptTools.Search
{
	using System;

	/// <summary>
	/// A text that is searched from memory (an open document, including unsaved edits).
	/// </summary>
	/// <param name="Path">The full path of the file, or <c>none</c> for a document that was never saved.</param>
	/// <param name="Text">The current text.</param>
	public sealed record SearchTextSource(String Path, String Text);
}