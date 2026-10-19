namespace FalloutScriptStudio.Views.ToolWindows
{
	using System;
	using System.Windows.Input;
	using Wpf.Ui.Controls;
	using ViewModels.ToolWindows;

	/// <summary>"Format Project": the files that formatting would change, their differences, and the button that formats the checked ones.</summary>
	public partial class FormatProjectWindow : FluentWindow
	{
		private readonly FormatProjectViewModel _viewModel;

		/// <summary>Initializes a new instance of the <see cref="FormatProjectWindow"/> class.</summary>
		/// <param name="viewModel">The view model.</param>
		public FormatProjectWindow(FormatProjectViewModel viewModel)
		{
			ArgumentNullException.ThrowIfNull(viewModel);

			this._viewModel = viewModel;
			this.InitializeComponent();
			ToolWindowHelper.Attach(this, viewModel);
		}

		private void OnFileListKeyDown(Object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Space && this._viewModel.SelectedItem is { } item)
			{
				item.IsSelected = !item.IsSelected;
				e.Handled = true;
			}
		}
	}
}