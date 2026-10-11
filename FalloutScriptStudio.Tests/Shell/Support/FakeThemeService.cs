namespace FalloutScriptStudio.Tests.Shell.Support
{
	using System;
	using FalloutScriptStudio.Editor.Highlighting;
	using FalloutScriptStudio.Services;

	/// <summary>A theme service that only remembers the requested mode.</summary>
	internal sealed class FakeThemeService : IThemeService
	{
		public event EventHandler? ThemeChanged;

		public AppTheme Current { get; private set; } = AppTheme.System;

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
