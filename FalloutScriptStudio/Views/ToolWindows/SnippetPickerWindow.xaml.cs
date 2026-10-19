namespace FalloutScriptStudio.Views.ToolWindows
{
	using System;
	using System.Windows;
	using System.Windows.Input;
	using Wpf.Ui.Controls;
	using ViewModels.ToolWindows;

	/// <summary>
	/// The list of snippets (Insert Snippet, Surround With): a filter box that is typed into while the arrow keys move in the list.
	/// </summary>
	public partial class SnippetPickerWindow : FluentWindow
	{
		private readonly SnippetPickerViewModel _viewModel;

		/// <summary>Initializes a new instance of the <see cref="SnippetPickerWindow"/> class.</summary>
		/// <param name="viewModel">The view model.</param>
		public SnippetPickerWindow(SnippetPickerViewModel viewModel)
		{
			ArgumentNullException.ThrowIfNull(viewModel);

			this._viewModel = viewModel;
			this.InitializeComponent();
			ToolWindowHelper.Attach(this, viewModel);
		}

		private void OnLoaded(Object sender, RoutedEventArgs e)
		{
			this.FilterBox.Focus();
		}

		private void OnListDoubleClick(Object sender, MouseButtonEventArgs e)
		{
			if (this._viewModel.AcceptCommand.CanExecute(null))
			{
				this._viewModel.AcceptCommand.Execute(null);
			}
		}

		private void OnPreviewKeyDown(Object sender, KeyEventArgs e)
		{
			if (e.Key is not (Key.Down or Key.Up or Key.PageDown or Key.PageUp) || !this.FilterBox.IsKeyboardFocusWithin)
			{
				return;
			}

			Int32 delta = e.Key switch
			{
				Key.Down => 1,
				Key.Up => -1,
				Key.PageDown => 8,
				_ => -8,
			};
			this._viewModel.MoveSelection(delta);
			if (this._viewModel.SelectedItem is { } selected)
			{
				this.SnippetList.ScrollIntoView(selected);
			}

			e.Handled = true;
		}
	}
}