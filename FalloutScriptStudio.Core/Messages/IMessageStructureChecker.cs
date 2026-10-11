namespace FalloutScriptStudio.Core.Messages
{
	using System;

	/// <summary>
	/// Checks the <c>{id}{sound}{text}</c> structure of message file text.
	/// </summary>
	public interface IMessageStructureChecker
	{
		/// <summary>
		/// Checks the structure of a message file.
		/// </summary>
		/// <param name="text">The complete text of the message file.</param>
		/// <returns>The report with all problems found.</returns>
		MessageStructureReport Check(String text);
	}
}
