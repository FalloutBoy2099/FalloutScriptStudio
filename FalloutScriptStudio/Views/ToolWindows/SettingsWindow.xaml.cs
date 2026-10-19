namespace FalloutScriptStudio.Views.ToolWindows
{
	using Wpf.Ui.Controls;
	using ViewModels.ToolWindows;

	/// <summary>
	/// The settings window: a list of pages on the left and the options of the selected page on the right.
	/// </summary>
	public partial class SettingsWindow : FluentWindow
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="SettingsWindow"/> class.
		/// </summary>
		/// <param name="viewModel">The view model.</param>
		public SettingsWindow(SettingsViewModel viewModel)
		{
			this.InitializeComponent();
			ToolWindowHelper.Attach(this, viewModel);
		}
	}
}