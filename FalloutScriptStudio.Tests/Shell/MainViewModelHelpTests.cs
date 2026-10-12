namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using FalloutScriptStudio.Tests.Shell.Support;
	using FalloutScriptStudio.ViewModels.Main;
	using Xunit;

	[Collection("Test collection for FalloutScriptStudio.Tests.Shell.MainViewModelToolsTests")]
	public sealed class MainViewModelHelpTests
	{
		[Fact]
		public void The_first_run_flow_shows_settings_and_marks_the_run_as_completed()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Dialogs.ConfirmAnswer = false;
				Assert.False(harness.Settings.Settings.IsFirstRunCompleted);

				await harness.Main.RunFirstRunAsync();

				Assert.Contains("settings", harness.ToolWindows.Calls);
				Assert.True(harness.Settings.Settings.IsFirstRunCompleted);

				harness.ToolWindows.Calls.Clear();
				await harness.Main.RunFirstRunAsync();
				Assert.Empty(harness.ToolWindows.Calls);
			});
		}

		[Fact]
		public void The_first_run_never_asks_to_associate_the_file_types()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Dialogs.ConfirmAnswer = true;

				await harness.Main.RunFirstRunAsync();

				Assert.Equal(0, harness.FileAssociation.AssociateCount);
				Assert.Empty(harness.Dialogs.Confirmations);
				Assert.True(harness.Settings.Settings.IsFirstRunCompleted);
			});
		}

		[Fact]
		public void The_help_menu_associates_the_file_types_when_the_user_confirms()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Dialogs.ConfirmAnswer = true;

				await harness.Main.AssociateFileTypesAsync();

				Assert.Equal(1, harness.FileAssociation.AssociateCount);
				Assert.Equal(harness.Paths.ResourcesDirectory, harness.FileAssociation.LastIconsDirectory);
				Assert.Single(harness.Dialogs.Confirmations);
				Assert.Single(harness.Dialogs.Messages);
			});
		}

		[Fact]
		public void The_help_menu_does_nothing_when_the_user_declines_the_association()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Dialogs.ConfirmAnswer = false;

				await harness.Main.AssociateFileTypesAsync();

				Assert.Equal(0, harness.FileAssociation.AssociateCount);
				Assert.Empty(harness.Dialogs.Messages);
			});
		}

		[Fact]
		public void The_help_menu_reports_a_registry_failure_instead_of_crashing()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Dialogs.ConfirmAnswer = true;
				harness.FileAssociation.FailWith = new UnauthorizedAccessException("denied");

				await harness.Main.AssociateFileTypesAsync();

				Assert.Equal(0, harness.FileAssociation.AssociateCount);
				String message = Assert.Single(harness.Dialogs.Messages);
				Assert.Contains("denied", message);
			});
		}

		[Fact]
		public void Help_opens_the_sfall_documentation_site_in_the_default_browser()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();

				await harness.Main.HelpAsync();

				Assert.Equal("https://sfall-team.github.io/sfall/", MainViewModel.DocumentationUrl);
				Assert.Equal(MainViewModel.DocumentationUrl, Assert.Single(harness.Shell.Opened));
				Assert.Empty(harness.Dialogs.Messages);
			});
		}

		[Fact]
		public void Help_tells_the_user_the_address_when_no_browser_could_be_started()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Shell.OpenWithShellResult = false;

				await harness.Main.HelpAsync();

				String message = Assert.Single(harness.Dialogs.Messages);
				Assert.Contains(MainViewModel.DocumentationUrl, message, StringComparison.Ordinal);
			});
		}

		[Fact]
		public void The_about_command_opens_the_about_window()
		{
			using ShellHarness harness = new ShellHarness();

			harness.Main.AboutCommand.Execute(null);

			Assert.Contains("about", harness.ToolWindows.Calls);
		}
	}
}
