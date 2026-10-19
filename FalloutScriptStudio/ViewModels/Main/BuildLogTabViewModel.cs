namespace FalloutScriptStudio.ViewModels.Main
{
	using Workspace;

	/// <summary>The "Build" tab: the raw compiler log (<see cref="OutputPanelViewModel.BuildText"/>).</summary>
	public sealed class BuildLogTabViewModel : OutputTabViewModel
	{
		/// <summary>Initializes the tab.</summary>
		/// <param name="output">The output panel.</param>
		public BuildLogTabViewModel(OutputPanelViewModel output)
			: base(output)
		{
		}

		/// <inheritdoc />
		public override OutputTab? Kind => OutputTab.Build;
	}
}