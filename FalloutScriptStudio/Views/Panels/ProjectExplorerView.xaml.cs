namespace FalloutScriptStudio.Views.Panels
{
	using System;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Input;
	using FalloutScriptStudio.ViewModels.Panels;

	/// <summary>The "Solution" tab: the project folder as a tree.</summary>
	public partial class ProjectExplorerView : UserControl
	{
		public ProjectExplorerView()
		{
			this.InitializeComponent();
		}

		private void OnTreePreviewMouseRightButtonDown(Object sender, MouseButtonEventArgs e)
		{
			TreeViewHelper.SelectItemUnderMouse(e.OriginalSource as DependencyObject);
		}

		/// <summary>The keyboard equivalents of the mouse and of the context menu: Enter opens a file or toggles a folder, Ctrl+C copies the path.</summary>
		private void OnTreeKeyDown(Object sender, KeyEventArgs e)
		{
			if (this.DataContext is not ProjectExplorerViewModel viewModel || (sender as TreeView)?.SelectedItem is not ProjectNodeViewModel node)
			{
				return;
			}

			if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
			{
				if (node.IsDirectory)
				{
					node.IsExpanded = !node.IsExpanded;
				}
				else
				{
					viewModel.OpenCommand.Execute(node);
				}

				e.Handled = true;
			}
			else if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
			{
				viewModel.CopyPathCommand.Execute(node);
				e.Handled = true;
			}
		}

		private void OnItemMouseDoubleClick(Object sender, MouseButtonEventArgs e)
		{
			if (sender is TreeViewItem { DataContext: ProjectNodeViewModel { IsDirectory: false } node } item
				&& item.IsSelected
				&& this.DataContext is ProjectExplorerViewModel viewModel)
			{
				viewModel.OpenCommand.Execute(node);
				e.Handled = true;
			}
		}
	}
}
