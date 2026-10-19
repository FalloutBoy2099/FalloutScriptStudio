namespace FalloutScriptStudio.ViewModels.ToolWindows
{
	using System;

	// Code editor page: the style of the code formatter, with its preview.
	public sealed partial class SettingsViewModel
	{
		private FormattingEditorViewModel? _formattingEditor;

		/// <summary>Gets the editor of the style of the code formatter (it edits the style of the draft in place).</summary>
		public FormattingEditorViewModel FormattingEditor => this._formattingEditor ??= new FormattingEditorViewModel(this.Draft.Editor.Formatting, () => this.Draft.Editor);
	}
}