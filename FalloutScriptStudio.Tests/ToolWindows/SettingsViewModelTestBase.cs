namespace FalloutScriptStudio.Tests.ToolWindows
{
	using System;
	using FalloutScriptStudio.ViewModels.ToolWindows;

	public abstract class SettingsViewModelTestBase
	{
		internal readonly FakeSettingsService _settings = new FakeSettingsService(@"C:\fss");

		internal readonly FakeThemeService _theme = new FakeThemeService();

		internal readonly FakeDialogService _dialogs = new FakeDialogService();

		internal readonly FakeFileAssociationService _association = new FakeFileAssociationService();

		internal SettingsViewModel Create(params String[] fonts)
		{
			return new SettingsViewModel(this._settings, this._theme, this._dialogs, this._association, new FakeFontCatalog(fonts));
		}
	}
}