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
