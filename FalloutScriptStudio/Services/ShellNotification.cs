namespace FalloutScriptStudio.Services
{
	using System;

	/// <summary>A transient message shown over the main window (the "Compiled successfully" popup).</summary>
	/// <param name="Title">The short headline.</param>
	/// <param name="Message">The detail line.</param>
	/// <param name="Kind">The look of the notification.</param>
	public sealed record ShellNotification(String Title, String Message, NotificationKind Kind);
}
