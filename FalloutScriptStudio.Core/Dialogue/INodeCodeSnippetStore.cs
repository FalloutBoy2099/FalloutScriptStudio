namespace FalloutScriptStudio.Core.Dialogue
{
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>
	/// The user's saved node code snippets (legacy <c>CustomCode.ini</c>).
	/// </summary>
	public interface INodeCodeSnippetStore
	{
		/// <summary>Occurs after the list changed.</summary>
		event EventHandler? Changed;

		/// <summary>Gets a snapshot of the snippets in file order.</summary>
		IReadOnlyList<NodeCodeSnippet> Snippets { get; }

		/// <summary>Reads the snippets from the file; a missing file gives an empty list.</summary>
		/// <param name="cancellationToken">Cancels the operation.</param>
		/// <returns>A task that completes when the snippets are loaded.</returns>
		Task LoadAsync(CancellationToken cancellationToken = default);

		/// <summary>Adds a snippet (a snippet with the same name is replaced) and saves the file; the file is read first when it was not loaded yet.</summary>
		/// <param name="name">The name; <c>|</c> and line breaks are replaced because the file format uses them.</param>
		/// <param name="code">The code.</param>
		/// <param name="cancellationToken">Cancels the operation.</param>
		/// <returns><c>false</c> when the name or the code is empty, or when the existing file could not be read (it is then left untouched).</returns>
		Task<Boolean> AddAsync(String name, String code, CancellationToken cancellationToken = default);

		/// <summary>Removes a snippet and saves the file.</summary>
		/// <param name="name">The name.</param>
		/// <param name="cancellationToken">Cancels the operation.</param>
		/// <returns><c>true</c> when the snippet existed.</returns>
		Task<Boolean> RemoveAsync(String name, CancellationToken cancellationToken = default);
	}
}