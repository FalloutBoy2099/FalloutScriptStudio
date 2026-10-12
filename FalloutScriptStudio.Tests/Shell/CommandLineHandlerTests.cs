namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using FalloutScriptStudio.Tests.Shell.Support;
	using FalloutScriptStudio.Workspace;
	using Xunit;

	public sealed class CommandLineHandlerTests
	{
		[Fact]
		public void A_message_file_from_the_command_line_opens_in_a_maximized_message_editor()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				String msg = harness.Folder.Write("npc.msg", "{100}{}{Hello}\r\n");
				CommandLineHandler handler = new CommandLineHandler(harness.Workspace, harness.MessageEditor, harness.ScriptTools, harness.Dialogs);

				Boolean opened = await handler.HandleAsync(new[] { msg });

				Assert.False(opened);
				FalloutScriptStudio.Services.MessageEditor.MessageEditorOpenOptions options = Assert.Single(harness.MessageEditor.Options);
				Assert.Equal(msg, options.FilePath);
				Assert.True(options.StartMaximized);
				Assert.Null(options.Target);
			});
		}

		[Fact]
		public void The_message_file_sends_its_lines_to_the_active_script()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				await harness.OpenFileAsync("npc.ssl", "procedure start begin\r\nend\r\n");
				String msg = harness.Folder.Write("npc.msg", "{100}{}{Hello}\r\n");
				CommandLineHandler handler = new CommandLineHandler(harness.Workspace, harness.MessageEditor, harness.ScriptTools, harness.Dialogs);

				await handler.HandleAsync(new[] { msg });

				Assert.NotNull(Assert.Single(harness.MessageEditor.Options).Target);
				Assert.True(harness.MessageEditor.Options[0].StartMaximized);
			});
		}

		[Fact]
		public void A_folder_on_the_command_line_is_opened_as_the_project_and_the_window_comes_to_the_front()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				String folder = harness.Folder.Combine("mod");
				System.IO.Directory.CreateDirectory(folder);
				CommandLineHandler handler = new CommandLineHandler(harness.Workspace, harness.MessageEditor, harness.ScriptTools, harness.Dialogs, harness.Projects);

				Boolean opened = await handler.HandleAsync(new[] { folder });

				Assert.True(opened);
				Assert.Equal(folder, harness.Projects.Folder);
				Assert.Empty(harness.Workspace.Documents);
				Assert.Empty(harness.Dialogs.Messages);
			});
		}

		[Fact]
		public void A_folder_is_ignored_when_no_project_service_is_given()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				CommandLineHandler handler = new CommandLineHandler(harness.Workspace, harness.MessageEditor, harness.ScriptTools, harness.Dialogs);

				Boolean opened = await handler.HandleAsync(new[] { harness.Folder.Path });

				Assert.False(opened);
				Assert.Empty(harness.Workspace.Documents);
			});
		}
	}
}
