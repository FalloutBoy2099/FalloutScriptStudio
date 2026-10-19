namespace FalloutScriptStudio.Views.ScriptTools
{
	using System;
	using System.Windows.Input;
	using Wpf.Ui.Controls;
	using Core.Configuration;
	using ViewModels.ScriptTools;
	using Views.ToolWindows;

	/// <summary>
	/// The advanced "Search &amp; Replace" window. It remembers its size.
	/// </summary>
	public partial class AdvancedSearchWindow : FluentWindow
	{
		private readonly ISettingsService _settingsService;

		/// <summary>
		/// Initializes a new instance of the <see cref="AdvancedSearchWindow"/> class.
		/// </summary>
		/// <param name="viewModel">The view model.</param>
		/// <param name="settingsService">Stores the window size.</param>
		public AdvancedSearchWindow(AdvancedSearchViewModel viewModel, ISettingsService settingsService)
		{
			ArgumentNullException.ThrowIfNull(viewModel);
			ArgumentNullException.ThrowIfNull(settingsService);

			this.ViewModel = viewModel;
			this._settingsService = settingsService;
			this.DataContext = viewModel;
			this.InitializeComponent();
			ToolWindowHelper.ApplySize(this, settingsService.Settings.Window.GetPlacement(WindowKind.Search));
			this.Closing += (_, _) =>
			{
				WindowPlacement placement = ToolWindowHelper.Capture(this);
				this._settingsService.Update(settings => settings.Window.SetPlacement(WindowKind.Search, placement));
			};
			ScriptToolWindowHelper.CloseWhenRequested(this, viewModel);
			this.Activated += (_, _) => this.ViewModel.RefreshHistory();

			// Closing the window (Esc closes it) stops a search that is still running instead of letting it finish unseen.
			this.Closed += (_, _) =>
			{
				if (this.ViewModel.CancelRunningCommand.CanExecute(null))
				{
					this.ViewModel.CancelRunningCommand.Execute(null);
				}
			};
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
			this.GoToSelectedResult();
		}

		private void OnResultKeyDown(Object sender, KeyEventArgs e)
		{
			// Enter does what a double click does: goes to the match.
			if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
			{
				this.GoToSelectedResult();
				e.Handled = true;
			}
		}

		private void GoToSelectedResult()
		{
			if (this.ResultsGrid.SelectedItem is SearchResultItem item && this.ViewModel.GoToResultCommand.CanExecute(item))
			{
				this.ViewModel.GoToResultCommand.Execute(item);
			}
		}
	}
}