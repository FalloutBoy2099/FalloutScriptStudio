namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using FalloutScriptStudio.Tests.Shell.Support;
	using Xunit;
	using static FalloutScriptStudio.Tests.Shell.MainViewModelToolsTestData;

	[Collection("Test collection for FalloutScriptStudio.Tests.Shell.MainViewModelToolsTests")]
	public sealed class MainViewModelRegisterTests
	{
		[Fact]
		public void The_scripts_list_editor_opens_without_a_script()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();

				await harness.Main.ScriptsListAsync();

				Assert.Equal("register:<none>", Assert.Single(harness.ScriptTools.Calls));
			});
		}

		[Theory]
		[InlineData("toolongname1.ssl", "8 characters")]
		[InlineData("a b.ssl", "spaces")]
		public void Registering_checks_the_script_name(String fileName, String expectedMessage)
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				await harness.OpenFileAsync(fileName, Script);

				await harness.Main.RegisterScriptAsync();

				Assert.Contains(expectedMessage, Assert.Single(harness.Dialogs.Messages), StringComparison.Ordinal);
				Assert.Empty(harness.ScriptTools.Calls);
			});
		}

		[Fact]
		public void Registering_a_valid_script_opens_the_registration_window()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				await harness.OpenFileAsync("npc.ssl", Script);

				await harness.Main.RegisterScriptAsync();

				Assert.Equal("register:npc.ssl", Assert.Single(harness.ScriptTools.Calls));
			});
		}

		[Fact]
		public void Global_scripts_are_confirmed_once_by_the_script_tool_service_not_by_the_main_view_model()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				await harness.OpenFileAsync("glob.ssl", Script);
				harness.Dialogs.ConfirmAnswer = false;

				await harness.Main.RegisterScriptAsync();

				Assert.Equal("register:glob.ssl", Assert.Single(harness.ScriptTools.Calls));
				Assert.Empty(harness.Dialogs.Confirmations);
			});
		}

		[Fact]
		public void Unsaved_scripts_cannot_be_registered()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Workspace.CreateNew(Script);

				await harness.Main.RegisterScriptAsync();

				Assert.Single(harness.Dialogs.Messages);
				Assert.Empty(harness.ScriptTools.Calls);
			});
		}
	}
}
