namespace FalloutScriptStudio.Views.Accessibility
{
	using System;
	using System.Windows;

	/// <summary>
	/// Keeps a window inside the work area of the screen. With a display scale of 150% or 200% a window that is 700 units high does not
	/// fit on a 1080p screen (540 units at 200%), and its buttons would be out of reach, so its size and its minimum size are limited.
	/// </summary>
	public static class WindowFit
	{
		/// <summary>Identifies the FitToWorkArea attached property.</summary>
		public static readonly DependencyProperty FitToWorkAreaProperty = DependencyProperty.RegisterAttached(
			"FitToWorkArea",
			typeof(Boolean),
			typeof(WindowFit),
			new PropertyMetadata(false, OnFitToWorkAreaChanged));

		/// <summary>Gets whether the window is limited to the work area.</summary>
		/// <param name="element">The window.</param>
		/// <returns><see langword="true"/> when the size of the window is limited.</returns>
		public static Boolean GetFitToWorkArea(DependencyObject element)
		{
			ArgumentNullException.ThrowIfNull(element);
			return (Boolean)element.GetValue(FitToWorkAreaProperty);
		}

		/// <summary>Sets whether the window is limited to the work area.</summary>
		/// <param name="element">The window.</param>
		/// <param name="value">Whether the size of the window is limited.</param>
		public static void SetFitToWorkArea(DependencyObject element, Boolean value)
		{
			ArgumentNullException.ThrowIfNull(element);
			element.SetValue(FitToWorkAreaProperty, value);
		}

		/// <summary>Limits the size and the minimum size of a window to an area.</summary>
		/// <param name="window">The window.</param>
		/// <param name="area">The size of the work area, in device independent units.</param>
		public static void Fit(Window window, Size area)
		{
			ArgumentNullException.ThrowIfNull(window);

			window.MinWidth = Math.Min(window.MinWidth, area.Width);
			window.MinHeight = Math.Min(window.MinHeight, area.Height);
			if (!Double.IsNaN(window.Width))
			{
				window.Width = Math.Min(window.Width, area.Width);
			}

			if (!Double.IsNaN(window.Height))
			{
				window.Height = Math.Min(window.Height, area.Height);
			}
		}

		private static void OnFitToWorkAreaChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			if (d is not Window window)
			{
				return;
			}

			window.Initialized -= OnWindowInitialized;
			if ((Boolean)e.NewValue)
			{
				window.Initialized += OnWindowInitialized;
			}
		}

		private static void OnWindowInitialized(Object? sender, EventArgs e)
		{
			if (sender is Window window)
			{
				Fit(window, SystemParameters.WorkArea.Size);
			}
		}
	}
}
