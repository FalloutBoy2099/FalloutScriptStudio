namespace FalloutScriptStudio.Core.Functions
{
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>
	/// The list of user keywords (macros of the user's functions) that the editor highlights; it replaces the
	/// <c>UserMacros</c> section of the legacy <c>User_SyntaxRules.xml</c> (<c>SyntaxFile.AddKeyWord</c> / <c>RemoveKeyWord</c>).
	/// </summary>
	public interface IUserKeywordStore
	{
		/// <summary>Occurs after the list changed.</summary>
		event EventHandler? Changed;

		/// <summary>Gets a snapshot of the keywords in the order they were added.</summary>
		IReadOnlyList<String> Keywords { get; }

		/// <summary>Reads the keywords from the store file; a missing or damaged file gives an empty list.</summary>
		/// <param name="cancellationToken">Cancels the operation.</param>
		/// <returns>A task that completes when the list is loaded.</returns>
		Task LoadAsync(CancellationToken cancellationToken = default);

		/// <summary>Adds a keyword and saves the file.</summary>
		/// <param name="keyword">The keyword: letters, digits and underscores (case-sensitive).</param>
		/// <param name="cancellationToken">Cancels the operation.</param>
		/// <returns><c>true</c> when the keyword was added; <c>false</c> when it is invalid or already present.</returns>
		Task<Boolean> AddAsync(String keyword, CancellationToken cancellationToken = default);

		/// <summary>Removes a keyword and saves the file.</summary>
		/// <param name="keyword">The keyword.</param>
		/// <param name="cancellationToken">Cancels the operation.</param>
		/// <returns><c>true</c> when the keyword was present.</returns>
		Task<Boolean> RemoveAsync(String keyword, CancellationToken cancellationToken = default);
	}
}
