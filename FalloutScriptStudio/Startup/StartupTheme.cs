namespace FalloutScriptStudio.Startup
{
	using System;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Services;

	/// <summary>Chooses the color mode the application starts with.</summary>
	public static class StartupTheme
	{
		/// <summary>The stored mode; the legacy light / dark choice after an import of the old settings; otherwise the Windows mode.</summary>
		/// <param name="layoutExists">Whether the shell layout file exists.</param>
		/// <param name="storedTheme">The mode stored in the shell layout (only used when <paramref name="layoutExists"/>).</param>
		/// <param name="importedLegacyData">Whether the settings were imported from the old editor.</param>
		/// <param name="legacyHighlightTheme">The editor color scheme of the imported settings.</param>
		/// <returns>The mode to apply.</returns>
		public static AppTheme Resolve(Boolean layoutExists, AppTheme storedTheme, Boolean importedLegacyData, HighlightTheme legacyHighlightTheme)
		{
			if (layoutExists)
			{
				return storedTheme;
			}

			if (importedLegacyData)
			{
				return legacyHighlightTheme == HighlightTheme.Dark ? AppTheme.Dark : AppTheme.Light;
			}

			return AppTheme.System;
		}
	}
}
