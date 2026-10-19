namespace FalloutScriptStudio.Services.ScriptTools.Replace
{
	using System;

	/// <summary>A text that is replaced in memory: an open document, with the unsaved edits it holds.</summary>
	/// <param name="Path">The full path of the file, or <see cref="FalloutScriptStudio.Core.Compilation.BuildError.NoFile"/> for a document that was never saved.</param>
	/// <param name="Text">The current text.</param>
	/// <param name="Owner">The object the text belongs to (the document); the applier finds it again through this.</param>
	public sealed record ReplaceTextSource(String Path, String Text, Object Owner);
}