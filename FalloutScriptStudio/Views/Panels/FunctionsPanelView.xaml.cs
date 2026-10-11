namespace FalloutScriptStudio.Views.Panels
{
	using System;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Input;
	using FalloutScriptStudio.ViewModels.Panels;

	/// <summary>The functions tree: Ctrl+click or double-click inserts the code of a function.</summary>
	public partial class FunctionsPanelView : UserControl
	{
		public FunctionsPanelView()
		{
			this.InitializeComponent();
		}

		private FunctionsPanelViewModel? ViewModel => this.DataContext as FunctionsPanelViewModel;

		private void OnSelectedItemChanged(Object sender, RoutedPropertyChangedEventArgs<Object> e)
		{
			if (this.ViewModel is { } viewModel)
			{
				viewModel.SelectedNode = e.NewValue as FunctionNodeViewModel;
			}
		}

		private void OnTreePreviewMouseRightButtonDown(Object sender, MouseButtonEventArgs e)
		{
			TreeViewHelper.SelectItemUnderMouse(e.OriginalSource as DependencyObject);
		}

		private void OnItemMouseDoubleClick(Object sender, MouseButtonEventArgs e)
		{
			if (sender is TreeViewItem { DataContext: FunctionNodeViewModel { IsFunction: true } node } item && item.IsSelected && this.ViewModel is { } viewModel)
			{
				viewModel.InsertCommand.Execute(node);
				e.Handled = true;
			}
		}

		private void OnItemPreviewMouseLeftButtonUp(Object sender, MouseButtonEventArgs e)
		{
			if (Keyboard.Modifiers == ModifierKeys.Control
				&& sender is TreeViewItem { DataContext: FunctionNodeViewModel { IsFunction: true } node }
				&& this.ViewModel is { } viewModel)
			{
				viewModel.InsertCommand.Execute(node);
				e.Handled = true;
			}
		}
	}
}
