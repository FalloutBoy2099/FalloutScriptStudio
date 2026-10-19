namespace FalloutScriptStudio.Views.MessageEditor
{
	using System;
	using System.Linq;
	using System.Windows;
	using Core.Configuration;

	/// <summary>Restores and stores the position and size of a Message Editor window in the user settings.</summary>
	internal static class MessageEditorPlacement
	{
		private const Double CascadeOffset = 32;
		private const Int32 MaxCascade = 8;

		/// <summary>
		/// Moves and sizes the window to the stored placement, kept inside the virtual screen and shifted for every other
		/// visible Message Editor window so that the windows do not cover each other.
		/// </summary>
		public static void Restore(MessageEditorWindow window, ISettingsService settings)
		{
			WindowPlacement? placement = settings.Settings.Window.GetPlacement(WindowKind.MessageEditor);
			if (placement is null)
			{
				return;
			}

			Int32 siblings = Application.Current?.Windows.OfType<MessageEditorWindow>().Count(other => !ReferenceEquals(other, window) && other.IsVisible) ?? 0;
			Double offset = Math.Min(siblings, MaxCascade) * CascadeOffset;

			Double screenLeft = SystemParameters.VirtualScreenLeft;
			Double screenTop = SystemParameters.VirtualScreenTop;
			Double screenWidth = SystemParameters.VirtualScreenWidth;
			Double screenHeight = SystemParameters.VirtualScreenHeight;

			Double width = Math.Clamp(placement.Width, window.MinWidth, screenWidth);
			Double height = Math.Clamp(placement.Height, window.MinHeight, screenHeight);
			Double left = Math.Clamp(placement.X + offset, screenLeft, Math.Max(screenLeft, screenLeft + screenWidth - width));
			Double top = Math.Clamp(placement.Y + offset, screenTop, Math.Max(screenTop, screenTop + screenHeight - height));

			window.WindowStartupLocation = WindowStartupLocation.Manual;
			window.Width = width;
			window.Height = height;
			window.Left = left;
			window.Top = top;
			if (placement.IsMaximized)
			{
				window.WindowState = WindowState.Maximized;
			}
		}

		/// <summary>Stores the placement of the window (nothing is stored while it is minimized).</summary>
		public static void Store(MessageEditorWindow window, ISettingsService settings)
		{
			if (window.WindowState == WindowState.Minimized)
			{
				return;
			}

			Rect bounds = window.WindowState == WindowState.Normal ? new Rect(window.Left, window.Top, window.Width, window.Height) : window.RestoreBounds;
			if (bounds.Width <= 0 || bounds.Height <= 0)
			{
				return;
			}

			settings.Settings.Window.SetPlacement(
				WindowKind.MessageEditor,
				new WindowPlacement
				{
					IsMaximized = window.WindowState == WindowState.Maximized,
					X = (Int32)bounds.X,
					Y = (Int32)bounds.Y,
					Width = (Int32)bounds.Width,
					Height = (Int32)bounds.Height,
				});
		}
	}
}