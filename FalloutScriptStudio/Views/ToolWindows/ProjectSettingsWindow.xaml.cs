namespace FalloutScriptStudio.Views.ToolWindows
{
	using FalloutScriptStudio.ViewModels.ToolWindows;
	using Wpf.Ui.Controls;

	/// <summary>
	/// The project settings window: for each option a project can override, the choice between the global setting and a value of the project.
	/// </summary>
	public partial class ProjectSettingsWindow : FluentWindow
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="ProjectSettingsWindow"/> class.
		/// </summary>
		/// <param name="viewModel">The view model.</param>
		public ProjectSettingsWindow(ProjectSettingsViewModel viewModel)
		{
			this.InitializeComponent();
			ToolWindowHelper.Attach(this, viewModel);
		}
	}
}
