namespace FalloutScriptStudio.ViewModels.Main
{
	using FalloutScriptStudio.Workspace;

	/// <summary>The "Parser" tab: the raw parser log of the active script (<see cref="OutputPanelViewModel.ParserText"/>).</summary>
	public sealed class ParserLogTabViewModel : OutputTabViewModel
	{
		/// <summary>Initializes the tab.</summary>
		/// <param name="output">The output panel.</param>
		public ParserLogTabViewModel(OutputPanelViewModel output)
			: base(output)
		{
		}

		/// <inheritdoc />
		public override OutputTab? Kind => OutputTab.Parser;
	}
}
