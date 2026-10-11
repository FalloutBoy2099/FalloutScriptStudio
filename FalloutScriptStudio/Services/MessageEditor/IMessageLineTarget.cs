namespace FalloutScriptStudio.Services.MessageEditor
{
	using System;
	using FalloutScriptStudio.Core.Messages;

	/// <summary>
	/// What the Message Editor needs from the part of the application that opened it, usually the script editor tab
	/// the message file belongs to. A window opened without a target (for example from the command line) works stand-alone:
	/// it simply cannot send lines anywhere.
	/// </summary>
	public interface IMessageLineTarget
	{
		/// <summary>
		/// Gets the path of the script that owns the message file, shown in the window as a hint. <see langword="null"/> if there is none.
		/// </summary>
		String? ScriptPath { get; }

		/// <summary>
		/// Gets a value indicating whether <see cref="AcceptMessageLine"/> can be used right now (for example, a script is open and has the focus).
		/// The editor re-reads it whenever it enables or disables its "Send line" commands.
		/// </summary>
		Boolean CanAcceptMessageLine { get; }

		/// <summary>
		/// Receives the message the user chose with "Send line" (Alt+S, middle mouse button, toolbar). The legacy editor inserted the
		/// number into the script at the caret.
		/// </summary>
		/// <param name="messageId">The message number.</param>
		/// <param name="messageText">The text of the message, in case the receiver wants more than the number.</param>
		void AcceptMessageLine(Int32 messageId, String messageText);

		/// <summary>
		/// Called after the message file was loaded or written, so the owner can refresh its cache of associated messages
		/// (the legacy <c>MessageFile.ParseMessages</c>). Called on the UI thread; the document is a private copy the owner may keep.
		/// </summary>
		/// <param name="messageFilePath">The file that was loaded or saved.</param>
		/// <param name="document">The content of the file.</param>
		void MessagesUpdated(String messageFilePath, MessageDocument document);

		/// <summary>
		/// Asks the owner to open the message file as plain text in the script editor ("Open as Text").
		/// The editor window closes itself when this returns <see langword="true"/>.
		/// </summary>
		/// <param name="messageFilePath">The file to open.</param>
		/// <returns><see langword="true"/> if the owner opened the file; <see langword="false"/> if it cannot.</returns>
		Boolean TryOpenAsText(String messageFilePath);
	}
}
