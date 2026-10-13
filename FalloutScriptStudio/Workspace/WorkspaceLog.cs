namespace FalloutScriptStudio.Workspace
{
	using System;
	using Microsoft.Extensions.Logging;

	/// <summary>Source-generated log messages of the workspace services (event ids 2100-2199).</summary>
	internal static partial class WorkspaceLog
	{
		[LoggerMessage(EventId = 2100, Level = LogLevel.Warning, Message = "Cannot read the default template {File}")]
		public static partial void TemplateNotRead(this ILogger logger, Exception exception, String file);

		[LoggerMessage(EventId = 2101, Level = LogLevel.Warning, Message = "Cannot write {File}")]
		public static partial void FileNotWritten(this ILogger logger, Exception exception, String file);

		[LoggerMessage(EventId = 2102, Level = LogLevel.Warning, Message = "Cannot read the message file {Path}")]
		public static partial void MessageFileNotRead(this ILogger logger, Exception exception, String path);

		[LoggerMessage(EventId = 2103, Level = LogLevel.Error, Message = "The message file {Path} could not be created")]
		public static partial void MessageFileNotCreated(this ILogger logger, Exception exception, String path);

		[LoggerMessage(EventId = 2104, Level = LogLevel.Error, Message = "Parsing '{File}' failed")]
		public static partial void ParseFailed(this ILogger logger, Exception exception, String file);

		[LoggerMessage(EventId = 2105, Level = LogLevel.Error, Message = "A background operation failed")]
		public static partial void BackgroundOperationFailed(this ILogger logger, Exception exception);

		[LoggerMessage(EventId = 2106, Level = LogLevel.Information, Message = "The remembered project document {Path} no longer exists and is not reopened")]
		public static partial void RememberedDocumentSkipped(this ILogger logger, String path);
	}
}
