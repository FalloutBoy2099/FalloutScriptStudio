namespace FalloutScriptStudio.ViewModels.ToolWindows
{
	using System;
	using CommunityToolkit.Mvvm.ComponentModel;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Workspace.Formatting;

	/// <summary>One file of the preview of "Format Project": a check box to format it or not, its name and the number of lines that would change.</summary>
	public sealed partial class FormatItemViewModel : ObservableObject
	{
		[ObservableProperty]
		private Boolean _isSelected = true;

		/// <summary>Initializes a new instance of the <see cref="FormatItemViewModel"/> class.</summary>
		/// <param name="change">The file.</param>
		public FormatItemViewModel(FormatFileChange change)
		{
			ArgumentNullException.ThrowIfNull(change);
			this.Change = change;
		}

		/// <summary>Gets the file.</summary>
		public FormatFileChange Change { get; }

		/// <summary>Gets the name shown for the file.</summary>
		public String DisplayName => this.Change.DisplayName;

		/// <summary>Gets the number of lines that would change.</summary>
		public Int32 ChangedLines => this.Change.ChangedLines;

		/// <summary>Gets the number of changed lines as a text.</summary>
		public String ChangedLinesText => Loc.Format("Str.ToolWindows.Format.Lines", this.Change.ChangedLines);

		/// <summary>Gets the name a screen reader reads for the row: the file, whether it is open, and the number of changed lines.</summary>
		public String AccessibleName => this.Change.IsOpen
			? Loc.Format("Str.ToolWindows.Format.RowOpen", this.Change.DisplayName, this.Change.ChangedLines)
			: Loc.Format("Str.ToolWindows.Format.Row", this.Change.DisplayName, this.Change.ChangedLines);

		/// <summary>Gets a value indicating whether the file is open in the editor.</summary>
		public Boolean IsOpen => this.Change.IsOpen;

		/// <summary>Tells the bindings that the texts changed with the language.</summary>
		public void RefreshTexts()
		{
			this.OnPropertyChanged(nameof(this.ChangedLinesText));
			this.OnPropertyChanged(nameof(this.AccessibleName));
		}
	}
}