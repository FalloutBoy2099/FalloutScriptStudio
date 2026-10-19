namespace FalloutScriptStudio.Views.ScriptTools
{
	using System;
	using System.Windows.Input;
	using Wpf.Ui.Controls;
	using ViewModels.ScriptTools;

	/// <summary>The preview of "Replace in Files": the files with their replacements, to tick before anything is changed.</summary>
	public partial class ReplacePreviewWindow : FluentWindow
	{
		/// <summary>Initializes a new instance of the <see cref="ReplacePreviewWindow"/> class.</summary>
		/// <param name="viewModel">The view model.</param>
		public ReplacePreviewWindow(ReplacePreviewViewModel viewModel)
		{
			ArgumentNullException.ThrowIfNull(viewModel);

			this.ViewModel = viewModel;
			this.DataContext = viewModel;
			this.InitializeComponent();
			ScriptToolWindowHelper.CloseWhenRequested(this, viewModel);
		}

		/// <summary>Gets the view model.</summary>
		public ReplacePreviewViewModel ViewModel { get; }

		private void OnTreeKeyDown(Object sender, KeyEventArgs e)
		{
			// Space ticks or unticks the selected file or replacement (the check boxes themselves are not in the tab order).
			if (e.Key == Key.Space && this.MatchTree.SelectedItem is ReplaceNodeViewModel node)
			{
				node.IsChecked = node.IsChecked != true;
				e.Handled = true;
			}
		}
	}
}