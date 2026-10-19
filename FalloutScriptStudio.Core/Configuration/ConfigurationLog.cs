namespace FalloutScriptStudio.Core.Configuration
{
	using System;
	using Microsoft.Extensions.Logging;

	/// <summary>Source-generated log messages of the settings services (event ids 1200-1299).</summary>
	internal static partial class ConfigurationLog
	{
		[LoggerMessage(EventId = 1200, Level = LogLevel.Warning, Message = "The legacy settings file could not be read completely")]
		public static partial void LegacySettingsUnreadable(this ILogger logger, Exception exception);

		[LoggerMessage(EventId = 1201, Level = LogLevel.Warning, Message = "The legacy recent files list could not be read completely")]
		public static partial void LegacyRecentFilesUnreadable(this ILogger logger, Exception exception);

		[LoggerMessage(EventId = 1202, Level = LogLevel.Warning, Message = "The legacy folding file could not be read completely")]
		public static partial void LegacyFoldingUnreadable(this ILogger logger, Exception exception);

		[LoggerMessage(EventId = 1203, Level = LogLevel.Warning, Message = "The legacy file {Path} could not be opened")]
		public static partial void LegacyFileNotOpened(this ILogger logger, Exception exception, String path);

		[LoggerMessage(EventId = 1204, Level = LogLevel.Warning, Message = "The legacy file {Path} could not be read")]
		public static partial void LegacyFileNotRead(this ILogger logger, Exception exception, String path);

		[LoggerMessage(EventId = 1205, Level = LogLevel.Error, Message = "The project file {Path} could not be saved")]
		public static partial void ProjectFileNotSaved(this ILogger logger, Exception exception, String path);

		[LoggerMessage(EventId = 1206, Level = LogLevel.Warning, Message = "The project file {Path} is damaged; the project inherits the global settings")]
		public static partial void ProjectFileDamaged(this ILogger logger, String path);

		[LoggerMessage(EventId = 1207, Level = LogLevel.Error, Message = "The project file {Path} could not be read ({Detail}); it is left untouched")]
		public static partial void ProjectFileUnreadable(this ILogger logger, String path, String detail);

		[LoggerMessage(EventId = 1208, Level = LogLevel.Error, Message = "The file {Path} could not be saved")]
		public static partial void FileNotSaved(this ILogger logger, Exception exception, String path);

		[LoggerMessage(EventId = 1209, Level = LogLevel.Error, Message = "The program folders could not be prepared: {Problem}")]
		public static partial void FoldersNotPrepared(this ILogger logger, String problem);

		[LoggerMessage(EventId = 1210, Level = LogLevel.Error, Message = "The program folders could not be created")]
		public static partial void FoldersNotCreated(this ILogger logger, Exception exception);

		[LoggerMessage(EventId = 1211, Level = LogLevel.Error, Message = "The file {Path} could not be read; it is left untouched")]
		public static partial void FileNotRead(this ILogger logger, Exception exception, String path);

		[LoggerMessage(EventId = 1212, Level = LogLevel.Warning, Message = "Legacy import: {Problem}")]
		public static partial void LegacyImportProblem(this ILogger logger, String problem);

		[LoggerMessage(EventId = 1213, Level = LogLevel.Information, Message = "Settings were imported from the files of the old editor")]
		public static partial void LegacyImported(this ILogger logger);

		[LoggerMessage(EventId = 1214, Level = LogLevel.Warning, Message = "The file {Path} is empty; defaults are used")]
		public static partial void FileEmpty(this ILogger logger, String path);

		[LoggerMessage(EventId = 1215, Level = LogLevel.Warning, Message = "The file {Path} is damaged; defaults are used")]
		public static partial void FileDamaged(this ILogger logger, Exception exception, String path);

		[LoggerMessage(EventId = 1216, Level = LogLevel.Warning, Message = "A copy of the damaged file {Path} could not be made")]
		public static partial void CorruptCopyFailed(this ILogger logger, Exception exception, String path);

		[LoggerMessage(EventId = 1217, Level = LogLevel.Warning, Message = "The file {Path} cannot be read and is not overwritten")]
		public static partial void FileNotOverwritten(this ILogger logger, String path);

		[LoggerMessage(EventId = 1218, Level = LogLevel.Information, Message = "The project file {Path} was {Change} on disk")]
		public static partial void ProjectFileChangedOnDisk(this ILogger logger, String path, String change);

		[LoggerMessage(EventId = 1219, Level = LogLevel.Warning, Message = "The project file {Path} is locked or cannot be read yet; it is tried again later ({Detail})")]
		public static partial void ProjectFileLocked(this ILogger logger, String path, String detail);

		[LoggerMessage(EventId = 1220, Level = LogLevel.Warning, Message = "The project file {Path} is not valid ({Detail}); the last valid settings stay in effect")]
		public static partial void ProjectFileInvalid(this ILogger logger, String path, String detail);

		[LoggerMessage(EventId = 1221, Level = LogLevel.Error, Message = "Watching the project file {Path} failed")]
		public static partial void ProjectWatchFailed(this ILogger logger, Exception exception, String path);

		[LoggerMessage(EventId = 1222, Level = LogLevel.Warning, Message = "The folder {Path} cannot be watched for changes; the project file is checked at intervals only")]
		public static partial void ProjectFolderNotWatched(this ILogger logger, Exception exception, String path);
	}
}