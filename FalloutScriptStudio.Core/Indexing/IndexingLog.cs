namespace FalloutScriptStudio.Core.Indexing
{
	using System;
	using Microsoft.Extensions.Logging;

	/// <summary>Source-generated log messages of the project index (event ids 1700-1799).</summary>
	internal static partial class IndexingLog
	{
		[LoggerMessage(EventId = 1700, Level = LogLevel.Debug, Message = "Project index: {Indexed} of {Files} file(s) read, {Symbols} symbol(s) in {Milliseconds} ms")]
		public static partial void Updated(this ILogger logger, Int32 indexed, Int32 files, Int32 symbols, Int64 milliseconds);

		[LoggerMessage(EventId = 1701, Level = LogLevel.Debug, Message = "Project index: '{File}' could not be read: {Reason}")]
		public static partial void FileNotRead(this ILogger logger, String file, String reason);

		[LoggerMessage(EventId = 1702, Level = LogLevel.Warning, Message = "Project index: the limit of {Limit} symbols was reached, the remaining files are not indexed")]
		public static partial void LimitReached(this ILogger logger, Int32 limit);
	}
}