namespace FalloutScriptStudio.Views.Accessibility
{
	using System;
	using System.Windows;
	using System.Windows.Input;
	using ToolWindows;

	/// <summary>
	/// Keyboard conventions shared by the windows. <see cref="CloseOnEscapeProperty"/> gives the windows that have no cancel button
	/// (the non modal tools) the Escape key of a dialog.
	/// </summary>
	public static class WindowKeys
	{
		/// <summary>Identifies the CloseOnEscape attached property: the window closes when Escape reaches it unhandled.</summary>
		public static readonly DependencyProperty CloseOnEscapeProperty = DependencyProperty.RegisterAttached(
			"CloseOnEscape",
			typeof(Boolean),
			typeof(WindowKeys),
			new PropertyMetadata(false, OnCloseOnEscapeChanged));

		/// <summary>Gets whether the window closes on Escape.</summary>
		/// <param name="element">The window.</param>
		/// <returns><see langword="true"/> when Escape closes the window.</returns>
		public static Boolean GetCloseOnEscape(DependencyObject element)
		{
			ArgumentNullException.ThrowIfNull(element);
			return (Boolean)element.GetValue(CloseOnEscapeProperty);
		}

		/// <summary>Sets whether the window closes on Escape.</summary>
		/// <param name="element">The window.</param>
		/// <param name="value">Whether Escape closes the window.</param>
		public static void SetCloseOnEscape(DependencyObject element, Boolean value)
		{
			ArgumentNullException.ThrowIfNull(element);
			element.SetValue(CloseOnEscapeProperty, value);
		}

		private static void OnCloseOnEscapeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			if (d is not Window window)
			{
				return;
			}

			window.KeyDown -= OnWindowKeyDown;
			if ((Boolean)e.NewValue)
			{
				window.KeyDown += OnWindowKeyDown;
			}
		}

		private static void OnWindowKeyDown(Object sender, KeyEventArgs e)
		{
			// KeyDown is the bubbling event: a drop-down, a cell editor or a completion list that used Escape has already handled it.
			if (e.Key == Key.Escape && !e.Handled && e.KeyboardDevice.Modifiers == ModifierKeys.None && sender is Window window)
			{
				e.Handled = true;
				ToolWindowHelper.Close(window, false);
			}
		}
	}
}