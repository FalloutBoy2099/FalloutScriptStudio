namespace FalloutScriptStudio.Views.ToolWindows
{
	using System;
	using System.Windows;
	using Core.Configuration;
	using ViewModels.ToolWindows;

	/// <summary>
	/// Plumbing shared by the code-behind of the tool windows: it connects a window to its view model
	/// and converts window geometry to and from the persisted placement.
	/// </summary>
	internal static class ToolWindowHelper
	{
		/// <summary>
		/// Sets the data context of a window and closes the window when the view model asks for it.
		/// </summary>
		/// <param name="window">The window.</param>
		/// <param name="viewModel">The view model.</param>
		public static void Attach(Window window, ToolWindowViewModelBase viewModel)
		{
			ArgumentNullException.ThrowIfNull(window);
			ArgumentNullException.ThrowIfNull(viewModel);

			window.DataContext = viewModel;
			viewModel.CloseRequested += (_, accepted) => Close(window, accepted);
		}

		/// <summary>
		/// Closes a window; a modal window reports <paramref name="accepted"/> as its dialog result.
		/// </summary>
		/// <param name="window">The window.</param>
		/// <param name="accepted">Whether the dialog was accepted.</param>
		public static void Close(Window window, Boolean accepted)
		{
			ArgumentNullException.ThrowIfNull(window);

			try
			{
				window.DialogResult = accepted;
			}
			catch (InvalidOperationException)
			{
				// The window was not shown with ShowDialog; closing it is enough.
			}

			if (window.IsVisible)
			{
				window.Close();
			}
		}

		/// <summary>
		/// Applies the stored size of a window.
		/// </summary>
		/// <param name="window">The window.</param>
		/// <param name="placement">The stored placement, or <see langword="null"/> to keep the default size.</param>
		public static void ApplySize(Window window, WindowPlacement? placement)
		{
			ArgumentNullException.ThrowIfNull(window);

			if (placement is null || placement.IsEmpty)
			{
				return;
			}

			window.Width = Math.Max(window.MinWidth, placement.Width);
			window.Height = Math.Max(window.MinHeight, placement.Height);
		}

		/// <summary>
		/// Captures the geometry of a window for persisting.
		/// </summary>
		/// <param name="window">The window.</param>
		/// <returns>The placement.</returns>
		public static WindowPlacement Capture(Window window)
		{
			ArgumentNullException.ThrowIfNull(window);

			Rect bounds = window.WindowState == WindowState.Normal
				? new Rect(window.Left, window.Top, window.ActualWidth, window.ActualHeight)
				: window.RestoreBounds;

			return new WindowPlacement
			{
				IsMaximized = window.WindowState == WindowState.Maximized,
				X = (Int32)Math.Round(bounds.X),
				Y = (Int32)Math.Round(bounds.Y),
				Width = (Int32)Math.Round(bounds.Width),
				Height = (Int32)Math.Round(bounds.Height),
			};
		}
	}
}