namespace FalloutScriptStudio.Tests.Editor.Support
{
	using System;
	using FalloutScriptStudio.Editor.Highlighting;
	using FalloutScriptStudio.Services;

	/// <summary>An <see cref="IThemeService"/> that does not touch the application resources.</summary>
	public sealed class FixedThemeService : IThemeService
	{
		public FixedThemeService(Boolean dark)
		{
			this.IsDark = dark;
			this.Current = dark ? AppTheme.Dark : AppTheme.Light;
		}

		public event EventHandler? ThemeChanged;

		public AppTheme Current { get; private set; }

		public Boolean IsDark { get; private set; }

		public SyntaxTheme SyntaxTheme => SyntaxThemes.Get(this.IsDark);

		public void Apply(AppTheme theme)
		{
			this.Current = theme;
			this.IsDark = theme == AppTheme.Dark;
			this.ThemeChanged?.Invoke(this, EventArgs.Empty);
		}
	}
}