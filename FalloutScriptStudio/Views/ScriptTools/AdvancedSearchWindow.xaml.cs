namespace FalloutScriptStudio.Views.ScriptTools
{
	using System;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Input;
	using FalloutScriptStudio.ViewModels.ScriptTools;
	using Wpf.Ui.Controls;

	/// <summary>
	/// The advanced "Search &amp; Replace" window.
	/// </summary>
	public partial class AdvancedSearchWindow : FluentWindow
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="AdvancedSearchWindow"/> class.
		/// </summary>
		/// <param name="viewModel">The view model.</param>
		public AdvancedSearchWindow(AdvancedSearchViewModel viewModel)
		{
			ArgumentNullException.ThrowIfNull(viewModel);

			this.ViewModel = viewModel;
			this.DataContext = viewModel;
			this.InitializeComponent();
			ScriptToolWindowHelper.CloseWhenRequested(this, viewModel);
			this.Activated += (_, _) => this.ViewModel.RefreshHistory();
		}

		/// <summary>Gets the view model.</summary>
		public AdvancedSearchViewModel ViewModel { get; }

		/// <summary>Gives the focus to the search field and selects its text.</summary>
		public void FocusSearchText()
		{
			this.SearchBox.Focus();
			if (this.SearchBox.Template.FindName("PART_EditableTextBox", this.SearchBox) is System.Windows.Controls.TextBox editor)
			{
				editor.Focus();
				editor.SelectAll();
			}
		}

		private void OnWindowKeyDown(Object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Escape && !this.SearchBox.IsDropDownOpen)
			{
				e.Handled = true;
				this.Close();
			}
		}

		private void OnResultDoubleClick(Object sender, MouseButtonEventArgs e)
		{
			if (this.ResultsGrid.SelectedItem is SearchResultItem item && this.ViewModel.GoToResultCommand.CanExecute(item))
			{
				this.ViewModel.GoToResultCommand.Execute(item);
			}
		}
	}
}
