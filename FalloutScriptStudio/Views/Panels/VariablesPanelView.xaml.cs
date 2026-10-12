namespace FalloutScriptStudio.Views.Panels
{
	using System;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Input;
	using FalloutScriptStudio.ViewModels.Panels;

	/// <summary>The "Variables" tab: tree interaction (double-click and Ctrl+click).</summary>
	public partial class VariablesPanelView : UserControl
	{
		public VariablesPanelView()
		{
			this.InitializeComponent();
		}

		private VariablesPanelViewModel? ViewModel => this.DataContext as VariablesPanelViewModel;

		private void OnTreePreviewMouseRightButtonDown(Object sender, MouseButtonEventArgs e)
		{
			TreeViewHelper.SelectItemUnderMouse(e.OriginalSource as DependencyObject);
		}

		/// <summary>The keyboard equivalents of the mouse and of the context menu: Enter, Ctrl+Enter and F2.</summary>
		private void OnTreeKeyDown(Object sender, KeyEventArgs e)
		{
			if (this.ViewModel is not { } viewModel || (sender as TreeView)?.SelectedItem is not TreeNodeViewModel node)
			{
				return;
			}

			ModifierKeys modifiers = Keyboard.Modifiers;
			if (e.Key == Key.Enter && modifiers == ModifierKeys.None)
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
			else if (e.Key == Key.Enter && modifiers == ModifierKeys.Control && node.IsSymbol)
			{
				viewModel.InsertNameCommand.Execute(node);
				e.Handled = true;
			}
			else if (e.Key == Key.F2 && modifiers == ModifierKeys.None && node.IsSymbol)
			{
				viewModel.RenameCommand.Execute(node);
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

		private void OnItemPreviewMouseLeftButtonUp(Object sender, MouseButtonEventArgs e)
		{
			if (Keyboard.Modifiers == ModifierKeys.Control
				&& sender is TreeViewItem { DataContext: TreeNodeViewModel node }
				&& node.IsSymbol
				&& this.ViewModel is { } viewModel)
			{
				viewModel.InsertNameCommand.Execute(node);
				e.Handled = true;
			}
		}
	}
}
