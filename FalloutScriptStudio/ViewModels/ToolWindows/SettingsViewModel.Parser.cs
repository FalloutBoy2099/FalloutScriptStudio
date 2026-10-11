namespace FalloutScriptStudio.ViewModels.ToolWindows
{
	using System;

	// Parser page.
	public sealed partial class SettingsViewModel
	{
		/// <summary>Gets or sets a value indicating whether the script parser runs.</summary>
		public Boolean EnableParser
		{
			get => this.Draft.Editor.EnableParser;
			set
			{
				if (this.Draft.Editor.EnableParser != value)
				{
					this.Draft.Editor.EnableParser = value;
					this.OnPropertyChanged();
					this.OnPropertyChanged(nameof(this.IsParserWarningsEnabled));
				}
			}
		}

		/// <summary>Gets a value indicating whether the parser warnings option can be changed (it needs the parser).</summary>
		public Boolean IsParserWarningsEnabled => this.Draft.Editor.EnableParser;
	}
}
