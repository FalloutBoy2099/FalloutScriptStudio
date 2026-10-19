namespace FalloutScriptStudio.Core.Building
{
	using System;
	using Microsoft.Extensions.Logging;

	internal static partial class BuildingLog
	{
		[LoggerMessage(EventId = 2600, Level = LogLevel.Warning, Message = "The build cache {Path} is damaged; every script is built again")]
		public static partial void CacheDamaged(this ILogger logger, Exception exception, String path);

		[LoggerMessage(EventId = 2601, Level = LogLevel.Warning, Message = "The build cache {Path} cannot be read; every script is built again")]
		public static partial void CacheUnreadable(this ILogger logger, Exception exception, String path);

		[LoggerMessage(EventId = 2602, Level = LogLevel.Warning, Message = "The build cache {Path} could not be saved; the next run builds the scripts again")]
		public static partial void CacheNotSaved(this ILogger logger, Exception exception, String path);

		[LoggerMessage(EventId = 2603, Level = LogLevel.Information, Message = "Build plan: {Total} scripts, {Stale} to build, {UpToDate} up to date")]
		public static partial void Planned(this ILogger logger, Int32 total, Int32 stale, Int32 upToDate);
	}
}