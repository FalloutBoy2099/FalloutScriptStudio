namespace FalloutScriptStudio.Views.Panels
{
	using System;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Input;
	using FalloutScriptStudio.ViewModels.Panels;

	/// <summary>The "Procedures" tab: tree interaction (double-click, Ctrl+click, drag to move).</summary>
	public partial class ProceduresPanelView : UserControl
	{
		private const String DragFormat = "FalloutScriptStudio.ProcedureNode";

		private Point _dragStart;
		private ProcedureNodeViewModel? _dragCandidate;

		public ProceduresPanelView()
		{
			this.InitializeComponent();
		}

		private ProceduresPanelViewModel? ViewModel => this.DataContext as ProceduresPanelViewModel;

		private void OnSelectedItemChanged(Object sender, RoutedPropertyChangedEventArgs<Object> e)
		{
			if (this.ViewModel is { } viewModel)
			{
				viewModel.SelectedNode = e.NewValue as TreeNodeViewModel;
			}
		}

		private void OnTreePreviewMouseRightButtonDown(Object sender, MouseButtonEventArgs e)
		{
			TreeViewHelper.SelectItemUnderMouse(e.OriginalSource as DependencyObject);
		}

		/// <summary>The keyboard equivalents of the mouse and of the context menu: Enter, Ctrl+Enter, F2, Delete, Alt+Up and Alt+Down.</summary>
		private void OnTreeKeyDown(Object sender, KeyEventArgs e)
		{
			if (this.ViewModel is not { } viewModel || (sender as TreeView)?.SelectedItem is not TreeNodeViewModel node)
			{
				return;
			}

			ModifierKeys modifiers = Keyboard.Modifiers;
			Key key = e.Key == Key.System ? e.SystemKey : e.Key;
			if (key == Key.Enter && modifiers == ModifierKeys.None)
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
			else if (key == Key.Enter && modifiers == ModifierKeys.Control && node.IsSymbol)
			{
				viewModel.InsertNameCommand.Execute(node);
				e.Handled = true;
			}
			else if (node is ProcedureNodeViewModel { CanModify: true } procedure)
			{
				switch (key)
				{
					case Key.F2 when modifiers == ModifierKeys.None:
						viewModel.RenameCommand.Execute(procedure);
						e.Handled = true;
						break;
					case Key.Delete when modifiers == ModifierKeys.None:
						viewModel.DeleteCommand.Execute(procedure);
						e.Handled = true;
						break;
					case Key.Up when modifiers == ModifierKeys.Alt:
						viewModel.MoveUpCommand.Execute(procedure);
						e.Handled = true;
						break;
					case Key.Down when modifiers == ModifierKeys.Alt:
						viewModel.MoveDownCommand.Execute(procedure);
						e.Handled = true;
						break;
					default:
						break;
				}
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
			this._dragCandidate = null;
			if (Keyboard.Modifiers == ModifierKeys.Control
				&& sender is TreeViewItem { DataContext: TreeNodeViewModel node }
				&& node.IsSymbol
				&& this.ViewModel is { } viewModel)
			{
				viewModel.InsertNameCommand.Execute(node);
				e.Handled = true;
			}
		}

		private void OnItemPreviewMouseLeftButtonDown(Object sender, MouseButtonEventArgs e)
		{
			this._dragStart = e.GetPosition(null);
			this._dragCandidate = (sender as TreeViewItem)?.DataContext as ProcedureNodeViewModel;
		}

		private void OnItemPreviewMouseMove(Object sender, MouseEventArgs e)
		{
			if (e.LeftButton != MouseButtonState.Pressed || this._dragCandidate is not { CanModify: true } node || sender is not TreeViewItem item)
			{
				return;
			}

			Vector delta = e.GetPosition(null) - this._dragStart;
			if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
			{
				return;
			}

			this._dragCandidate = null;
			DragDrop.DoDragDrop(item, new DataObject(DragFormat, node), DragDropEffects.Move);
		}

		private void OnItemDragOver(Object sender, DragEventArgs e)
		{
			Boolean allowed = e.Data.GetDataPresent(DragFormat)
				&& (sender as TreeViewItem)?.DataContext is ProcedureNodeViewModel { CanModify: true };
			e.Effects = allowed ? DragDropEffects.Move : DragDropEffects.None;
			e.Handled = true;
		}

		private async void OnItemDrop(Object sender, DragEventArgs e)
		{
			if (this.ViewModel is not { } viewModel
				|| !e.Data.GetDataPresent(DragFormat)
				|| e.Data.GetData(DragFormat) is not ProcedureNodeViewModel source
				|| (sender as TreeViewItem)?.DataContext is not ProcedureNodeViewModel target)
			{
				return;
			}

			e.Handled = true;
			await viewModel.MoveToAsync(source, target);
		}
	}
}
