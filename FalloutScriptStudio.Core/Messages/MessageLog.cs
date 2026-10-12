namespace FalloutScriptStudio.Core.Messages
{
	using System;
	using Microsoft.Extensions.Logging;

	/// <summary>Source-generated log messages of the message file services (event ids 1400-1499).</summary>
	internal static partial class MessageLog
	{
		[LoggerMessage(EventId = 1400, Level = LogLevel.Information, Message = "Created the message file {Path}")]
		public static partial void FileCreated(this ILogger logger, String path);

		[LoggerMessage(EventId = 1401, Level = LogLevel.Warning, Message = "Message {MessageId} was not found in {Path}")]
		public static partial void MessageNotFound(this ILogger logger, Int32 messageId, String path);

		[LoggerMessage(EventId = 1402, Level = LogLevel.Warning, Message = "The message folder {Directory} could not be searched")]
		public static partial void FolderNotSearched(this ILogger logger, Exception exception, String directory);
	}
}
