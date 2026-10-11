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
