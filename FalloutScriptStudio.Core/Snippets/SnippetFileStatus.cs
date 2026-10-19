namespace FalloutScriptStudio.Core.Snippets
{
	/// <summary>The outcome of reading a snippet file.</summary>
	public enum SnippetFileStatus
	{
		/// <summary>There is no file: no snippets.</summary>
		Missing = 0,

		/// <summary>The file was read.</summary>
		Loaded = 1,

		/// <summary>The content is not a valid snippet file; a <c>.corrupt</c> copy is kept and the file counts as empty until the user saves.</summary>
		Damaged = 2,

		/// <summary>The file exists but cannot be read; it is never overwritten.</summary>
		Unreadable = 3,
	}
}