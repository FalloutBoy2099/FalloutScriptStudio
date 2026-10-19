namespace FalloutScriptStudio.Services.ScriptTools.Flowchart
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;
	using Core.Dialogue;
	using Documents;

	/// <summary>
	/// Gives the dialogue tools (flowchart, node code editor, dialogue preview) access to the message files of a script and
	/// writes changed message texts back.
	/// </summary>
	public interface IDialogueMessageService
	{
		/// <summary>
		/// Finds and loads the message file of a script (and prepares the lookup of the other dialogue message files).
		/// A script without a file or without a message file gets a source whose lookups report a missing file.
		/// </summary>
		/// <param name="script">The script.</param>
		/// <param name="cancellationToken">Cancels the operation.</param>
		/// <returns>The message source.</returns>
		Task<IDialogueMessageSource> CreateSourceAsync(DocumentViewModel script, CancellationToken cancellationToken = default);

		/// <summary>Replaces the text of one message in a message file, keeping all other lines.</summary>
		/// <param name="messageFilePath">The message file.</param>
		/// <param name="messageId">The message number.</param>
		/// <param name="text">The new text.</param>
		/// <param name="cancellationToken">Cancels the operation.</param>
		/// <returns><see langword="false"/> when the message or the file does not exist or cannot be written.</returns>
		Task<Boolean> UpdateMessageTextAsync(String messageFilePath, Int32 messageId, String text, CancellationToken cancellationToken = default);
	}
}