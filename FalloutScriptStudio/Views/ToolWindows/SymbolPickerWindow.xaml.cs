namespace FalloutScriptStudio.Views.ToolWindows
{
	using System;
	using System.Windows;
	using System.Windows.Input;
	using Wpf.Ui.Controls;
	using ViewModels.ToolWindows;

	/// <summary>
	/// The symbol list: "Go to symbol" with a filter box that is typed into while the arrow keys move in the list, and the list of the
	/// places to choose from when a name is defined several times.
	/// </summary>
	public partial class SymbolPickerWindow : FluentWindow
	{
		private readonly SymbolPickerViewModel _viewModel;

		/// <summary>Initializes a new instance of the <see cref="SymbolPickerWindow"/> class.</summary>
		/// <param name="viewModel">The view model.</param>
		public SymbolPickerWindow(SymbolPickerViewModel viewModel)
		{
			ArgumentNullException.ThrowIfNull(viewModel);

			this._viewModel = viewModel;
			this.InitializeComponent();
			ToolWindowHelper.Attach(this, viewModel);
			this.Closed += (_, _) => viewModel.Dispose();
		}

		private void OnLoaded(Object sender, RoutedEventArgs e)
		{
			if (this._viewModel.IsFilterVisible)
			{
				this.FilterBox.Focus();
			}
			else
			{
				this.FocusSelectedItem();
			}
		}

		private void FocusSelectedItem()
		{
			this.SymbolList.Focus();
			if (this._viewModel.SelectedItem is { } selected)
			{
				this.SymbolList.ScrollIntoView(selected);
				if (this.SymbolList.ItemContainerGenerator.ContainerFromItem(selected) is IInputElement container)
				{
					_ = container.Focus();
				}
			}
		}

		private void OnListDoubleClick(Object sender, MouseButtonEventArgs e)
		{
			if (this._viewModel.AcceptCommand.CanExecute(null))
			{
				this._viewModel.AcceptCommand.Execute(null);
			}
		}

		private void OnPreviewKeyDown(Object sender, KeyEventArgs e)
		{
			if (e.Key is not (Key.Down or Key.Up or Key.PageDown or Key.PageUp) || !this.FilterBox.IsKeyboardFocusWithin)
			{
				return;
			}

			// The arrow keys move the selection while the filter box keeps the focus, so the user can type and choose without leaving it.
			Int32 delta = e.Key switch
			{
				Key.Down => 1,
				Key.Up => -1,
				Key.PageDown => 8,
				_ => -8,
			};
			this._viewModel.MoveSelection(delta);
			if (this._viewModel.SelectedItem is { } selected)
			{
				this.SymbolList.ScrollIntoView(selected);
			}

			e.Handled = true;
		}
	}
}