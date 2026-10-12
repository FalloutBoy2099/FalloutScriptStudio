namespace FalloutScriptStudio.Core.Compilation
{
	using System;
	using Microsoft.Extensions.Logging;

	/// <summary>Source-generated log messages of the compiler (event ids 1100-1199).</summary>
	internal static partial class CompilationLog
	{
		[LoggerMessage(EventId = 1100, Level = LogLevel.Error, Message = "The scratch folder {Folder} could not be created")]
		public static partial void ScratchFolderNotCreated(this ILogger logger, Exception exception, String folder);

		[LoggerMessage(EventId = 1101, Level = LogLevel.Information, Message = "{Operation} of {Script} {Outcome} ({ErrorCount} log entries)")]
		public static partial void Finished(this ILogger logger, String operation, String script, String outcome, Int32 errorCount);

		[LoggerMessage(EventId = 1102, Level = LogLevel.Error, Message = "The preprocessed file {File} could not be moved")]
		public static partial void PreprocessedNotMoved(this ILogger logger, Exception exception, String file);

		[LoggerMessage(EventId = 1103, Level = LogLevel.Error, Message = "The previous preprocessed file {File} could not be deleted")]
		public static partial void PreviousPreprocessedNotDeleted(this ILogger logger, Exception exception, String file);

		[LoggerMessage(EventId = 1104, Level = LogLevel.Warning, Message = "The leftover file {File} of a cancelled run could not be deleted")]
		public static partial void LeftoverNotDeleted(this ILogger logger, Exception exception, String file);

		[LoggerMessage(EventId = 1105, Level = LogLevel.Warning, Message = "The previous {File} could not be backed up")]
		public static partial void BackupFailed(this ILogger logger, Exception exception, String file);

		[LoggerMessage(EventId = 1106, Level = LogLevel.Warning, Message = "The previous {File} could not be restored from {Backup}")]
		public static partial void RestoreFailed(this ILogger logger, Exception exception, String file, String backup);

		[LoggerMessage(EventId = 1107, Level = LogLevel.Warning, Message = "Required tool {Tool} was not found")]
		public static partial void ToolNotFound(this ILogger logger, String tool);

		[LoggerMessage(EventId = 1108, Level = LogLevel.Debug, Message = "Starting {Tool} {Arguments}")]
		public static partial void ToolStarting(this ILogger logger, String tool, String arguments);

		[LoggerMessage(EventId = 1109, Level = LogLevel.Error, Message = "Failed to start {Tool}")]
		public static partial void ToolStartFailed(this ILogger logger, Exception exception, String tool);

		[LoggerMessage(EventId = 1110, Level = LogLevel.Warning, Message = "The preprocessed script {Script} could not be moved to its final name")]
		public static partial void FinalNameNotApplied(this ILogger logger, Exception exception, String script);
	}
}
