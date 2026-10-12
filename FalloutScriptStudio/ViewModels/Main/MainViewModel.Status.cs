namespace FalloutScriptStudio.ViewModels.Main
{
	using System;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Localization;

	/// <summary>The texts of the status bar and the refresh of the state that depends on the active editor.</summary>
	public sealed partial class MainViewModel
	{
		/// <summary>Gets the line of the caret as text for the status bar.</summary>
		public String LineText => Loc.Format("Str.Shell.Status.Line", this._tracker.Editor?.CaretLine ?? 1);

		/// <summary>Gets the column of the caret as text for the status bar.</summary>
		public String ColumnText => Loc.Format("Str.Shell.Status.Column", this._tracker.Editor?.CaretColumn ?? 1);

		/// <summary>Gets the zoom of the active editor as text for the status bar.</summary>
		public String ZoomText => (this._tracker.Editor?.ZoomPercent ?? 100) + "%";

		/// <summary>Gets the parser state of the active document for the status bar.</summary>
		public String ParserStatusText
		{
			get
			{
				DocumentViewModel? document = this._workspace.ActiveDocument;
				if (document is null)
				{
					return Loc.Get(this._settings.Settings.Editor.EnableParser ? "Str.Shell.Parser.NoFile" : "Str.Shell.Parser.OffMacros");
				}

				if (!document.IsParsable)
				{
					return Loc.Get(this._settings.Settings.Editor.EnableParser ? "Str.Shell.Parser.NotSsl" : "Str.Shell.Parser.OffMacros");
				}

				return document.ParserStatus.Kind != ParserStatusKind.None ? document.ParserStatus.ToText() : Loc.Get("Str.Shell.Parser.Idle");
			}
		}

		/// <summary>Gets the last short status message.</summary>
		public String StatusMessage => this.Output.StatusMessage;

		/// <summary>Gets the project folder text for the status bar.</summary>
		public String ProjectText => Loc.Format("Str.Shell.Status.Project", this._projects.IsOpen ? this._projects.DisplayName : Loc.Get("Str.Shell.Status.Unset"));

		/// <summary>Gets a value indicating whether a build command is running.</summary>
		public Boolean IsBuildRunning => this._build.IsBusy;

		private void RefreshEditorState()
		{
			this.OnPropertyChanged(nameof(this.HasDocument));
			this.OnPropertyChanged(nameof(this.IsScript));
			this.OnPropertyChanged(nameof(this.IsParsable));
			this.OnPropertyChanged(nameof(this.IsMessage));
			this.OnPropertyChanged(nameof(this.IsRightBrowserVisible));
			this.OnPropertyChanged(nameof(this.CanUndo));
			this.OnPropertyChanged(nameof(this.CanRedo));
			this.OnPropertyChanged(nameof(this.Title));
			this.OnPropertyChanged(nameof(this.ParserStatusText));
			this.OnPropertyChanged(nameof(this.ZoomText));
			this.RefreshCaretState();
		}

		private void RefreshCaretState()
		{
			this.OnPropertyChanged(nameof(this.LineText));
			this.OnPropertyChanged(nameof(this.ColumnText));
			this.OnPropertyChanged(nameof(this.CanGoBack));
			this.OnPropertyChanged(nameof(this.CanGoForward));
		}
	}
}
