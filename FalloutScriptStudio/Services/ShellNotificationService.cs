namespace FalloutScriptStudio.Services
{
	using System;

	/// <inheritdoc />
	public sealed class ShellNotificationService : IShellNotificationService
	{
		public event EventHandler<ShellNotification>? NotificationRaised;

		public void Show(String title, String message, NotificationKind kind)
		{
			this.NotificationRaised?.Invoke(this, new ShellNotification(title, message, kind));
		}
	}
}
