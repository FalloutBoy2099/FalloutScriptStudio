namespace FalloutScriptStudio.Core.Snippets
{
	using System;

	/// <summary>What was found in a snippet file.</summary>
	/// <param name="Status">The outcome.</param>
	/// <param name="File">The content (an empty file when nothing could be read).</param>
	/// <param name="Error">Why the file is damaged or unreadable, or <see langword="null"/>.</param>
	public sealed record SnippetFileReadResult(SnippetFileStatus Status, SnippetFile File, String? Error = null);
}