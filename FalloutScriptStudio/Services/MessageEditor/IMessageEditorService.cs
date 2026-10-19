namespace FalloutScriptStudio.Services.MessageEditor
{
	using System;
	using System.Threading.Tasks;

	/// <summary>
	/// Opens Message Editor windows. A window that already edits the requested file is activated instead of opening a second one.
	/// All members must be called on the UI thread.
	/// </summary>
	/// <remarks>
	/// Stand-alone use (command line <c>*.msg</c>, no main window): call <see cref="Open(String?, IMessageLineTarget?)"/> without a target and
	/// shut the application down from <see cref="AllWindowsClosed"/> (the application can use <c>ShutdownMode.OnExplicitShutdown</c>).
	/// </remarks>
	public interface IMessageEditorService
	{
		/// <summary>Occurs after the last Message Editor window was closed.</summary>
		event EventHandler? AllWindowsClosed;

		/// <summary>Gets the number of open Message Editor windows.</summary>
		Int32 OpenWindowCount { get; }

		/// <summary>
		/// Opens (or activates) the editor for a message file.
		/// </summary>
		/// <param name="msgFilePath">The file to edit, or <see langword="null"/> for an empty unsaved document.</param>
		/// <param name="target">The owner of the lines the editor sends, or <see langword="null"/> for a stand-alone editor.</param>
		/// <returns><see langword="true"/> if a window is shown.</returns>
		Boolean Open(String? msgFilePath, IMessageLineTarget? target = null);

		/// <summary>
		/// Opens (or activates) the editor with full control over how it is opened.
		/// </summary>
		/// <param name="options">The options; with <see cref="MessageEditorOpenOptions.Modal"/> the call returns when the window is closed.</param>
		/// <returns><see langword="true"/> if a window is shown.</returns>
		Boolean Open(MessageEditorOpenOptions options);

		/// <summary>
		/// Finds the message file that belongs to a script (see <c>IMessageFileService.LocateForScript</c>), offers to create it when it
		/// does not exist and opens it. This is the "Associated message file" command of the script editor.
		/// </summary>
		/// <param name="scriptPath">The full path of the script.</param>
		/// <param name="scriptNameId">The script number from its <c>NAME</c> macro, -1 if it has none, or <see langword="null"/> if the script was not parsed.</param>
		/// <param name="target">The script editor tab that receives the lines.</param>
		/// <param name="createIfMissing">Whether to offer to create the file when none exists.</param>
		/// <returns><see langword="true"/> if the editor was opened; <see langword="false"/> if there is no file or the user declined.</returns>
		Task<Boolean> OpenForScriptAsync(String scriptPath, Int32? scriptNameId, IMessageLineTarget target, Boolean createIfMissing = true);

		/// <summary>
		/// Closes all windows, asking about unsaved changes first. Call it before the application exits.
		/// </summary>
		/// <returns><see langword="false"/> if the user cancelled and at least one window stays open.</returns>
		Task<Boolean> CloseAllAsync();
	}
}