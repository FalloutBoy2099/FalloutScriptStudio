namespace FalloutScriptStudio.Views.ScriptTools
{
	using System;
	using System.ComponentModel;
	using System.Threading.Tasks;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Input;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.ViewModels.ScriptTools;
	using Wpf.Ui.Controls;

	/// <summary>
	/// The "Scripts List Editor" window.
	/// </summary>
	public partial class RegisterScriptWindow : FluentWindow
	{
		private Boolean _closeConfirmed;

		/// <summary>
		/// Initializes a new instance of the <see cref="RegisterScriptWindow"/> class.
		/// </summary>
		/// <param name="viewModel">The view model.</param>
		public RegisterScriptWindow(RegisterScriptViewModel viewModel)
		{
			ArgumentNullException.ThrowIfNull(viewModel);

			this.ViewModel = viewModel;
			this.DataContext = viewModel;
			this.InitializeComponent();
			viewModel.PropertyChanged += this.OnViewModelPropertyChanged;
			this.Closing += this.OnWindowClosing;
			_ = LanguageChangedSubscription.SubscribeUntilClosed(this, static window => window.ViewModel.RefreshTexts());
		}

		/// <summary>Gets the view model.</summary>
		public RegisterScriptViewModel ViewModel { get; }

		private void OnViewModelPropertyChanged(Object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(RegisterScriptViewModel.SelectedEntry) && this.ViewModel.SelectedEntry is not null)
			{
				this.ScriptsGrid.ScrollIntoView(this.ViewModel.SelectedEntry);
			}
		}

		private void OnCurrentCellChanged(Object? sender, EventArgs e)
		{
			Int32 column = this.ScriptsGrid.CurrentCell.Column is null ? 0 : this.ScriptsGrid.Columns.IndexOf(this.ScriptsGrid.CurrentCell.Column);
			this.ViewModel.FindColumn = Math.Max(column, 0);
		}

		private void CommitEdit()
		{
			this.ScriptsGrid.CommitEdit(DataGridEditingUnit.Row, true);
		}

		private void OnSaveClick(Object sender, RoutedEventArgs e)
		{
			this.CommitEdit();
		}

		private void OnWindowKeyDown(Object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
			{
				e.Handled = true;
				this.Close();
			}
		}

		private void OnWindowClosing(Object? sender, CancelEventArgs e)
		{
			this.CommitEdit();
			if (this._closeConfirmed || !this.ViewModel.IsModified)
			{
				return;
			}

			e.Cancel = true;
			_ = this.ConfirmAndCloseAsync();
		}

		private async Task ConfirmAndCloseAsync()
		{
			if (await this.ViewModel.ConfirmCloseAsync())
			{
				this._closeConfirmed = true;
				this.Close();
			}
		}
	}
}
