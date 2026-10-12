namespace FalloutScriptStudio.Core.Decompilation
{
	using System;
	using Microsoft.Extensions.Logging;

	/// <summary>Source-generated log messages of the decompilers (event ids 1300-1399).</summary>
	internal static partial class DecompilationLog
	{
		[LoggerMessage(EventId = 1300, Level = LogLevel.Warning, Message = "The script {Script} could not be read")]
		public static partial void ScriptNotRead(this ILogger logger, Exception exception, String script);

		[LoggerMessage(EventId = 1301, Level = LogLevel.Warning, Message = "The script {Script} could not be decompiled")]
		public static partial void ScriptNotDecompiled(this ILogger logger, Exception exception, String script);

		[LoggerMessage(EventId = 1302, Level = LogLevel.Warning, Message = "The decompiled script {Script} could not be written")]
		public static partial void OutputNotWritten(this ILogger logger, Exception exception, String script);

		[LoggerMessage(EventId = 1303, Level = LogLevel.Warning, Message = "Decompiler {Tool} was not found")]
		public static partial void ToolNotFound(this ILogger logger, String tool);

		[LoggerMessage(EventId = 1304, Level = LogLevel.Error, Message = "Failed to start decompiler {Tool}")]
		public static partial void ToolStartFailed(this ILogger logger, Exception exception, String tool);
	}
}
