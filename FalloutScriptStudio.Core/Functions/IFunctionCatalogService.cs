namespace FalloutScriptStudio.Core.Functions
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>
	/// Loads the functions tree from the ini files and edits the user's functions file (legacy <c>Functions</c> class).
	/// </summary>
	/// <remarks>
	/// Every edit re-reads the file, checks that the node is still where it was read, changes the lines, writes the file
	/// atomically and reloads <see cref="Catalog"/>; nodes of the old catalog must then be replaced by the new ones
	/// (<see cref="FunctionCatalog.FindEquivalent"/> finds the node that has the same line and text).
	/// Edits are serialized, so the methods may be called from any thread.
	/// </remarks>
	public interface IFunctionCatalogService
	{
		/// <summary>Occurs after <see cref="Catalog"/> was replaced (a load or a successful edit).</summary>
		event EventHandler? CatalogChanged;

		/// <summary>Gets the current catalog (empty until the first <see cref="LoadAsync"/>).</summary>
		FunctionCatalog Catalog { get; }

		/// <summary>
		/// Reads both ini files. A missing user file is created from a template; a missing built-in file gives a catalog with an issue.
		/// </summary>
		/// <param name="cancellationToken">Cancels the operation.</param>
		/// <returns>The new catalog.</returns>
		Task<FunctionCatalog> LoadAsync(CancellationToken cancellationToken = default);

		/// <summary>Determines whether a function can be added to (or after) a node: only in the user's part of the tree.</summary>
		/// <param name="node">The selected node.</param>
		/// <returns><c>true</c> for user categories and user functions.</returns>
		Boolean CanAddFunction(FunctionNode node);

		/// <summary>Determines whether a function can be edited: any function (editing a built-in one changes <c>functions.ini</c>).</summary>
		/// <param name="node">The selected node.</param>
		/// <returns><c>true</c> for functions.</returns>
		Boolean CanEditFunction(FunctionNode node);

		/// <summary>Determines whether a sub category can be added to a node: user categories above the deepest level.</summary>
		/// <param name="node">The selected node.</param>
		/// <returns><c>true</c> when the node is a user category of depth 0 or 1.</returns>
		Boolean CanAddCategory(FunctionNode node);

		/// <summary>Determines whether a category can be renamed: any user category.</summary>
		/// <param name="node">The selected node.</param>
		/// <returns><c>true</c> for user categories.</returns>
		Boolean CanRenameCategory(FunctionNode node);

		/// <summary>Determines whether a node can be deleted: user functions and empty user sub categories.</summary>
		/// <param name="node">The selected node.</param>
		/// <returns><c>true</c> when the node can be deleted.</returns>
		Boolean CanDelete(FunctionNode node);

		/// <summary>
		/// Adds a function to the user's file: as the first item of a category, or right after another function.
		/// </summary>
		/// <param name="target">The category or function the new function goes into / after.</param>
		/// <param name="definition">The new function.</param>
		/// <param name="highlightKeyword">Whether the leading identifier of the code is added to the user keywords (needs a keyword store).</param>
		/// <param name="cancellationToken">Cancels the operation.</param>
		/// <returns>The outcome.</returns>
		Task<FunctionOperationResult> AddFunctionAsync(FunctionNode target, FunctionDefinition definition, Boolean highlightKeyword = false, CancellationToken cancellationToken = default);

		/// <summary>Replaces name, description and code of a function in the file it came from.</summary>
		/// <param name="node">The function node.</param>
		/// <param name="definition">The new values.</param>
		/// <param name="cancellationToken">Cancels the operation.</param>
		/// <returns>The outcome.</returns>
		Task<FunctionOperationResult> EditFunctionAsync(FunctionNode node, FunctionDefinition definition, CancellationToken cancellationToken = default);

		/// <summary>Adds an empty category to the user's file.</summary>
		/// <param name="parent">The category it becomes the last child of, or <c>null</c> for a new top level category at the end of the file.</param>
		/// <param name="name">The title.</param>
		/// <param name="cancellationToken">Cancels the operation.</param>
		/// <returns>The outcome.</returns>
		Task<FunctionOperationResult> AddCategoryAsync(FunctionNode? parent, String name, CancellationToken cancellationToken = default);

		/// <summary>Renames a user category.</summary>
		/// <param name="category">The category.</param>
		/// <param name="newName">The new title.</param>
		/// <param name="cancellationToken">Cancels the operation.</param>
		/// <returns>The outcome.</returns>
		Task<FunctionOperationResult> RenameCategoryAsync(FunctionNode category, String newName, CancellationToken cancellationToken = default);

		/// <summary>Deletes a user function or an empty user sub category; the keyword of a deleted function is removed from the keyword store.</summary>
		/// <param name="node">The node.</param>
		/// <param name="cancellationToken">Cancels the operation.</param>
		/// <returns>The outcome.</returns>
		Task<FunctionOperationResult> DeleteAsync(FunctionNode node, CancellationToken cancellationToken = default);
	}
}