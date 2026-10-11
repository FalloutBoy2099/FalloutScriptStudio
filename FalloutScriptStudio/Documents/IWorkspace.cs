namespace FalloutScriptStudio.Documents
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Parsing;

	/// <summary>
	/// The set of open documents plus the application-level operations that editors, panels and tool windows
	/// need (navigation, parsing, symbol operations). Implemented by the main window's workspace service.
	/// </summary>
	/// <remarks>Members may be ADDED, never renamed or removed. Line / column numbers are 1-based.</remarks>
	public interface IWorkspace
	{
		/// <summary>Raised when the active document changed (may be <see langword="null"/> when all tabs are closed).</summary>
		event EventHandler? ActiveDocumentChanged;

		/// <summary>Raised after a document finished a parse and its <see cref="DocumentViewModel.ProgramInfo"/> was refreshed.</summary>
		event EventHandler<DocumentViewModel>? DocumentParsed;

		/// <summary>Gets the open documents in tab order.</summary>
		ObservableCollection<DocumentViewModel> Documents { get; }

		/// <summary>Gets or sets the document of the selected tab.</summary>
		DocumentViewModel? ActiveDocument { get; set; }

		/// <summary>Finds an open document by file path (case-insensitive).</summary>
		DocumentViewModel? FindDocument(String filePath);

		/// <summary>Opens a file in a tab (or activates the existing tab) and optionally navigates inside it.</summary>
		/// <returns>The document, or <see langword="null"/> when the file could not be opened.</returns>
		Task<DocumentViewModel?> OpenAsync(String filePath, Int32 line = -1, Int32 column = -1, Int32 selectLength = -1);

		/// <summary>Navigates to a location, opening the file when needed. Does nothing when <paramref name="file"/> is null and there is no active document.</summary>
		Task GoToAsync(String? file, Int32 line, Int32 column = -1, Int32 selectLength = -1);

		/// <summary>Opens the file referenced by an <c>#include</c> directive of <paramref name="from"/>.</summary>
		Task OpenIncludeAsync(DocumentViewModel from, String includePath);

		/// <summary>Saves a document (asks for a file name when it has none). Returns false when cancelled or failed.</summary>
		Task<Boolean> SaveAsync(DocumentViewModel document);

		/// <summary>Closes a tab, asking to save modifications. Returns false when the user cancelled.</summary>
		Task<Boolean> CloseAsync(DocumentViewModel document);

		/// <summary>Requests a new parse of a document (debounced unless <paramref name="immediate"/>).</summary>
		Task RequestParseAsync(DocumentViewModel document, Boolean immediate = false);

		/// <summary>Shows the references of a symbol in the search / errors result list and optionally jumps to the first one.</summary>
		Task ShowReferencesAsync(String symbolName, IReadOnlyList<SourceReference> references);

		/// <summary>Starts the rename-symbol refactoring (preview window + edits across files) for a symbol.</summary>
		Task RenameSymbolAsync(DocumentViewModel document, IScriptSymbol symbol);

		/// <summary>Gets the text of a message of the <c>.msg</c> file associated with a script, or null when unknown.</summary>
		String? GetMessageText(DocumentViewModel document, Int32 messageId);

		/// <summary>Opens the message editor window for the file associated with the script (creating it when asked).</summary>
		Task OpenMessageEditorAsync(DocumentViewModel document);

		/// <summary>Asks for a procedure name and inserts a new procedure (declaration + body) in the document.</summary>
		/// <param name="document">The script.</param>
		/// <param name="suggestedName">The name to start with (the selected procedure or text); its trailing number is incremented.</param>
		/// <param name="anchorProcedure">The procedure "after the procedure" and "copy the body" refer to (the one selected in the procedures tree); the procedure under the caret when <see langword="null"/>.</param>
		Task CreateProcedureAsync(DocumentViewModel document, String? suggestedName = null, String? anchorProcedure = null);

		/// <summary>Shows search results (type <c>BuildErrorType.Search</c>) in the Errors tab of the output panel.</summary>
		void ShowSearchResults(IReadOnlyList<FalloutScriptStudio.Core.Compilation.BuildError> results);

		/// <summary>Gets the open documents' current texts keyed by full path (used by search / refactoring so unsaved edits are honored).</summary>
		IReadOnlyDictionary<String, String> GetOpenDocumentTexts();

		/// <summary>
		/// Opens files that were dropped on the window or on the text of an editor: they are added to the recent files and a file that is
		/// already open is offered as a copy. The default implementation only opens the files.
		/// </summary>
		/// <param name="filePaths">The dropped files.</param>
		/// <returns>A task that completes when all files were handled.</returns>
		async Task OpenDroppedFilesAsync(IReadOnlyList<String> filePaths)
		{
			foreach (String filePath in filePaths)
			{
				await this.OpenAsync(filePath);
			}
		}

		/// <summary>Writes a short message in the status bar / output panel.</summary>
		void ReportStatus(String message);
	}
}
