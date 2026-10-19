namespace FalloutScriptStudio.Tests.ToolWindows
{
	using System;
	using System.Linq;
	using FalloutScriptStudio.Tests.Shell.Support;
	using FalloutScriptStudio.ViewModels.ToolWindows;
	using Xunit;

	/// <summary>The button of the Code editor page of the Settings window that opens the snippet manager.</summary>
	public sealed class SnippetSettingsTests
	{
		private static SettingsViewModel Create(FakeToolWindowService? toolWindows)
		{
			return new SettingsViewModel(new FakeSettingsService(@"C:\fss"), new FakeThemeService(), new FakeDialogService(), new FalloutScriptStudio.Tests.FakeFileAssociationService(), new FakeFontCatalog("Consolas"), toolWindows);
		}

		[Fact]
		public async System.Threading.Tasks.Task ManageSnippets_OpensTheManager()
		{
			FakeToolWindowService toolWindows = new FakeToolWindowService();
			SettingsViewModel viewModel = Create(toolWindows);

			Assert.True(viewModel.ManageSnippetsCommand.CanExecute(null));
			await viewModel.ManageSnippetsCommand.ExecuteAsync(null);

			Assert.Equal("SnippetManager", Assert.Single(toolWindows.Calls));
		}

		[Fact]
		public void WithoutAToolWindowService_TheButtonIsOff()
		{
			Assert.False(Create(null).ManageSnippetsCommand.CanExecute(null));
		}
	}
}