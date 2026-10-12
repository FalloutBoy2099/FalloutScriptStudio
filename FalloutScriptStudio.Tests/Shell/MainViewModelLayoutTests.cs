namespace FalloutScriptStudio.Tests.Shell
{
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Tests.Shell.Support;
	using FalloutScriptStudio.Workspace;
	using Xunit;

	[Collection("Test collection for FalloutScriptStudio.Tests.Shell.MainViewModelToolsTests")]
	public sealed class MainViewModelLayoutTests
	{
		[Fact]
		public void The_theme_command_applies_and_stores_the_mode()
		{
			using ShellHarness harness = new ShellHarness();

			harness.Main.SetThemeCommand.Execute(AppTheme.Dark);

			Assert.Equal(AppTheme.Dark, harness.Theme.Current);
			Assert.Equal(HighlightTheme.Dark, harness.Settings.Settings.Editor.Theme);
			Assert.Equal(AppTheme.Dark, harness.Main.CurrentTheme);
		}

		[Fact]
		public void Panel_toggles_and_the_view_options_are_persisted_in_the_layout_file()
		{
			using ShellHarness harness = new ShellHarness();
			harness.Main.ShowFunctionTree = true;
			harness.Main.ShowRightBrowser = false;
			harness.Main.IsOutputMinimized = true;
			harness.Main.ShowLineNumbers = false;
			harness.Main.HighlightParseErrors = false;
			harness.Main.RightTabIndex = 2;

			Assert.False(harness.ViewOptions.ShowLineNumbers);
			Assert.False(harness.ViewOptions.HighlightParseErrors);
			ShellLayout layout = new ShellLayoutStore(harness.Paths).Load();
			Assert.True(layout.ShowFunctionTree);
			Assert.False(layout.ShowRightBrowser);
			Assert.True(layout.LogMinimized);
			Assert.False(layout.ShowLineNumbers);
			Assert.False(layout.HighlightParseErrors);
			Assert.Equal(2, layout.RightTabIndex);
		}

		[Fact]
		public void The_log_window_toggle_is_stored_in_the_window_settings()
		{
			using ShellHarness harness = new ShellHarness();

			harness.Main.ShowLog = false;

			Assert.False(harness.Settings.Settings.Window.ShowLog);
			harness.Main.ShowLog = true;
			Assert.True(harness.Settings.Settings.Window.ShowLog);
		}

		[Fact]
		public void A_request_to_show_the_output_makes_the_panel_visible_again()
		{
			using ShellHarness harness = new ShellHarness();
			harness.Main.ShowLog = false;
			harness.Main.IsOutputMinimized = true;

			harness.Output.ShowTab(FalloutScriptStudio.Workspace.OutputTab.Errors);

			Assert.True(harness.Main.ShowLog);
			Assert.False(harness.Main.IsOutputMinimized);
		}
	}
}
