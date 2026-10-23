namespace FalloutScriptStudio.Views.Panels
{
	using System;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Input;
	using ViewModels.Panels;

	/// <summary>The "Globals" tab: tree interaction (Enter and double-click go to the use).</summary>
	public partial class GlobalsPanelView : UserControl
	{
		public GlobalsPanelView()
		{
			this.InitializeComponent();
		}

		private GlobalsPanelViewModel? ViewModel => this.DataContext as GlobalsPanelViewModel;

		private void OnTreePreviewMouseRightButtonDown(Object sender, MouseButtonEventArgs e)
		{
			TreeViewHelper.SelectItemUnderMouse(e.OriginalSource as DependencyObject);
		}

		/// <summary>The keyboard equivalents of the mouse: Enter goes to the use, or opens a group.</summary>
		private void OnTreeKeyDown(Object sender, KeyEventArgs e)
		{
			if (this.ViewModel is not { } viewModel || (sender as TreeView)?.SelectedItem is not TreeNodeViewModel node)
			{
				return;
			}

			if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
			{
				if (node.IsSymbol)
				{
					viewModel.NavigateCommand.Execute(node);
				}
				else
				{
					node.IsExpanded = !node.IsExpanded;
				}

				e.Handled = true;
			}
		}

		private void OnItemMouseDoubleClick(Object sender, MouseButtonEventArgs e)
		{
			if (sender is TreeViewItem { DataContext: TreeNodeViewModel node } item && item.IsSelected && node.IsSymbol && this.ViewModel is { } viewModel)
			{
				viewModel.NavigateCommand.Execute(node);
				e.Handled = true;
			}
		}
	}
}