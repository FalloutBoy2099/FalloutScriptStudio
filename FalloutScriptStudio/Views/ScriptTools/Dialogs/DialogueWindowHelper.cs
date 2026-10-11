namespace FalloutScriptStudio.Views.ScriptTools.Dialogs
{
	using System;
	using System.Windows;
	using FalloutScriptStudio.ViewModels.ScriptTools.Dialogs;

	/// <summary>
	/// Plumbing shared by the code-behind of the dialogue dialogs.
	/// </summary>
	internal static class DialogueWindowHelper
	{
		/// <summary>Sets the data context of a window and closes the window when the view model asks for it.</summary>
		/// <param name="window">The window.</param>
		/// <param name="viewModel">The view model.</param>
		public static void Attach(Window window, DialogueDialogViewModelBase viewModel)
		{
			ArgumentNullException.ThrowIfNull(window);
			ArgumentNullException.ThrowIfNull(viewModel);

			window.DataContext = viewModel;
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
	}
}
