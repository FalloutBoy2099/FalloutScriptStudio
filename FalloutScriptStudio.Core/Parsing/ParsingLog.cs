namespace FalloutScriptStudio.Core.Parsing
{
	using System;
	using Microsoft.Extensions.Logging;

	/// <summary>Source-generated log messages of the parsers (event ids 1500-1599).</summary>
	internal static partial class ParsingLog
	{
		[LoggerMessage(EventId = 1500, Level = LogLevel.Debug, Message = "Parsed '{File}': {Outcome}, native status {Status}")]
		public static partial void Parsed(this ILogger logger, String file, ParseOutcome outcome, Int32 status);

		[LoggerMessage(EventId = 1501, Level = LogLevel.Error, Message = "The native parser host '{Host}' could not be started")]
		public static partial void HostNotStarted(this ILogger logger, Exception exception, String host);

		[LoggerMessage(EventId = 1502, Level = LogLevel.Warning, Message = "The native parser host was gone when a request was sent ({Reason}); restarting it")]
		public static partial void HostGoneOnSend(this ILogger logger, String reason);

		[LoggerMessage(EventId = 1503, Level = LogLevel.Error, Message = "{Message}")]
		public static partial void HostFailed(this ILogger logger, String message);

		[LoggerMessage(EventId = 1504, Level = LogLevel.Warning, Message = "The job object of the parser host could not be closed (Win32 error {ErrorCode})")]
		public static partial void JobHandleNotClosed(this ILogger logger, Int32 errorCode);

		[LoggerMessage(EventId = 1505, Level = LogLevel.Error, Message = "{Reason} Last problem: {Message}")]
		public static partial void ParserDisabled(this ILogger logger, String reason, String message);
	}
}
