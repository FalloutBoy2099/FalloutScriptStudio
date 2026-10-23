namespace FalloutScriptStudio.Core.Configuration
{
	using System;
	using System.Collections.Generic;

	/// <summary>
	/// Options of the script editor.
	/// </summary>
	public sealed class EditorSettings
	{
		/// <summary>The smallest allowed tab size.</summary>
		public const Int32 MinTabSize = 1;

		/// <summary>The largest allowed tab size.</summary>
		public const Int32 MaxTabSize = 30;

		/// <summary>The tab size used when the stored value is invalid.</summary>
		public const Int32 DefaultTabSize = 3;

		/// <summary>The smallest editor zoom in percent.</summary>
		public const Int32 MinZoomPercent = 40;

		/// <summary>The largest editor zoom in percent.</summary>
		public const Int32 MaxZoomPercent = 500;

		/// <summary>Gets or sets the color theme.</summary>
		public HighlightTheme Theme { get; set; } = HighlightTheme.Dark;

		/// <summary>Gets or sets the encoding of scripts and message files.</summary>
		public TextEncodingKind FileEncoding { get; set; } = TextEncodingKind.Ansi;

		/// <summary>Gets or sets a value indicating whether the Tab key inserts spaces.</summary>
		public Boolean TabsToSpaces { get; set; } = true;

		/// <summary>Gets or sets the width of a tab in characters.</summary>
		public Int32 TabSize { get; set; } = DefaultTabSize;

		/// <summary>Gets or sets a value indicating whether tab and space characters are drawn.</summary>
		public Boolean ShowTabCharacters { get; set; }

		/// <summary>Gets or sets a value indicating whether the vertical ruler is shown.</summary>
		public Boolean ShowVerticalRuler { get; set; } = true;

		/// <summary>Gets or sets a value indicating whether the script parser runs in the background.</summary>
		public Boolean EnableParser { get; set; } = true;

		/// <summary>Gets or sets a value indicating whether parser warnings are shown.</summary>
		public Boolean ParserWarnings { get; set; } = true;

		/// <summary>Gets or sets a value indicating whether code completion is enabled.</summary>
		public Boolean Autocomplete { get; set; } = true;

		/// <summary>Gets or sets a value indicating whether the completion list uses colors.</summary>
		public Boolean AutocompleteColor { get; set; } = true;

		/// <summary>Gets or sets a value indicating whether closing brackets and quotes are inserted automatically.</summary>
		public Boolean AutoInputPairedCharacters { get; set; } = true;

		/// <summary>Gets or sets a value indicating whether trailing spaces are removed when a file is saved.</summary>
		public Boolean AutoRemoveTrailingSpaces { get; set; } = true;

		/// <summary>Gets or sets a value indicating whether tooltips with function descriptions are shown.</summary>
		public Boolean ShowTips { get; set; } = true;

		/// <summary>Gets or sets a value indicating whether tooltips show the short form of descriptions.</summary>
		public Boolean ShortDescriptions { get; set; }

		/// <summary>Gets or sets a value indicating whether the last caret line of every script is remembered.</summary>
		public Boolean StoreLastPosition { get; set; } = true;

		/// <summary>
		/// Gets or sets a value indicating whether the documents that were open in a project when it was left are opened again when the
		/// project is opened (at once when nothing is open, otherwise a hint offers it).
		/// </summary>
		public Boolean RestoreProjectDocuments { get; set; } = true;

		/// <summary>
		/// Gets or sets a value indicating whether the local variables that are declared but never used get a faint hint mark in the editor
		/// (Code &gt; Find Unused Variables lists them whatever this option says).
		/// </summary>
		public Boolean ShowUnusedVariables { get; set; }

		/// <summary>
		/// Gets or sets how much of the semantic analysis of a script is shown while it is typed: the findings that sslc would only report
		/// when the script is compiled (a wrong number of arguments, an unknown name, a procedure used before its definition...). The
		/// analysis runs in the background after each parse, underlines the places in the editor and lists them in the problems.
		/// </summary>
		public LiveDiagnosticsLevel LiveDiagnostics { get; set; } = LiveDiagnosticsLevel.ErrorsAndWarnings;

		/// <summary>
		/// Gets or sets a value indicating whether the findings about the global variables of the project (written but never read, read but
		/// never written, mixed integer and float use of an sfall global, probable typos in names, names sfall cannot use) are listed in the
		/// problems by themselves after each index update. Off by default (they are information about the whole project, not about the
		/// script being edited); Code &gt; Check Global Variables lists them whatever this option says.
		/// </summary>
		public Boolean GlobalVariableFindings { get; set; }

		/// <summary>
		/// Gets or sets the global variables the findings leave out: a name (an sfall global, a <c>GVAR_*</c> macro), a number, or a prefix
		/// that ends with <c>*</c>. The case is ignored.
		/// </summary>
		public List<String> GlobalVariableIgnore { get; set; } = [];

		/// <summary>Gets or sets a value indicating whether scripts are saved as UTF-8.</summary>
		public Boolean SaveScriptsAsUtf8 { get; set; }

		/// <summary>
		/// Gets or sets the selected editor font: 0 is the default font, N is the Nth font found in the fonts folder (1-based).
		/// </summary>
		public Int32 FontIndex { get; set; } = 1;

		/// <summary>Gets or sets the size adjustment added to the base size of the font in points.</summary>
		public Int32 FontSizeAdjustment { get; set; }

		/// <summary>
		/// Gets or sets the editor zoom in percent (100 = the font size of the settings). It is changed with Ctrl+wheel or the
		/// zoom commands and restored when the program starts (the legacy editor kept its font size the same way).
		/// </summary>
		public Int32 ZoomPercent { get; set; } = 100;

		/// <summary>
		/// Gets or sets a value indicating whether a script (<c>.ssl</c>) or header (<c>.h</c>) is formatted when it is opened
		/// (inserting the missing spaces around operators); the "Auto Formatting Code" option of the legacy editor.
		/// </summary>
		public Boolean FormatCodeOnOpen { get; set; }

		/// <summary>
		/// Gets or sets the style of the code formatter (Format Document, Format Selection, Format Project, format on open and on save). A
		/// project may replace it (<see cref="ProjectSettings.Formatting"/>); read the style in effect from <see cref="EffectiveSettings.Formatting"/>.
		/// </summary>
		public FormatSettings Formatting { get; set; } = new FormatSettings();

		/// <summary>Gets or sets a value indicating whether searches ignore the letter case.</summary>
		public Boolean SearchIgnoreCase { get; set; }

		/// <summary>Gets or sets a value indicating whether searches match whole words only.</summary>
		public Boolean SearchWholeWord { get; set; }
	}
}