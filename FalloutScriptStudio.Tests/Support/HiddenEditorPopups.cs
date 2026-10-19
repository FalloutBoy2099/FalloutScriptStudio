namespace FalloutScriptStudio.Tests.Support
{
	using System.Collections.Generic;
	using System.Windows.Controls;
	using System.Windows.Controls.Primitives;
	using FalloutScriptStudio.Editor;
	using ICSharpCode.AvalonEdit.CodeCompletion;

	/// <summary>
	/// The popup presenter of the tests: the completion lists, the tooltips and the code tips are built as in the application but never
	/// opened, because each of them is a real top-level window that the system keeps on the screen (and that would flash while the tests run).
	/// It records what the editor wanted to show, so that the tests can still check it.
	/// </summary>
	public sealed class HiddenEditorPopups : IEditorPopupPresenter
	{
		/// <summary>Gets the completion lists the editor asked to open.</summary>
		public List<CompletionWindow> CompletionWindows { get; } = [];

		/// <summary>Gets the tooltips the editor asked to open.</summary>
		public List<ToolTip> ToolTips { get; } = [];

		/// <summary>Gets the popups the editor asked to open.</summary>
		public List<Popup> Popups { get; } = [];

		/// <inheritdoc/>
		public void Show(CompletionWindow window)
		{
			this.CompletionWindows.Add(window);
		}

		/// <inheritdoc/>
		public void Show(ToolTip tip)
		{
			this.ToolTips.Add(tip);
		}

		/// <inheritdoc/>
		public void Show(Popup popup)
		{
			this.Popups.Add(popup);
		}
	}
}