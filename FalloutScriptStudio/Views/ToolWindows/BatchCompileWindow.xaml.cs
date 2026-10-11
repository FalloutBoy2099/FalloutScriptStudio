namespace FalloutScriptStudio.Views.ToolWindows
{
	using System;
	using System.ComponentModel;
	using System.Windows.Controls;
	using FalloutScriptStudio.ViewModels.ToolWindows;
	using Wpf.Ui.Controls;

	/// <summary>
	/// The batch compiler window: progress, failed scripts and their output.
	/// </summary>
	public partial class BatchCompileWindow : FluentWindow
	{
		private readonly BatchCompileViewModel _viewModel;

		/// <summary>
		/// Initializes a new instance of the <see cref="BatchCompileWindow"/> class.
		/// </summary>
		/// <param name="viewModel">The view model.</param>
		public BatchCompileWindow(BatchCompileViewModel viewModel)
		{
			this._viewModel = viewModel;
			this.InitializeComponent();
			ToolWindowHelper.Attach(this, viewModel);
		}

		private void OnLogChanged(Object sender, TextChangedEventArgs e)
		{
			this.LogBox.ScrollToEnd();
		}

		private void OnClosing(Object? sender, CancelEventArgs e)
		{
			// Closing the window while the run is in progress cancels it.
			this._viewModel.RequestCancel();
		}
	}
}
