namespace FalloutScriptStudio.Views.Shell
{
	using System;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Input;
	using FalloutScriptStudio.ViewModels.Main;

	/// <summary>The quick find / replace bar.</summary>
	public partial class SearchBarView : UserControl
	{
		public SearchBarView()
		{
			this.InitializeComponent();
			this.DataContextChanged += this.OnDataContextChanged;
		}

		private SearchBarViewModel? ViewModel => this.DataContext as SearchBarViewModel;

		private void OnDataContextChanged(Object sender, DependencyPropertyChangedEventArgs e)
		{
			if (e.OldValue is SearchBarViewModel old)
			{
				old.FocusRequested -= this.OnFocusRequested;
			}

			if (e.NewValue is SearchBarViewModel viewModel)
			{
				viewModel.FocusRequested += this.OnFocusRequested;
			}
		}

		private void OnFocusRequested(Object? sender, EventArgs e)
		{
			this.Dispatcher.BeginInvoke(new Action(() =>
			{
				this.SearchBox.Focus();
				if (this.SearchBox.Template.FindName("PART_EditableTextBox", this.SearchBox) is TextBox editor)
				{
					editor.Focus();
					editor.SelectAll();
				}
			}));
		}

		private void OnSearchBoxPreviewKeyDown(Object sender, KeyEventArgs e)
		{
			if (this.ViewModel is not { } viewModel)
			{
				return;
			}

			switch (e.Key)
			{
				case Key.Enter:
					if (Keyboard.Modifiers == ModifierKeys.Shift)
					{
						viewModel.FindPreviousCommand.Execute(null);
					}
					else
					{
						viewModel.FindNextCommand.Execute(null);
					}

					e.Handled = true;
					break;
				case Key.Escape:
					viewModel.HideCommand.Execute(null);
					e.Handled = true;
					break;
			}
		}

		private void OnReplaceBoxPreviewKeyDown(Object sender, KeyEventArgs e)
		{
			if (this.ViewModel is not { } viewModel)
			{
				return;
			}

			if (e.Key == Key.Enter)
			{
				viewModel.ReplaceCommand.Execute(null);
				e.Handled = true;
			}
			else if (e.Key == Key.Escape)
			{
				viewModel.HideCommand.Execute(null);
				e.Handled = true;
			}
		}
	}
}
