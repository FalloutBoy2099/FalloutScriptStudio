namespace FalloutScriptStudio.Views.ToolWindows
{
	using System.Windows.Controls;

	/// <summary>
	/// The editor of a list of compile profiles (see <c>CompileProfileEditorViewModel</c>): the list with its Add and Delete buttons, and
	/// for the selected profile its name and, for each option, the choice "inherit / override" and the value.
	/// </summary>
	public partial class CompileProfilesEditor : UserControl
	{
		/// <summary>Initializes a new instance of the <see cref="CompileProfilesEditor"/> class.</summary>
		public CompileProfilesEditor()
		{
			this.InitializeComponent();
		}
	}
}