namespace FalloutScriptStudio.Views.Shell
{
	using System;
	using System.ComponentModel;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Data;
	using System.Windows.Input;
	using FalloutScriptStudio.ViewModels.Main;
	using Wpf.Ui.Controls;

	/// <summary>The output panel with the Parser, Build and Errors tabs.</summary>
	public partial class OutputPanelView : UserControl
	{
		public OutputPanelView()
		{
			this.InitializeComponent();
			this.DataContextChanged += this.OnDataContextChanged;
		}

		private MainViewModel? ViewModel => this.DataContext as MainViewModel;

		private void OnDataContextChanged(Object sender, DependencyPropertyChangedEventArgs e)
		{
			if (e.OldValue is MainViewModel old)
			{
				old.PropertyChanged -= this.OnViewModelPropertyChanged;
			}

			if (e.NewValue is MainViewModel viewModel)
			{
				ICollectionView view = CollectionViewSource.GetDefaultView(viewModel.Output.Errors);
				view.GroupDescriptions.Clear();
				view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ErrorItemViewModel.Group)));
				viewModel.PropertyChanged += this.OnViewModelPropertyChanged;
				this.UpdateMinimizeIcon();
			}
		}

		private void OnViewModelPropertyChanged(Object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(MainViewModel.IsOutputMinimized))
			{
				this.UpdateMinimizeIcon();
			}
		}

		private void UpdateMinimizeIcon()
		{
			this.MinimizeButton.Icon = new SymbolIcon(this.ViewModel?.IsOutputMinimized == true ? SymbolRegular.ChevronUp24 : SymbolRegular.ChevronDown24);
		}

		private void OnOutputTabsSelectionChanged(Object sender, SelectionChangedEventArgs e)
		{
			// Choosing a tab of a minimized panel restores it.
			if (e.OriginalSource == this.OutputTabs && this.ViewModel is { IsOutputMinimized: true } viewModel && this.OutputTabs.IsMouseOver)
			{
				viewModel.IsOutputMinimized = false;
			}
		}

		private void OnErrorsDoubleClick(Object sender, MouseButtonEventArgs e)
		{
			if (this.ViewModel is { } viewModel && sender is System.Windows.Controls.DataGrid { SelectedItem: ErrorItemViewModel selected })
			{
				viewModel.Output.GoToErrorCommand.Execute(selected);
			}
		}
	}
}
