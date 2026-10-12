namespace FalloutScriptStudio.Editor
{
	using System.Windows.Controls;
	using System.Windows.Controls.Primitives;
	using ICSharpCode.AvalonEdit.CodeCompletion;

	/// <summary>
	/// Opens the top-level windows of the editor (the completion list, the hover tooltip and the code tip popup). The controllers build and
	/// manage them, the presenter only makes them appear: the tests replace it so that nothing is ever shown on the screen.
	/// </summary>
	public interface IEditorPopupPresenter
	{
		/// <summary>Opens a completion list.</summary>
		/// <param name="window">The completion window.</param>
		void Show(CompletionWindow window);

		/// <summary>Opens a tooltip.</summary>
		/// <param name="tip">The tooltip.</param>
		void Show(ToolTip tip);

		/// <summary>Opens a popup.</summary>
		/// <param name="popup">The popup.</param>
		void Show(Popup popup);
	}
}
