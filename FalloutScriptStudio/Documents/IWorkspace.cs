namespace FalloutScriptStudio.Documents
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Threading.Tasks;
	using Core.Parsing;

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

		/// <summary>
		/// Shows the list of snippets the user can insert (Insert Snippet) or surround the selection with (Surround With), and waits for the choice.
		/// The default implementation chooses nothing.
		/// </summary>
		/// <param name="request">The snippets to offer.</param>
		/// <returns>The chosen snippet, or <see langword="null"/> when the list was cancelled.</returns>
		Task<FalloutScriptStudio.Core.Snippets.Snippet?> PickSnippetAsync(FalloutScriptStudio.Services.ToolWindows.SnippetPickerRequest request)
		{
			return Task.FromResult<FalloutScriptStudio.Core.Snippets.Snippet?>(null);
		}

		/// <summary>
		/// Reports that something failed: the message goes to the status bar and, in the main window, also to the list of problems of the
		/// Errors tab (where it stays until it is cleared). The default implementation only writes to the status bar.
		/// </summary>
		/// <param name="message">What failed, in the current language.</param>
		void ReportFailure(String message)
		{
			this.ReportStatus(message);
		}

		/// <summary>
		/// Determines whether the index of the project knows a procedure, macro or variable of that name (a cheap lookup, used to enable
		/// the navigation commands). The default implementation knows nothing.
		/// </summary>
		/// <param name="name">The name.</param>
		/// <returns><see langword="true"/> when the project index holds a symbol of that name.</returns>
		Boolean HasProjectSymbol(String name)
		{
			return false;
		}

		/// <summary>
		/// Goes to the definition or declaration of a symbol, or lists its references, searching the whole project when the symbol is not
		/// defined in the document or in the files it includes. Several candidates are offered in a list, the nearest first. The default
		/// implementation does nothing.
		/// </summary>
		/// <param name="from">The document the symbol is used in.</param>
		/// <param name="name">The name of the symbol.</param>
		/// <param name="kind">What to look for.</param>
		/// <returns>A task that completes when the navigation ended.</returns>
		Task NavigateToProjectSymbolAsync(DocumentViewModel from, String name, ProjectNavigationKind kind)
		{
			return Task.CompletedTask;
		}

		/// <summary>
		/// Lists the references of a symbol in the whole project (the files that see its definition), together with the ones the parser
		/// already found. The default implementation does nothing.
		/// </summary>
		/// <param name="from">The document the symbol is used in.</param>
		/// <param name="symbol">The symbol found in the document or its includes.</param>
		/// <param name="name">The name of the symbol.</param>
		/// <returns>A task that completes when the references are shown.</returns>
		Task ShowProjectReferencesAsync(DocumentViewModel from, IScriptSymbol symbol, String name)
		{
			return Task.CompletedTask;
		}

		/// <summary>
		/// Deletes a procedure that nothing uses, after showing what is removed and asking for confirmation; refuses when the procedure is
		/// still used (the uses are listed) or is a handler the game calls. The default implementation does nothing.
		/// </summary>
		/// <param name="document">The script.</param>
		/// <param name="name">The name of the procedure.</param>
		/// <returns><see langword="true"/> when the procedure was deleted.</returns>
		Task<Boolean> DeleteUnusedProcedureAsync(DocumentViewModel document, String name)
		{
			return Task.FromResult(false);
		}

		/// <summary>
		/// Lists the variables of a script that are declared but never read or never used in the list of problems. The default
		/// implementation does nothing.
		/// </summary>
		/// <param name="document">The script.</param>
		/// <returns>The number of variables listed.</returns>
		Task<Int32> FindUnusedVariablesAsync(DocumentViewModel document)
		{
			return Task.FromResult(0);
		}

		/// <summary>
		/// Removes the declaration of an unused variable (the fix a problem of the list offers). The default implementation does nothing.
		/// </summary>
		/// <param name="file">The script; it is opened when needed.</param>
		/// <param name="line">The 1-based line of the name of the variable.</param>
		/// <param name="column">The 1-based column of the name of the variable.</param>
		/// <returns>A task that completes when the declaration was removed.</returns>
		Task RemoveUnusedVariableAsync(String file, Int32 line, Int32 column)
		{
			return Task.CompletedTask;
		}

		/// <summary>
		/// Applies the quick fix of the finding of the semantic analysis at a place of a script (the fix a problem of the list offers). The
		/// script is analysed again first, so a fix is never applied to a text it was not made for. The default implementation does nothing.
		/// </summary>
		/// <param name="file">The script; it is opened when needed.</param>
		/// <param name="line">The 1-based line of the finding.</param>
		/// <param name="column">The 1-based column of the finding.</param>
		/// <returns>A task that completes when the fix was applied.</returns>
		Task ApplyQuickFixAsync(String file, Int32 line, Int32 column)
		{
			return Task.CompletedTask;
		}

		/// <summary>
		/// Applies the quick fix of the finding of the semantic analysis under an offset of a script (the Quick Fix command of the editor).
		/// The default implementation does nothing.
		/// </summary>
		/// <param name="document">The script.</param>
		/// <param name="offset">The offset, usually of the caret.</param>
		/// <returns><see langword="true"/> when a fix was applied.</returns>
		Task<Boolean> ApplyQuickFixAtAsync(DocumentViewModel document, Int32 offset)
		{
			return Task.FromResult(false);
		}

		/// <summary>
		/// Opens the dialogue preview of a script at a dialogue node (Preview Dialogue From This Node); a window that is already open for the
		/// script is moved to that node. The default implementation does nothing.
		/// </summary>
		/// <param name="document">The script.</param>
		/// <param name="nodeName">The name of the node procedure.</param>
		/// <returns>A task that completes when the preview is shown.</returns>
		Task PreviewDialogueFromNodeAsync(DocumentViewModel document, String nodeName)
		{
			return Task.CompletedTask;
		}

		/// <summary>
		/// Appends the message numbers a script uses and its message file lacks to the message file (the fix of a missing message), then checks
		/// the script again. The default implementation does nothing.
		/// </summary>
		/// <param name="scriptPath">The script.</param>
		/// <param name="numbers">The message numbers to add.</param>
		/// <returns>A task that completes when the messages are added.</returns>
		Task AddMissingMessagesAsync(String scriptPath, IReadOnlyList<Int32> numbers)
		{
			return Task.CompletedTask;
		}

		/// <summary>
		/// Finds or creates the message file of a script and associates it with the script (the fix of a script that uses messages and has no
		/// message file). The default implementation does nothing.
		/// </summary>
		/// <param name="scriptPath">The script.</param>
		/// <returns>A task that completes when the file is associated.</returns>
		Task CreateMessageFileAsync(String scriptPath)
		{
			return Task.CompletedTask;
		}

		/// <summary>
		/// Extracts the selected statements of a script into a new procedure (Code &gt; Extract Procedure). The default implementation does nothing.
		/// </summary>
		/// <param name="document">The script.</param>
		/// <returns>A task that completes when the refactoring ended.</returns>
		Task ExtractProcedureAsync(DocumentViewModel document)
		{
			return Task.CompletedTask;
		}
	}
}