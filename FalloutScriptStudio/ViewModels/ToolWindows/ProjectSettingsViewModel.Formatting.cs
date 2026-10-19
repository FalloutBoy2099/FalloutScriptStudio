namespace FalloutScriptStudio.ViewModels.ToolWindows
{
	using System;
	using System.ComponentModel;
	using FalloutScriptStudio.Core.Configuration;

	// The style of the code formatter: inherited from the global settings, or the project's own.
	public sealed partial class ProjectSettingsViewModel
	{
		private readonly FormatSettings _formatting = new();
		private OverrideMode? _formattingMode;
		private FormattingEditorViewModel? _formattingEditor;

		/// <summary>Gets the choice between the global style and the style of the project.</summary>
		public OverrideMode FormattingMode => this._formattingMode ??= this.CreateFormattingMode();

		/// <summary>Gets the editor of the style of the project (it shows the global style while the project inherits it).</summary>
		public FormattingEditorViewModel FormattingEditor => this._formattingEditor ??= new FormattingEditorViewModel(this._formatting, () => this._settings.Settings.Editor);

		private OverrideMode CreateFormattingMode()
		{
			OverrideMode mode = new(false);
			mode.PropertyChanged += this.OnFormattingModeChanged;
			return mode;
		}

		private void LoadFormatting(AppSettings global, ProjectSettings project)
		{
			this.FormattingMode.IsOverridden = false;
			this.FormattingEditor.Assign(project.Formatting ?? global.Editor.Formatting);
			this.FormattingMode.IsOverridden = project.Formatting is not null;
		}

		private FormatSettings? SaveFormatting()
		{
			return this.FormattingMode.IsOverridden ? this._formatting.Clone() : null;
		}

		private void OnFormattingModeChanged(Object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(OverrideMode.IsOverridden) && !this.FormattingMode.IsOverridden)
			{
				// A project that goes back to the global style shows it again.
				this.FormattingEditor.Assign(this._settings.Settings.Editor.Formatting);
			}
		}
	}
}