namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Services.ToolWindows;
	using FalloutScriptStudio.Tests.MessageEditor;
	using FalloutScriptStudio.Tests.Shell.Support;
	using FalloutScriptStudio.Workspace;
	using Xunit;

	/// <summary>The Dialogue menu commands that check the dialogue of the active script and compare the message files of the languages.</summary>
	public sealed class MainViewModelDialogueToolsTests
	{
		private static readonly String _dialogue = String.Join(
			"\r\n",
			"procedure talk_p_proc begin",
			"   Reply(100);",
			"   NOption(101, Node001, 4);",
			"   NOption(102, Node999, 4);",
			"end",
			"procedure Node001 begin",
			"   Reply(110);",
			"   NOption(111, Node999, 4);",
			"end",
			"procedure Node999 begin",
			"end",
			String.Empty);

		[Fact]
		public void Check_Dialogue_does_nothing_without_a_script_and_asks_to_save_an_unsaved_one()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				await harness.Main.CheckDialogueCommand.ExecuteAsync(null);
				Assert.Empty(harness.Dialogs.Messages);

				harness.Workspace.CreateNew(_dialogue);
				await harness.Main.CheckDialogueCommand.ExecuteAsync(null);

				Assert.Equal("Dialogue check: Save the script first: the problems point to a line of the file.", Assert.Single(harness.Dialogs.Messages));
			});
		}

		[Fact]
		public void Check_Dialogue_says_so_when_the_script_has_no_dialogue_or_no_weak_point()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				await harness.OpenFileAsync("scripts/plain.ssl", "procedure start begin\r\nend\r\n");
				await harness.Main.CheckDialogueCommand.ExecuteAsync(null);
				Assert.Equal("Dialogue check: The script has no dialogue to check: none of its nodes shows a reply, a message or an option.", Assert.Single(harness.Dialogs.Messages));

				harness.Dialogs.Messages.Clear();
				await harness.OpenFileAsync("scripts/npc.ssl", _dialogue);
				await harness.Main.CheckDialogueCommand.ExecuteAsync(null);

				Assert.Equal("Dialogue check: No weak point was found in the dialogue.", Assert.Single(harness.Dialogs.Messages));
			});
		}

		[Fact]
		public void Check_Dialogue_lists_the_weak_points_in_the_errors_tab()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				await harness.OpenFileAsync("scripts/npc.ssl", _dialogue.Replace("NOption(102, Node999, 4);", "NOption(102, Node404, 4);", StringComparison.Ordinal));
				harness.Main.Output.ShowTab(OutputTab.Parser);

				await harness.Main.CheckDialogueCommand.ExecuteAsync(null);

				Assert.Empty(harness.Dialogs.Messages);
				Assert.Equal((Int32)OutputTab.Errors, harness.Main.Output.SelectedTab);
				Assert.Contains(harness.Main.Output.Errors, item => item.Message.Contains("Node404", StringComparison.Ordinal));
			});
		}

		[Fact]
		public void Compare_Message_Files_opens_the_message_file_of_the_active_message_document()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel message = await harness.OpenFileAsync("text/english/dialog/npc.msg", "{100}{}{Hello.}\r\n");

				await harness.Main.CompareMessageFilesCommand.ExecuteAsync(null);

				Assert.Equal(message.FilePath, Assert.Single(harness.ToolWindows.CompareRequests).ReferencePath);
			});
		}

		[Fact]
		public void Compare_Message_Files_opens_the_associated_message_file_of_a_script()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel script = await harness.OpenFileAsync("scripts/npc.ssl", _dialogue);
				String messageFile = System.IO.Path.GetFullPath(harness.Folder.Write("scripts/npc.msg", "{100}{}{Hello.}\r\n"));
				await harness.Workspace.AssociateMessageFileAsync(script, false, false);

				await harness.Main.CompareMessageFilesCommand.ExecuteAsync(null);

				Assert.Equal(messageFile, Assert.Single(harness.ToolWindows.CompareRequests).ReferencePath);
			});
		}

		[Fact]
		public void Compare_Message_Files_asks_for_the_original_when_the_document_has_no_message_file()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				await harness.OpenFileAsync("scripts/lonely.ssl", "procedure start begin\r\nend\r\n");

				await harness.Main.CompareMessageFilesCommand.ExecuteAsync(null);
				Assert.Empty(harness.ToolWindows.CompareRequests);

				harness.Dialogs.OpenFileAnswer = @"C:\game\text\english\dialog\other.msg";
				await harness.Main.CompareMessageFilesCommand.ExecuteAsync(null);

				Assert.Equal(@"C:\game\text\english\dialog\other.msg", Assert.Single(harness.ToolWindows.CompareRequests).ReferencePath);
			});
		}

		[Fact]
		public void Compare_Message_Files_from_a_tab_uses_the_document_of_that_tab_and_not_the_active_one()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel message = await harness.OpenFileAsync("text/english/dialog/npc.msg", "{100}{}{Hello.}\r\n");
				await harness.OpenFileAsync("scripts/lonely.ssl", "procedure start begin\r\nend\r\n");

				await harness.Main.CompareMessageFilesCommand.ExecuteAsync(message);

				Assert.Equal(message.FilePath, Assert.Single(harness.ToolWindows.CompareRequests).ReferencePath);
			});
		}

		[Fact]
		public void The_message_editor_compares_the_file_it_edits_and_only_when_it_has_one()
		{
			StaTestRunner.Run(async () =>
			{
				FakeToolWindowService toolWindows = new();
				using MessageEditorHarness harness = new MessageEditorHarness(null, toolWindows);
				Assert.False(harness.Editor.CompareMessageFilesCommand.CanExecute(null));

				String path = harness.WriteFile("npc.msg", "{100}{}{Hello.}");
				await harness.Editor.LoadFileAsync(path);
				Assert.True(harness.Editor.CompareMessageFilesCommand.CanExecute(null));
				await harness.Editor.CompareMessageFilesCommand.ExecuteAsync(null);

				Assert.Equal(path, Assert.Single(toolWindows.CompareRequests).ReferencePath);
			});
		}

		[Fact]
		public void The_message_editor_cannot_compare_without_the_tool_windows()
		{
			StaTestRunner.Run(async () =>
			{
				using MessageEditorHarness harness = new MessageEditorHarness();
				await harness.Editor.LoadFileAsync(harness.WriteFile("npc.msg", "{100}{}{Hello.}"));

				Assert.False(harness.Editor.CompareMessageFilesCommand.CanExecute(null));
			});
		}
	}
}