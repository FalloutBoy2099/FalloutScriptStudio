namespace FalloutScriptStudio.ViewModels.Main
{
	using FalloutScriptStudio.Workspace;

	/// <summary>The "Errors" tab: parser and build entries of the active script (<see cref="OutputPanelViewModel.Errors"/>).</summary>
	public sealed class ErrorsTabViewModel : OutputTabViewModel
	{
		/// <summary>Initializes the tab.</summary>
		/// <param name="output">The output panel.</param>
		public ErrorsTabViewModel(OutputPanelViewModel output)
			: base(output)
		{
		}

		/// <inheritdoc />
		public override OutputTab? Kind => OutputTab.Errors;
	}
}