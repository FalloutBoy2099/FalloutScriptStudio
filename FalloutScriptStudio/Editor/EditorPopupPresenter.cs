namespace FalloutScriptStudio.Editor
{
	using System.Windows.Controls;
	using System.Windows.Controls.Primitives;
	using ICSharpCode.AvalonEdit.CodeCompletion;

	/// <summary>The presenter of the application: the windows really open.</summary>
	public sealed class EditorPopupPresenter : IEditorPopupPresenter
	{
		/// <inheritdoc/>
		public void Show(CompletionWindow window)
		{
			window.Show();
		}

		/// <inheritdoc/>
		public void Show(ToolTip tip)
		{
			tip.IsOpen = true;
		}

		/// <inheritdoc/>
		public void Show(Popup popup)
		{
			popup.IsOpen = true;
		}
	}
}
