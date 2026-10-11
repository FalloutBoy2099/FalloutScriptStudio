namespace FalloutScriptStudio.Views.Editor
{
	using System.Windows.Controls;
	using FalloutScriptStudio.ViewModels.Editor;

	/// <summary>The content of an editor tooltip: colored title lines with their descriptions.</summary>
	public partial class EditorTipView : UserControl
	{
		public EditorTipView(EditorTipViewModel viewModel)
		{
			this.InitializeComponent();
			this.DataContext = viewModel;
		}
	}
}
