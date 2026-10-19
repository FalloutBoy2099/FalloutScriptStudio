namespace FalloutScriptStudio.Views.ToolWindows
{
	using Wpf.Ui.Controls;
	using ViewModels.ToolWindows;

	/// <summary>
	/// The About window.
	/// </summary>
	public partial class AboutWindow : FluentWindow
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="AboutWindow"/> class.
		/// </summary>
		/// <param name="viewModel">The view model.</param>
		public AboutWindow(AboutViewModel viewModel)
		{
			this.InitializeComponent();
			ToolWindowHelper.Attach(this, viewModel);
		}
	}
}