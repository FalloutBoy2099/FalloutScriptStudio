namespace FalloutScriptStudio.Core.Dialogue
{
	using System;

	/// <summary>
	/// The result of looking up one dialogue message.
	/// </summary>
	/// <param name="Status">How the lookup ended.</param>
	/// <param name="Text">
	/// The message text. For <see cref="DialogueMessageStatus.MessageNotFound"/> and <see cref="DialogueMessageStatus.FileNotFound"/>
	/// it is the error text the legacy windows showed (<c>[Error] ...</c>); <c>null</c> for <see cref="DialogueMessageStatus.NoMessageNumber"/>.
	/// </param>
	/// <param name="FilePath">The path of the message file (for opening the message editor), or <c>null</c> when unknown.</param>
	public sealed record DialogueMessageLookup(DialogueMessageStatus Status, String? Text, String? FilePath)
	{
		/// <summary>Gets a value indicating whether the message was found.</summary>
		public Boolean IsFound => this.Status == DialogueMessageStatus.Found;

		/// <summary>Gets a value indicating whether the lookup failed in a way the user should see (a missing file or message).</summary>
		public Boolean IsError => this.Status is DialogueMessageStatus.MessageNotFound or DialogueMessageStatus.FileNotFound;
	}
}