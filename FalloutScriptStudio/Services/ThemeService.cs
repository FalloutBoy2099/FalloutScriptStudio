namespace FalloutScriptStudio.Services
{
	using System;
	using Wpf.Ui.Appearance;
	using Editor.Highlighting;

	/// <inheritdoc />
	public sealed class ThemeService : IThemeService
	{
		public event EventHandler? ThemeChanged;

		public AppTheme Current { get; private set; } = AppTheme.System;

		public Boolean IsDark { get; private set; }

		public SyntaxTheme SyntaxTheme => SyntaxThemes.Get(this.IsDark);

		public void Apply(AppTheme theme)
		{
			this.Current = theme;
			ApplicationTheme effective = theme switch
			{
				AppTheme.Light => ApplicationTheme.Light,
				AppTheme.Dark => ApplicationTheme.Dark,
				_ => ApplicationThemeManager.GetSystemTheme() == SystemTheme.Light ? ApplicationTheme.Light : ApplicationTheme.Dark,
			};

			this.IsDark = effective == ApplicationTheme.Dark;
			ApplicationThemeManager.Apply(effective);
			this.ThemeChanged?.Invoke(this, EventArgs.Empty);
		}
	}
}