namespace FalloutScriptStudio.Views.MessageEditor
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Input;
	using System.Windows.Media;
	using Localization;
	using ViewModels.MessageEditor;

	/// <summary>
	/// Connects the message grid to the view model: pushes the selection, executes focus requests, decides which cells may be
	/// edited, handles the keyboard of the grid and of the cell editors, and keeps the column headers and options up to date.
	/// </summary>
	internal sealed class MessageGridController
	{
		private static readonly String[] HeaderKeys =
		{
			"Str.MessageEditor.Col.Line",
			"Str.MessageEditor.Col.Player",
			"Str.MessageEditor.Col.Text",
			"Str.MessageEditor.Col.Comment",
			"Str.MessageEditor.Col.Lip",
		};

		private readonly DataGrid _grid;
		private readonly DataGridColumn[] _columns;
		private readonly MessageEditorViewModel _viewModel;
		private Boolean _clearNextEdit;

		/// <summary>Initializes the controller.</summary>
		/// <param name="grid">The message grid.</param>
		/// <param name="columns">The columns in the order of <see cref="MessageGridColumn"/>.</param>
		/// <param name="viewModel">The view model of the window.</param>
		public MessageGridController(DataGrid grid, DataGridColumn[] columns, MessageEditorViewModel viewModel)
		{
			ArgumentNullException.ThrowIfNull(grid);
			ArgumentNullException.ThrowIfNull(columns);
			ArgumentNullException.ThrowIfNull(viewModel);
			this._grid = grid;
			this._columns = columns;
			this._viewModel = viewModel;
		}

		/// <summary>Subscribes to the events of the grid.</summary>
		public void Attach()
		{
			this._grid.BeginningEdit += this.OnBeginningEdit;
			this._grid.PreparingCellForEdit += this.OnPreparingCellForEdit;
			this._grid.CellEditEnding += (_, _) => this._viewModel.IsCellEditing = false;
			this._grid.SelectionChanged += (_, _) => this.PushSelection();
			this._grid.CurrentCellChanged += (_, _) => this.PushSelection();
			this._grid.PreviewKeyDown += this.OnPreviewKeyDown;
			this._grid.PreviewMouseDown += this.OnPreviewMouseDown;
		}

		/// <summary>Sets the headers of the columns (they are not part of the element tree and cannot use a <c>DynamicResource</c>).</summary>
		public void RefreshHeaders()
		{
			for (Int32 index = 0; index < HeaderKeys.Length; index++)
			{
				this._columns[index].Header = Loc.Get(HeaderKeys[index]);
			}
		}

		/// <summary>Applies the visibility of the sound column and the width of the line column.</summary>
		public void ApplyOptions()
		{
			this._columns[(Int32)MessageGridColumn.Sound].Visibility = this._viewModel.ShowLipColumn ? Visibility.Visible : Visibility.Collapsed;
			this._columns[(Int32)MessageGridColumn.Line].Width = new DataGridLength(this._viewModel.LineColumnWidth);
		}

		/// <summary>Selects the requested rows and cell, scrolls to them and optionally starts editing.</summary>
		/// <param name="request">The request of the view model.</param>
		/// <param name="searchHasFocus">Whether the search box has the keyboard focus (the grid then keeps it away unless it edits).</param>
		public void ApplyFocus(RowFocusRequestedEventArgs request, Boolean searchHasFocus)
		{
			if (request.Rows.Count == 0 || !this._viewModel.RowsView.Contains(request.Rows[0]))
			{
				return;
			}

			this._grid.SelectedItems.Clear();
			foreach (MessageRowViewModel row in request.Rows)
			{
				if (this._viewModel.RowsView.Contains(row))
				{
					this._grid.SelectedItems.Add(row);
				}
			}

			DataGridColumn column = this._columns[(Int32)request.Column];
			this._grid.CurrentCell = new DataGridCellInfo(request.Rows[0], column);
			this._grid.ScrollIntoView(request.Rows[0], column);
			if (request.BeginEdit || !searchHasFocus)
			{
				this._grid.Focus();
			}

			if (request.BeginEdit)
			{
				this._grid.BeginEdit();
			}
		}

		/// <summary>Finishes the cell edit in progress (a failed commit brings the old value back).</summary>
		public void CommitEdit()
		{
			if (!this._viewModel.IsCellEditing)
			{
				return;
			}

			if (!this._grid.CommitEdit(DataGridEditingUnit.Row, true))
			{
				this._grid.CancelEdit(DataGridEditingUnit.Row);
			}

			this._viewModel.IsCellEditing = false;
		}

		/// <summary>Handles the keys of a cell editor: Ctrl+Enter or Shift+Enter inserts a line break into a message text.</summary>
		/// <param name="sender">The editor.</param>
		/// <param name="e">The key event.</param>
		public void HandleCellEditorKey(Object sender, KeyEventArgs e)
		{
			if (e.Key != Key.Enter || (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) == 0)
			{
				return;
			}

			// Ctrl+Enter or Shift+Enter inserts a line break into a message text; plain Enter finishes the edit.
			if (sender is TextBox box && this.MapColumn(this._grid.CurrentCell.Column) == MessageGridColumn.Text)
			{
				Int32 start = box.SelectionStart;
				box.SelectedText = Environment.NewLine;
				box.CaretIndex = start + Environment.NewLine.Length;
				e.Handled = true;
			}
		}

		private static T? FindAncestor<T>(DependencyObject start)
			where T : DependencyObject
		{
			DependencyObject? current = start;
			while (current is not null and not T)
			{
				current = current is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(current) : LogicalTreeHelper.GetParent(current);
			}

			return current as T;
		}

		private MessageGridColumn MapColumn(DataGridColumn? column)
		{
			Int32 index = column is null ? -1 : Array.IndexOf(this._columns, column);
			return index < 0 ? MessageGridColumn.Line : (MessageGridColumn)index;
		}

		private void PushSelection()
		{
			List<MessageRowViewModel> selected = this._grid.SelectedItems.OfType<MessageRowViewModel>().ToList();
			MessageRowViewModel? current = this._grid.CurrentItem as MessageRowViewModel;
			if (current is null || !selected.Contains(current))
			{
				current = selected.FirstOrDefault();
			}

			this._viewModel.UpdateSelection(current, selected, this.MapColumn(this._grid.CurrentCell.Column));
		}

		private void OnBeginningEdit(Object? sender, DataGridBeginningEditEventArgs e)
		{
			if (e.Row.Item is not MessageRowViewModel row)
			{
				return;
			}

			Boolean allowed = this.MapColumn(e.Column) switch
			{
				MessageGridColumn.Line => row.CanEditLine,
				MessageGridColumn.Text => true,
				MessageGridColumn.Comment or MessageGridColumn.Sound => row.IsMessage,
				_ => false,
			};
			if (!allowed)
			{
				e.Cancel = true;
				this._clearNextEdit = false;
				return;
			}

			this._viewModel.IsCellEditing = true;
		}

		private void OnPreparingCellForEdit(Object? sender, DataGridPreparingCellForEditEventArgs e)
		{
			if (this._clearNextEdit && e.EditingElement is TextBox box)
			{
				box.Text = String.Empty;
			}

			this._clearNextEdit = false;
		}

		private void OnPreviewKeyDown(Object sender, KeyEventArgs e)
		{
			if (this._viewModel.IsCellEditing)
			{
				return;
			}

			Boolean control = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
			Boolean shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
			Boolean alt = (Keyboard.Modifiers & ModifierKeys.Alt) != 0;
			if (alt)
			{
				return;
			}

			switch (e.Key)
			{
				case Key.Enter:
					if (control)
					{
						this._viewModel.InsertBlankBelowCommand.Execute(null);
						e.Handled = true;
					}
					else if (shift)
					{
						this._viewModel.InsertBlankAboveCommand.Execute(null);
						e.Handled = true;
					}
					else if (this._grid.SelectedItems.Count <= 1)
					{
						this._viewModel.ActivateCurrentCell();
						e.Handled = true;
					}

					break;
				case Key.Delete when control:
					this._viewModel.DeleteRowsCommand.Execute(null);
					e.Handled = true;
					break;
				case Key.Delete or Key.Back when !control:
					// Starts editing with an empty cell; Escape brings the old text back.
					if (this._grid.SelectedItems.Count <= 1)
					{
						this._clearNextEdit = true;
						this._grid.BeginEdit();
					}

					e.Handled = true;
					break;
				case Key.Up when control:
					this._viewModel.MoveUpCommand.Execute(null);
					e.Handled = true;
					break;
				case Key.Down when control:
					this._viewModel.MoveDownCommand.Execute(null);
					e.Handled = true;
					break;
				default:
					break;
			}
		}

		private void OnPreviewMouseDown(Object sender, MouseButtonEventArgs e)
		{
			if (e.ChangedButton != MouseButton.Middle || e.OriginalSource is not DependencyObject source)
			{
				return;
			}

			DataGridRow? row = FindAncestor<DataGridRow>(source);
			if (row?.Item is MessageRowViewModel message)
			{
				this._viewModel.SendRow(message);
				e.Handled = true;
			}
		}
	}
}