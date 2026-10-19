namespace FalloutScriptStudio.Editor.Highlighting
{
	using System;
	using Microsoft.Extensions.Logging;

	/// <summary>Source-generated log messages of the editor (event ids 2000-2099).</summary>
	internal static partial class EditorLog
	{
		[LoggerMessage(EventId = 2000, Level = LogLevel.Warning, Message = "The syntax file {File} cannot be used; the built-in definition is used instead")]
		public static partial void SyntaxFileUnusable(this ILogger logger, Exception exception, String file);

		[LoggerMessage(EventId = 2001, Level = LogLevel.Warning, Message = "The syntax file {Path} cannot be read; the built-in definition is used instead")]
		public static partial void SyntaxFileUnreadable(this ILogger logger, Exception exception, String path);
	}
}