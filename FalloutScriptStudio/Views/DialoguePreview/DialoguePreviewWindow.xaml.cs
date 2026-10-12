namespace FalloutScriptStudio.Views.DialoguePreview
{
	using System;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Input;
	using System.Windows.Media;
	using FalloutScriptStudio.ViewModels.DialoguePreview;
	using Wpf.Ui.Controls;

	/// <summary>
	/// The dialogue preview window: the replies and options of one dialogue node, like the player sees them in the game.
	/// </summary>
	public partial class DialoguePreviewWindow : FluentWindow
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="DialoguePreviewWindow"/> class.
		/// </summary>
		/// <param name="viewModel">The view model.</param>
		public DialoguePreviewWindow(DialoguePreviewViewModel viewModel)
		{
			ArgumentNullException.ThrowIfNull(viewModel);

			this.ViewModel = viewModel;
			this.DataContext = viewModel;
			this.InitializeComponent();
			viewModel.Owner = this;
			this.Activated += this.OnActivated;
			this.Closed += this.OnClosed;
		}

		/// <summary>Gets the view model.</summary>
		public DialoguePreviewViewModel ViewModel { get; }

		private async void OnActivated(Object? sender, EventArgs e)
		{
			await this.ViewModel.OnActivatedAsync();
		}

		private void OnClosed(Object? sender, EventArgs e)
		{
			this.ViewModel.Dispose();
		}

		private async void OnGridDoubleClick(Object sender, MouseButtonEventArgs e)
		{
			DataGridCell? cell = FindCell(e.OriginalSource as DependencyObject);
			if (cell?.DataContext is not DialoguePreviewRowViewModel row)
			{
				return;
			}

			if (cell.Column.DisplayIndex == 1)
			{
				await this.ViewModel.FollowCommand.ExecuteAsync(row);
			}
			else if (cell.Column.DisplayIndex == 0)
			{
				await this.ViewModel.JumpToCodeCommand.ExecuteAsync(row);
			}
		}

		/// <summary>The keyboard equivalents of the double clicks: Enter follows the option, Ctrl+Enter shows its code, F2 edits its message.</summary>
		private void OnGridKeyDown(Object sender, KeyEventArgs e)
		{
			if (((System.Windows.Controls.DataGrid)sender).SelectedItem is not DialoguePreviewRowViewModel row)
			{
				return;
			}

			if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
			{
				this.ViewModel.FollowCommand.Execute(row);
				e.Handled = true;
			}
			else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
			{
				this.ViewModel.JumpToCodeCommand.Execute(row);
				e.Handled = true;
			}
			else if (e.Key == Key.F2 && Keyboard.Modifiers == ModifierKeys.None)
			{
				this.ViewModel.EditMessageCommand.Execute(row);
				e.Handled = true;
			}
		}

		private static DataGridCell? FindCell(DependencyObject? source)
		{
			while (source is not null and not DataGridCell)
			{
				source = VisualTreeHelper.GetParent(source);
			}

			return source as DataGridCell;
		}
	}
}
