namespace FalloutScriptStudio.Core.Snippets
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// The snippets in effect and the files they are stored in: the built-in snippets, the user's <c>snippets.json</c> and the
	/// <c>fss-snippets.json</c> of the active project. A change of one of the files (also made by another program) is noticed at the next
	/// request.
	/// </summary>
	public interface ISnippetService
	{
		/// <summary>Raised after the snippets changed: a file was saved here, or <see cref="Reload"/> was called.</summary>
		event EventHandler? Changed;

		/// <summary>Gets the path of the file of the user's snippets.</summary>
		String UserFilePath { get; }

		/// <summary>Gets the path of the file of the snippets of the active project, or <see langword="null"/> when no project is open.</summary>
		String? ProjectFilePath { get; }

		/// <summary>Gets the snippets in effect, ordered by trigger: the project's over the user's over the built-in ones.</summary>
		/// <returns>The snippets that are enabled.</returns>
		IReadOnlyList<Snippet> GetSnippets();

		/// <summary>Reads (again, when the file changed) the file of a source.</summary>
		/// <param name="source">The user's file or the project's file.</param>
		/// <returns>What was found.</returns>
		SnippetFileReadResult GetFile(SnippetSource source);

		/// <summary>Saves the snippets of a source. The properties of the file this version does not know are kept.</summary>
		/// <param name="source">The user's file or the project's file.</param>
		/// <param name="snippets">The snippets of the file.</param>
		/// <param name="error">Why the file was not written.</param>
		/// <returns><see langword="true"/> when the file was written; <see langword="false"/> when it cannot be read (it is never overwritten), when there is no project, or when the write failed.</returns>
		Boolean TrySave(SnippetSource source, IReadOnlyList<SnippetDefinition> snippets, out String? error);

		/// <summary>Forgets what was read, so that the files are read again.</summary>
		void Reload();
	}
}