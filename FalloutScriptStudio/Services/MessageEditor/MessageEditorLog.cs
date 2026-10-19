namespace FalloutScriptStudio.Services.MessageEditor
{
	using System;
	using Microsoft.Extensions.Logging;

	/// <summary>Source-generated log messages of the message editor (event ids 2200-2299).</summary>
	internal static partial class MessageEditorLog
	{
		[LoggerMessage(EventId = 2200, Level = LogLevel.Error, Message = "The message file {Path} could not be created")]
		public static partial void FileNotCreated(this ILogger logger, Exception exception, String path);

		[LoggerMessage(EventId = 2201, Level = LogLevel.Error, Message = "The message file {Path} could not be read")]
		public static partial void FileNotRead(this ILogger logger, Exception exception, String path);

		[LoggerMessage(EventId = 2202, Level = LogLevel.Warning, Message = "The settings could not be saved")]
		public static partial void SettingsNotSaved(this ILogger logger, Exception exception);

		[LoggerMessage(EventId = 2203, Level = LogLevel.Error, Message = "The external editor {Editor} could not be started")]
		public static partial void ExternalEditorNotStarted(this ILogger logger, Exception exception, String editor);

		[LoggerMessage(EventId = 2204, Level = LogLevel.Error, Message = "The message file {Path} could not be written")]
		public static partial void FileNotWritten(this ILogger logger, Exception exception, String path);
	}
}