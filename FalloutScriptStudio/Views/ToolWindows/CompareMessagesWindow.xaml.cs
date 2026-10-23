namespace FalloutScriptStudio.Views.ToolWindows
{
	using System;
	using System.Windows.Controls;
	using System.Windows.Input;
	using Wpf.Ui.Controls;
	using ViewModels.ToolWindows;

	/// <summary>"Compare Message Files": the messages of a reference file and of the file of another language side by side, with the status of each line.</summary>
	public partial class CompareMessagesWindow : FluentWindow
	{
		private readonly CompareMessagesViewModel _viewModel;

		/// <summary>Initializes a new instance of the <see cref="CompareMessagesWindow"/> class.</summary>
		/// <param name="viewModel">The view model.</param>
		public CompareMessagesWindow(CompareMessagesViewModel viewModel)
		{
			ArgumentNullException.ThrowIfNull(viewModel);

			this._viewModel = viewModel;
			this.InitializeComponent();
			ToolWindowHelper.Attach(this, viewModel);
		}

		/// <summary>Gets the view model.</summary>
		public CompareMessagesViewModel ViewModel => this._viewModel;

		private void OnTableDoubleClick(Object sender, MouseButtonEventArgs e)
		{
			if (e.OriginalSource is System.Windows.DependencyObject source && FindRow(source) is not null)
			{
				this._viewModel.OpenRowCommand.Execute(null);
			}
		}

		private void OnTableKeyDown(Object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Enter && this._viewModel.OpenRowCommand.CanExecute(null))
			{
				this._viewModel.OpenRowCommand.Execute(null);
				e.Handled = true;
			}
		}

		private static DataGridRow? FindRow(System.Windows.DependencyObject? element)
		{
			while (element is not null && element is not DataGridRow)
			{
				element = System.Windows.Media.VisualTreeHelper.GetParent(element);
			}

			return element as DataGridRow;
		}
	}
}