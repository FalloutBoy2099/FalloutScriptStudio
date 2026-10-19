namespace FalloutScriptStudio.Core.Archives
{
	using System;
	using Microsoft.Extensions.Logging;

	/// <summary>Source-generated log messages of the archive services (event ids 1000-1099).</summary>
	internal static partial class ArchiveLog
	{
		[LoggerMessage(EventId = 1000, Level = LogLevel.Warning, Message = "The script {Script} could not be read from {Archive}")]
		public static partial void ScriptNotReadable(this ILogger logger, Exception exception, String script, String archive);

		[LoggerMessage(EventId = 1001, Level = LogLevel.Error, Message = "The script {Script} could not be extracted from {Archive}")]
		public static partial void ScriptNotExtracted(this ILogger logger, Exception exception, String script, String archive);
	}
}