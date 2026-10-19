namespace FalloutScriptStudio.Workspace
{
	using System;
	using System.Threading.Tasks;
	using Core.Parsing;
	using Documents;

	/// <summary>Renames a symbol (procedure, variable or macro) in the places where it is used.</summary>
	/// <remarks>The default implementation edits the open documents directly; a preview based implementation can replace it.</remarks>
	public interface ISymbolRenameService
	{
		/// <summary>Asks for the new name and applies the rename.</summary>
		/// <param name="workspace">The workspace that provides documents.</param>
		/// <param name="document">The document in which the rename was requested.</param>
		/// <param name="symbol">The symbol to rename.</param>
		/// <returns><see langword="true"/> when the symbol was renamed.</returns>
		Task<Boolean> RenameAsync(IWorkspaceManager workspace, DocumentViewModel document, IScriptSymbol symbol);
	}
}