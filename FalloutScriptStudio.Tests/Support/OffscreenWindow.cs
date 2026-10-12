namespace FalloutScriptStudio.Tests.Support
{
	using System;
	using System.Windows;

	/// <summary>
	/// The only place of the tests that creates and shows a real WPF window. The tests must be invisible and must never take the focus: the
	/// window is placed far outside of every monitor, without taskbar button, without border, not activated and not topmost.
	/// Popups, tooltips, context menus and completion windows are separate top-level windows that the system moves back onto the nearest screen:
	/// a test must never let one open (fake the presenter, or disable the tooltips of the tested elements), see <see cref="DisableToolTips"/>.
	/// </summary>
	internal static class OffscreenWindow
	{
		/// <summary>The left and top coordinates of the windows, far outside of every monitor.</summary>
		public const Double Position = -20000;

		/// <summary>Creates a window that is not shown yet (it is configured by <see cref="Prepare"/>).</summary>
		/// <param name="content">The content of the window.</param>
		/// <param name="width">The width of the window.</param>
		/// <param name="height">The height of the window.</param>
		public static Window Create(Object? content = null, Double width = 800, Double height = 500)
		{
			Window window = new Window { Content = content, Width = width, Height = height };
			Prepare(window);
			return window;
		}

		/// <summary>Makes a window invisible and passive: off-screen, no taskbar button, no border, not activated, not topmost, no tooltips.</summary>
		/// <param name="window">The window, before it is shown.</param>
		public static void Prepare(Window window)
		{
			ArgumentNullException.ThrowIfNull(window);
			window.WindowStartupLocation = WindowStartupLocation.Manual;
			window.Left = Position;
			window.Top = Position;
			window.ShowInTaskbar = false;
			window.ShowActivated = false;
			window.Topmost = false;
			window.WindowStyle = WindowStyle.None;
			DisableToolTips(window);
		}

		/// <summary>Prepares and shows a window without activating it.</summary>
		/// <param name="window">The window.</param>
		public static void Show(Window window)
		{
			Prepare(window);
			window.Show();
		}

		/// <summary>Creates a prepared window around a content and shows it without activating it.</summary>
		/// <param name="content">The content of the window.</param>
		/// <param name="width">The width of the window.</param>
		/// <param name="height">The height of the window.</param>
		public static Window Show(Object content, Double width = 800, Double height = 500)
		{
			Window window = Create(content, width, height);
			window.Show();
			return window;
		}

		/// <summary>Disables the tooltips of an element and of everything inside it (the property is inherited): a tooltip is a real top-level window.</summary>
		/// <param name="element">The element.</param>
		public static void DisableToolTips(DependencyObject element)
		{
			ArgumentNullException.ThrowIfNull(element);
			System.Windows.Controls.ToolTipService.SetIsEnabled(element, false);
		}
	}
}
