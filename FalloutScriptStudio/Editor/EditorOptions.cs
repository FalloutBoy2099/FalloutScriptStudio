namespace FalloutScriptStudio.Editor
{
	using System;
	using FalloutScriptStudio.Core.Configuration;

	/// <summary>An immutable snapshot of the editor options that influence the text editor control.</summary>
	public sealed record EditorOptions
	{
		/// <summary>Gets a value indicating whether the Tab key inserts spaces.</summary>
		public Boolean TabsToSpaces { get; init; } = true;

		/// <summary>Gets the width of a tab (and of one indentation level) in characters.</summary>
		public Int32 TabSize { get; init; } = EditorSettings.DefaultTabSize;

		/// <summary>Gets a value indicating whether tab and space characters are drawn.</summary>
		public Boolean ShowTabCharacters { get; init; }

		/// <summary>Gets a value indicating whether the vertical ruler (indent line) is shown.</summary>
		public Boolean ShowVerticalRuler { get; init; } = true;

		/// <summary>Gets a value indicating whether the completion list is enabled.</summary>
		public Boolean Autocomplete { get; init; } = true;

		/// <summary>Gets a value indicating whether the completion list uses colors.</summary>
		public Boolean AutocompleteColor { get; init; } = true;

		/// <summary>Gets a value indicating whether closing brackets and quotes are inserted automatically.</summary>
		public Boolean AutoInputPairedCharacters { get; init; } = true;

		/// <summary>Gets a value indicating whether tooltips and code tips are shown.</summary>
		public Boolean ShowTips { get; init; } = true;

		/// <summary>Gets a value indicating whether tooltips show the short form of descriptions.</summary>
		public Boolean ShortDescriptions { get; init; }

		/// <summary>Gets the selected font: 0 is the default font, N the Nth font of the fonts folder.</summary>
		public Int32 FontIndex { get; init; } = 1;

		/// <summary>Gets the font size adjustment in points.</summary>
		public Int32 FontSizeAdjustment { get; init; }

		/// <summary>Gets a value indicating whether the editor shows line numbers.</summary>
		public Boolean ShowLineNumbers { get; init; } = true;

		/// <summary>Gets a value indicating whether parser errors are underlined.</summary>
		public Boolean HighlightParseErrors { get; init; } = true;

		/// <summary>Gets a value indicating whether local variables that are never used get a faint hint mark.</summary>
		public Boolean ShowUnusedVariables { get; init; }

		/// <summary>Creates the snapshot from the persisted settings.</summary>
		/// <param name="settings">The editor settings.</param>
		/// <param name="showLineNumbers">Whether line numbers are shown (a view option that is not persisted in the settings).</param>
		/// <param name="highlightParseErrors">Whether parser errors are underlined (a view option that is not persisted in the settings).</param>
		/// <returns>The options.</returns>
		public static EditorOptions From(EditorSettings settings, Boolean showLineNumbers = true, Boolean highlightParseErrors = true)
		{
			return new EditorOptions
			{
				TabsToSpaces = settings.TabsToSpaces,
				TabSize = Math.Clamp(settings.TabSize, EditorSettings.MinTabSize, EditorSettings.MaxTabSize),
				ShowTabCharacters = settings.ShowTabCharacters,
				ShowVerticalRuler = settings.ShowVerticalRuler,
				Autocomplete = settings.Autocomplete,
				AutocompleteColor = settings.AutocompleteColor,
				AutoInputPairedCharacters = settings.AutoInputPairedCharacters,
				ShowTips = settings.ShowTips,
				ShortDescriptions = settings.ShortDescriptions,
				FontIndex = settings.FontIndex,
				FontSizeAdjustment = settings.FontSizeAdjustment,
				ShowLineNumbers = showLineNumbers,
				HighlightParseErrors = highlightParseErrors,
				ShowUnusedVariables = settings.ShowUnusedVariables,
			};
		}
	}
}
