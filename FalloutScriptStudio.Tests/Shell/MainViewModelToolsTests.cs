namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Tests.Shell.Support;
	using Xunit;
	using static FalloutScriptStudio.Tests.Shell.MainViewModelToolsTestData;

	[Collection("Test collection for FalloutScriptStudio.Tests.Shell.MainViewModelToolsTests")]
	public sealed class MainViewModelToolsTests
	{
		[Fact]
		public void The_message_editor_command_opens_the_associated_file_of_a_saved_script()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("scripts/npc.ssl", Script);
				harness.Folder.Write("scripts/npc.msg", "{100}{}{Hi}\r\n");

				await harness.Main.OpenMessageEditorAsync();

				Assert.Equal(document.FilePath, Assert.Single(harness.MessageEditor.Opened));
				Assert.NotNull(Assert.Single(harness.MessageEditor.Targets));
			});
		}

		[Fact]
		public void Without_a_script_the_message_editor_opens_empty()
		{
			using ShellHarness harness = new ShellHarness();

			harness.Main.OpenMessageEditorCommand.Execute(null);

			Assert.Null(Assert.Single(harness.MessageEditor.Opened));
		}

		[Fact]
		public void The_flowchart_needs_an_associated_message_file()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				await harness.OpenFileAsync("scripts/npc.ssl", Script);

				await harness.Main.ShowFlowchartAsync();
				Assert.Empty(harness.ScriptTools.Calls);

				harness.Folder.Write("scripts/npc.msg", "{100}{}{Hi}\r\n");
				await harness.Main.ShowFlowchartAsync();
				await harness.Main.ShowDialoguePreviewAsync();
				Assert.Equal(new[] { "flowchart:npc.ssl", "preview:npc.ssl" }, harness.ScriptTools.Calls);
			});
		}

		[Fact]
		public void Compile_commands_reach_the_compiler()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Settings.Update(settings => settings.Compiler.CompileToSourceDirectory = true);
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);

				await harness.Main.CompileAsync();
				await harness.Main.CompileAllOpenAsync();

				Assert.Equal(new[] { document.FilePath, document.FilePath }, harness.Compiler.Compiled);
			});
		}

		[Fact]
		public void Updating_the_parser_data_parses_immediately()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				Int32 before = harness.Parser.Started;

				await harness.Main.UpdateParserDataAsync();

				Assert.True(harness.Parser.Started > before);
				Assert.NotNull(document.ProgramInfo);
			});
		}

		[Fact]
		public void The_project_folder_command_reports_a_missing_folder_and_opens_an_existing_one()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				await harness.Main.OpenProjectFolderAsync();
				Assert.Single(harness.Dialogs.Messages);

				harness.Settings.Update(settings => settings.Paths.SolutionFolder = harness.Folder.Path);
				await harness.Main.OpenProjectFolderAsync();
				Assert.Equal(harness.Folder.Path, Assert.Single(harness.Shell.Folders));
				Assert.Equal("Project: " + System.IO.Path.GetFileName(harness.Folder.Path), harness.Main.ProjectText);
			});
		}
	}
}