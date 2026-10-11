namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.IO;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Tests.Shell.Support;
	using FalloutScriptStudio.Workspace;
	using Xunit;

	public sealed class ShellLayoutStoreTests
	{
		[Fact]
		public void The_defaults_are_used_when_there_is_no_file()
		{
			using TempFolder folder = new TempFolder();
			ShellLayoutStore store = new ShellLayoutStore(AppPaths.FromBaseDirectory(folder.Path));

			ShellLayout layout = store.Load();

			Assert.True(layout.ShowRightBrowser);
			Assert.False(layout.ShowFunctionTree);
			Assert.True(layout.ShowLineNumbers);
			Assert.Equal(AppTheme.System, layout.Theme);
		}

		[Fact]
		public void A_saved_layout_is_read_back()
		{
			using TempFolder folder = new TempFolder();
			AppPaths paths = AppPaths.FromBaseDirectory(folder.Path);
			ShellLayoutStore store = new ShellLayoutStore(paths);

			store.Save(new ShellLayout { ShowFunctionTree = true, FunctionTreeWidth = 301, Theme = AppTheme.Dark, RightTabIndex = 3, LogMinimized = true });
			ShellLayout loaded = new ShellLayoutStore(paths).Load();

			Assert.True(loaded.ShowFunctionTree);
			Assert.Equal(301, loaded.FunctionTreeWidth);
			Assert.Equal(AppTheme.Dark, loaded.Theme);
			Assert.Equal(3, loaded.RightTabIndex);
			Assert.True(loaded.LogMinimized);
			Assert.Contains("\"Dark\"", File.ReadAllText(Path.Combine(paths.SettingsDirectory, "shell.json")), StringComparison.Ordinal);
		}

		[Fact]
		public void A_damaged_file_gives_the_defaults()
		{
			using TempFolder folder = new TempFolder();
			AppPaths paths = AppPaths.FromBaseDirectory(folder.Path);
			Directory.CreateDirectory(paths.SettingsDirectory);
			File.WriteAllText(Path.Combine(paths.SettingsDirectory, "shell.json"), "{ not json");

			ShellLayout layout = new ShellLayoutStore(paths).Load();

			Assert.True(layout.ShowRightBrowser);
		}
	}
}
