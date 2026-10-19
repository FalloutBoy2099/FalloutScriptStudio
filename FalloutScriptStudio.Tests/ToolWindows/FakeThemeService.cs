namespace FalloutScriptStudio.Tests.ToolWindows
{
	using System;
	using FalloutScriptStudio.Editor.Highlighting;
	using FalloutScriptStudio.Services;

	internal sealed class FakeThemeService : IThemeService
	{
		public event EventHandler? ThemeChanged;

		public AppTheme Current { get; private set; } = AppTheme.Dark;

		public Boolean IsDark { get; private set; } = true;

		public SyntaxTheme SyntaxTheme => SyntaxThemes.Get(this.IsDark);

		public Boolean SystemIsDark { get; set; } = true;

		public Int32 ApplyCount { get; private set; }

		public void Apply(AppTheme theme)
		{
			this.Current = theme;
			this.IsDark = theme == AppTheme.System ? this.SystemIsDark : theme == AppTheme.Dark;
			this.ApplyCount++;
			this.ThemeChanged?.Invoke(this, EventArgs.Empty);
		}
	}
}