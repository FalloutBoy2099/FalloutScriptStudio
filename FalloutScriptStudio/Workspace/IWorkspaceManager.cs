namespace FalloutScriptStudio.Workspace
{
	using System;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Documents;

	/// <summary>The application-level document operations of the main window, on top of <see cref="IWorkspace"/>.</summary>
	public interface IWorkspaceManager : IWorkspace
	{
		/// <summary>Raised after a document was added to the tab strip.</summary>
		event EventHandler<DocumentViewModel>? DocumentOpened;

		/// <summary>Raised after a document was removed from the tab strip.</summary>
		event EventHandler<DocumentViewModel>? DocumentClosed;

		/// <summary>Opens a file with detailed options.</summary>
		Task<DocumentViewModel?> OpenAsync(String filePath, OpenDocumentOptions options);

		/// <summary>Creates a new unsaved document.</summary>
		/// <param name="text">The initial text; the default script template when <see langword="null"/>.</param>
		/// <param name="kind">The kind of the document.</param>
		DocumentViewModel CreateNew(String? text = null, DocumentKind kind = DocumentKind.Script);

		/// <summary>Creates a new unsaved document from a template file.</summary>
		Task<DocumentViewModel?> NewFromTemplateAsync(String templatePath);

		/// <summary>Asks for a file name and saves the document under it.</summary>
		Task<Boolean> SaveAsAsync(DocumentViewModel document);

		/// <summary>Saves every modified document. Returns false when one could not be saved.</summary>
		Task<Boolean> SaveAllAsync();

		/// <summary>
		/// Closes all tabs except one. Every "save the changes?" question is asked first; only when none was cancelled are the chosen
		/// documents saved and the tabs closed, so a cancellation leaves everything as it was.
		/// </summary>
		/// <param name="except">The tab that stays open, or <see langword="null"/>.</param>
		/// <param name="beforeClosing">A step that runs once the user answered all questions and before anything is saved or closed (for example closing the Message Editor windows); returning <see langword="false"/> cancels the whole operation.</param>
		/// <returns><see langword="false"/> when the user cancelled or a document could not be saved.</returns>
		Task<Boolean> CloseAllAsync(DocumentViewModel? except = null, Func<Task<Boolean>>? beforeClosing = null);

		/// <summary>
		/// Runs an operation that nobody waits for (or whose failure must not escape): an exception is written to the log and to the
		/// status bar instead of being lost. The returned task never fails.
		/// </summary>
		/// <param name="operation">The operation.</param>
		/// <returns>A task that completes when the operation ended.</returns>
		Task GuardAsync(Func<Task> operation);

		/// <summary>Reloads a document from disk, keeping the caret line.</summary>
		Task<Boolean> ReloadAsync(DocumentViewModel document);

		/// <summary>Checks all open files for changes made by other programs and offers to reload them.</summary>
		Task CheckExternalChangesAsync();

		/// <summary>Moves a tab to a new position.</summary>
		void MoveDocument(Int32 oldIndex, Int32 newIndex);

		/// <summary>Extracts a script from a Fallout game archive (<c>.dat</c>) and opens it.</summary>
		Task<DocumentViewModel?> OpenFromArchiveAsync(String scriptFileName);

		/// <summary>Finds (and optionally creates) the message file of a script and remembers it in the document.</summary>
		/// <returns>The path of the message file, or <see langword="null"/>.</returns>
		Task<String?> AssociateMessageFileAsync(DocumentViewModel document, Boolean create, Boolean reportProblems);

		/// <summary>Stores the caret line and similar per-script state in the settings (done when a tab closes and at shutdown).</summary>
		void RememberDocumentState(DocumentViewModel document);

		/// <summary>Creates the adapter that lets the message editor send lines back into a script.</summary>
		FalloutScriptStudio.Services.MessageEditor.IMessageLineTarget CreateMessageTarget(DocumentViewModel document);

		/// <summary>
		/// Reads the associated message file of a script again and replaces its cached texts (used for the hover tips). It happens by
		/// itself when the script tab is selected, when the message file is saved or reloaded, and when external changes are checked.
		/// </summary>
		Task RefreshMessageTextsAsync(DocumentViewModel document);

		/// <summary>Replaces the cached message texts of a script (after the message editor saved the file).</summary>
		void UpdateMessageTexts(DocumentViewModel document, FalloutScriptStudio.Core.Messages.MessageDocument messages);

		/// <summary>Asks how to create one of the standard handlers (placement, copying) and inserts it.</summary>
		/// <param name="document">The script.</param>
		/// <param name="handlerName">The name of the handler.</param>
		/// <param name="anchorProcedure">The procedure "after the procedure" and "copy the body" refer to (the one selected in the procedures tree); the procedure under the caret when <see langword="null"/>.</param>
		Task CreateHandlerAsync(DocumentViewModel document, String handlerName, String? anchorProcedure = null);

		/// <summary>Deletes a procedure (declaration and body) from a script.</summary>
		Task<Boolean> DeleteProcedureAsync(DocumentViewModel document, String name);

		/// <summary>Moves a procedure in front of another procedure of the same script (or to the end of the script).</summary>
		Task<Boolean> MoveProcedureAsync(DocumentViewModel document, String name, String? beforeProcedure);

		/// <summary>Reloads the macros of the headers folder (after a header was saved or the folder setting changed).</summary>
		Task ReloadGlobalMacrosAsync();
	}
}
