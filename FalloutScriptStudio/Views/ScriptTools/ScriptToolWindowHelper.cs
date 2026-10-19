namespace FalloutScriptStudio.Views.ScriptTools
{
	using System;
	using System.Linq;
	using System.Windows;
	using ViewModels.ScriptTools;

	/// <summary>
	/// Plumbing shared by the code-behind of the script tool windows.
	/// </summary>
	internal static class ScriptToolWindowHelper
	{
		/// <summary>Closes a window when its view model asks for it; a modal window reports the dialog result.</summary>
		/// <param name="window">The window.</param>
		/// <param name="viewModel">The view model.</param>
		public static void CloseWhenRequested(Window window, ScriptToolViewModelBase viewModel)
		{
			ArgumentNullException.ThrowIfNull(window);
			ArgumentNullException.ThrowIfNull(viewModel);

			viewModel.CloseRequested += (_, accepted) => Close(window, accepted);
		}

		/// <summary>Closes a window; a modal window reports <paramref name="accepted"/> as its dialog result.</summary>
		/// <param name="window">The window.</param>
		/// <param name="accepted">Whether the dialog was accepted.</param>
		public static void Close(Window window, Boolean accepted)
		{
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

		/// <summary>Finds the window that should own a tool window: the active window, otherwise the main window.</summary>
		/// <param name="window">The tool window itself, which can never be its own owner.</param>
		/// <returns>The owner, or <see langword="null"/> when the application has no other visible window.</returns>
		public static Window? FindOwner(Window window)
		{
			Application? application = Application.Current;
			if (application is null)
			{
				return null;
			}

			Window? owner = application.Windows.OfType<Window>().FirstOrDefault(candidate => candidate.IsActive && !ReferenceEquals(candidate, window));
			owner ??= application.MainWindow;
			return owner is not null && !ReferenceEquals(owner, window) && owner.IsVisible ? owner : null;
		}
	}
}