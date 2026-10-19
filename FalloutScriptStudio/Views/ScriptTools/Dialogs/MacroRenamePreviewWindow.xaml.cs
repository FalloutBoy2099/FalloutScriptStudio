namespace FalloutScriptStudio.Views.ScriptTools.Dialogs
{
	using System;
	using System.Windows.Input;
	using Wpf.Ui.Controls;
	using ViewModels.ScriptTools.Dialogs;

	/// <summary>
	/// The macro rename preview window.
	/// </summary>
	public partial class MacroRenamePreviewWindow : FluentWindow
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="MacroRenamePreviewWindow"/> class.
		/// </summary>
		/// <param name="viewModel">The view model.</param>
		public MacroRenamePreviewWindow(MacroRenamePreviewViewModel viewModel)
		{
			ArgumentNullException.ThrowIfNull(viewModel);

			this.ViewModel = viewModel;
			this.InitializeComponent();
			DialogueWindowHelper.Attach(this, viewModel);
		}

		/// <summary>Gets the view model.</summary>
		public MacroRenamePreviewViewModel ViewModel { get; }

		private void OnTreeKeyDown(Object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Space && this.MatchTree.SelectedItem is MacroRenameNodeViewModel node)
			{
				node.IsChecked = node.IsChecked != true;
				e.Handled = true;
			}
		}
	}
}