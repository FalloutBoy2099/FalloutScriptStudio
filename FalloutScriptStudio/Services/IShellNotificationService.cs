namespace FalloutScriptStudio.Services
{
	using System;

	/// <summary>Raises transient notifications that the main window shows as a toast card.</summary>
	public interface IShellNotificationService
	{
		/// <summary>Raised when a notification should be shown.</summary>
		event EventHandler<ShellNotification>? NotificationRaised;

		/// <summary>Raises a notification.</summary>
		void Show(String title, String message, NotificationKind kind);
	}
}
