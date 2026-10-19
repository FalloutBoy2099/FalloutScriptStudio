namespace FalloutScriptStudio.Views.Shell
{
	using System;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Input;
	using Documents;
	using ViewModels.Main;

	/// <summary>The strip of document tabs: selection, close buttons, drag to reorder and the list of all tabs.</summary>
	public partial class DocumentTabStrip : UserControl
	{
		private const String DragFormat = "FalloutScriptStudio.DocumentTab";

		private readonly ContextMenu _allTabsMenu = new();
		private Point _dragStart;
		private DocumentViewModel? _dragCandidate;

		public DocumentTabStrip()
		{
			this.InitializeComponent();
		}

		private MainViewModel? ViewModel => this.DataContext as MainViewModel;

		private void OnTabsSelectionChanged(Object sender, SelectionChangedEventArgs e)
		{
			if (this.Tabs.SelectedItem is not null)
			{
				this.Tabs.ScrollIntoView(this.Tabs.SelectedItem);
			}
		}

		private void OnTabsPreviewMouseWheel(Object sender, MouseWheelEventArgs e)
		{
			ScrollViewer? scroller = FindScrollViewer(this.Tabs);
			if (scroller is null)
			{
				return;
			}

			scroller.ScrollToHorizontalOffset(scroller.HorizontalOffset - e.Delta);
			e.Handled = true;
		}

		private void OnTabPreviewMouseLeftButtonDown(Object sender, MouseButtonEventArgs e)
		{
			this._dragStart = e.GetPosition(null);
			this._dragCandidate = (sender as ListBoxItem)?.DataContext as DocumentViewModel;
		}

		private void OnTabPreviewMouseMove(Object sender, MouseEventArgs e)
		{
			if (e.LeftButton != MouseButtonState.Pressed || this._dragCandidate is null || sender is not ListBoxItem item)
			{
				return;
			}

			Vector delta = e.GetPosition(null) - this._dragStart;
			if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
			{
				return;
			}

			DocumentViewModel dragged = this._dragCandidate;
			this._dragCandidate = null;
			DragDrop.DoDragDrop(item, new DataObject(DragFormat, dragged), DragDropEffects.Move);
		}

		private void OnTabPreviewMouseUp(Object sender, MouseButtonEventArgs e)
		{
			this._dragCandidate = null;
			if (e.ChangedButton == MouseButton.Middle && (sender as ListBoxItem)?.DataContext is DocumentViewModel document)
			{
				this.ViewModel?.CloseDocumentCommand.Execute(document);
				e.Handled = true;
			}
		}

		private void OnTabDragOver(Object sender, DragEventArgs e)
		{
			e.Effects = e.Data.GetDataPresent(DragFormat) ? DragDropEffects.Move : DragDropEffects.None;
			e.Handled = true;
		}

		private void OnTabDrop(Object sender, DragEventArgs e)
		{
			if (this.ViewModel is not { } viewModel
				|| !e.Data.GetDataPresent(DragFormat)
				|| e.Data.GetData(DragFormat) is not DocumentViewModel dragged
				|| (sender as ListBoxItem)?.DataContext is not DocumentViewModel target)
			{
				return;
			}

			Int32 oldIndex = viewModel.Documents.IndexOf(dragged);
			Int32 newIndex = viewModel.Documents.IndexOf(target);
			viewModel.MoveDocument(oldIndex, newIndex);
			viewModel.ActiveDocument = dragged;
			e.Handled = true;
		}

		private void OnAllTabsClick(Object sender, RoutedEventArgs e)
		{
			if (this.ViewModel is not { } viewModel)
			{
				return;
			}

			this._allTabsMenu.Items.Clear();
			foreach (DocumentViewModel document in viewModel.Documents)
			{
				MenuItem item = new()
				{ Header = document.Title, IsChecked = document.IsActive, Tag = document };
				item.Click += (_, _) => viewModel.ActiveDocument = document;
				this._allTabsMenu.Items.Add(item);
			}

			this._allTabsMenu.PlacementTarget = sender as UIElement;
			this._allTabsMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
			this._allTabsMenu.IsOpen = true;
		}

		private static ScrollViewer? FindScrollViewer(DependencyObject root)
		{
			if (root is ScrollViewer viewer)
			{
				return viewer;
			}

			for (Int32 i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
			{
				ScrollViewer? found = FindScrollViewer(System.Windows.Media.VisualTreeHelper.GetChild(root, i));
				if (found is not null)
				{
					return found;
				}
			}

			return null;
		}
	}
}