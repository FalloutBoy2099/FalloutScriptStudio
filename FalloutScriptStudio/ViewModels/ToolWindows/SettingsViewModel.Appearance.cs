namespace FalloutScriptStudio.ViewModels.ToolWindows
{
	using System;
	using System.Collections.Generic;
	using CommunityToolkit.Mvvm.ComponentModel;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Services.ToolWindows;

	// Appearance page: color mode and editor font.
	public sealed partial class SettingsViewModel
	{
		[ObservableProperty]
		private AppTheme _selectedTheme;

		/// <summary>Gets the color modes to choose from.</summary>
		public IReadOnlyList<Choice<AppTheme>> ThemeChoices { get; } = new List<Choice<AppTheme>>
		{
			new Choice<AppTheme>(AppTheme.System, Loc.Get("Str.Common.ThemeSystem")),
			new Choice<AppTheme>(AppTheme.Light, Loc.Get("Str.Common.ThemeLight")),
			new Choice<AppTheme>(AppTheme.Dark, Loc.Get("Str.Common.ThemeDark")),
		};

		/// <summary>Gets the editor fonts to choose from; the value is the font index stored in the settings (0 is the default font).</summary>
		public IReadOnlyList<Choice<Int32>> FontChoices { get; }

		private static List<Choice<Int32>> BuildFontChoices(IEditorFontCatalog fontCatalog, String fontsDirectory)
		{
			List<Choice<Int32>> fonts = new List<Choice<Int32>> { new Choice<Int32>(0, KnownFonts.DefaultFamily) };
			IReadOnlyList<String> bundled = fontCatalog.GetFamilyNames(fontsDirectory);
			for (Int32 i = 0; i < bundled.Count; i++)
			{
				fonts.Add(new Choice<Int32>(i + 1, bundled[i]));
			}

			return fonts;
		}
	}
}
