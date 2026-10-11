namespace FalloutScriptStudio.Views.ToolWindows
{
	using System;
	using System.Windows;
	using FalloutScriptStudio.ViewModels.ToolWindows;
	using Wpf.Ui.Controls;

	/// <summary>
	/// The "Go to line" prompt.
	/// </summary>
	public partial class GoToLineWindow : FluentWindow
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="GoToLineWindow"/> class.
		/// </summary>
		/// <param name="viewModel">The view model.</param>
		public GoToLineWindow(GoToLineViewModel viewModel)
		{
			this.InitializeComponent();
			ToolWindowHelper.Attach(this, viewModel);
		}

		private void OnLoaded(Object sender, RoutedEventArgs e)
		{
			this.LineBox.Focus();
			this.LineBox.SelectAll();
		}
	}
}
