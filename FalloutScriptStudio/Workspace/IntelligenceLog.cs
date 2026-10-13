namespace FalloutScriptStudio.Workspace
{
	using System;
	using Microsoft.Extensions.Logging;

	/// <summary>Source-generated log messages of the project index and the code intelligence operations (event ids 2150-2169).</summary>
	internal static partial class IntelligenceLog
	{
		[LoggerMessage(EventId = 2150, Level = LogLevel.Debug, Message = "The project index could not be updated for {Path}: {Reason}")]
		public static partial void IndexUpdateFailed(this ILogger logger, String path, String reason);

		[LoggerMessage(EventId = 2151, Level = LogLevel.Warning, Message = "The project index run failed: {Reason}")]
		public static partial void IndexRunFailed(this ILogger logger, String reason);
	}
}
