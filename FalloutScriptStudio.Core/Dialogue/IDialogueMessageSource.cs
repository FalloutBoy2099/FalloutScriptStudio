namespace FalloutScriptStudio.Core.Dialogue
{
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;

	/// <summary>
	/// Gives the dialogue tools access to the message files of a script. Files are loaded asynchronously up front
	/// (<see cref="EnsureLoadedAsync"/>) so that the lookups themselves are synchronous and cheap.
	/// </summary>
	public interface IDialogueMessageSource
	{
		/// <summary>The message file number that stands for the message file of the script itself.</summary>
		public const Int32 OwnFile = -1;

		/// <summary>
		/// Loads the message files with the given numbers (indexes of <c>scripts.lst</c>) that are not loaded yet.
		/// Numbers that are not positive refer to the script's own file, which is loaded when the source is created.
		/// </summary>
		/// <param name="messageFileNumbers">The numbers of the files needed.</param>
		/// <param name="cancellationToken">Cancels the operation.</param>
		/// <returns>A task that completes when the files are loaded; files that cannot be found are remembered as missing.</returns>
		Task EnsureLoadedAsync(IEnumerable<Int32> messageFileNumbers, CancellationToken cancellationToken = default);

		/// <summary>Looks up a message in the files loaded so far.</summary>
		/// <param name="messageFileNumber">The file number, or any value that is not positive for the script's own file.</param>
		/// <param name="messageNumber">The message number.</param>
		/// <returns>The result of the lookup.</returns>
		DialogueMessageLookup GetMessage(Int32 messageFileNumber, Int32 messageNumber);

		/// <summary>Gets the path of a message file, to edit its messages.</summary>
		/// <param name="messageFileNumber">The file number, or any value that is not positive for the script's own file.</param>
		/// <returns>The path, or <c>null</c> when the file is unknown or missing.</returns>
		String? GetMessageFilePath(Int32 messageFileNumber);
	}
}
