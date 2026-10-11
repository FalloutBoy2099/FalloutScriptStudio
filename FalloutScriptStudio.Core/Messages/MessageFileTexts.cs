namespace FalloutScriptStudio.Core.Messages
{
	using System;

	/// <summary>
	/// Texts shown by dialog previews and flowcharts when a message cannot be resolved.
	/// </summary>
	public static class MessageFileTexts
	{
		/// <summary>Shown when the message number does not exist in the message file.</summary>
		public const String TextNotFound = "[Error] <Text was not found in msg file>";

		/// <summary>Shown when the message file does not exist; the argument is the message file number.</summary>
		public const String FileNotFoundFormat = "[Error] MsgID:{0} <Not found message file>";

		/// <summary>The content of a newly created message file.</summary>
		public const String NewFileContent = "{100}{}{}";

		/// <summary>The line comment prefix of message files.</summary>
		public const String CommentPrefix = "#";
	}
}
