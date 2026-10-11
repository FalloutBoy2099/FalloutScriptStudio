namespace FalloutScriptStudio.Services
{
	using System;
	using FalloutScriptStudio.Editor.Highlighting;

	/// <summary>The application color mode.</summary>
	public enum AppTheme
	{
		/// <summary>Follow the Windows light/dark setting.</summary>
		System,

		Light,

		Dark,
	}

	/// <summary>
	/// Applies the Fluent light/dark theme to the whole application and exposes the matching editor color scheme.
	/// </summary>
	public interface IThemeService
	{
		/// <summary>Raised after a new theme has been applied.</summary>
		event EventHandler? ThemeChanged;

		/// <summary>Gets the requested theme (may be <see cref="AppTheme.System"/>).</summary>
		AppTheme Current { get; }

		/// <summary>Gets a value indicating whether the effective theme is dark.</summary>
		Boolean IsDark { get; }

		/// <summary>Gets the editor color scheme matching the effective theme.</summary>
		SyntaxTheme SyntaxTheme { get; }

		/// <summary>Applies a theme to the application resources.</summary>
		void Apply(AppTheme theme);
	}
}
