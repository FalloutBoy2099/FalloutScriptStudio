namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.IO;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Tests.Shell.Support;
	using Xunit;

	public sealed class WorkspaceMessageRefreshTests
	{
		private const String Script = "procedure start begin\r\nend\r\n";

		[Fact]
		public void Selecting_the_script_tab_reads_the_message_file_again()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel script = await harness.OpenFileAsync("scripts/npc.ssl", Script);
				String msg = harness.Folder.Write("scripts/npc.msg", "{100}{}{Old text}\r\n");
				await harness.Workspace.AssociateMessageFileAsync(script, false, false);
				DocumentViewModel other = await harness.OpenFileAsync("scripts/other.ssl", Script);
				File.WriteAllText(msg, "{100}{}{New text}\r\n");

				harness.Workspace.ActiveDocument = script;

				Assert.True(await Eventually.TrueAsync(() => harness.Workspace.GetMessageText(script, 100) == "New text"));
				Assert.NotNull(other);
			});
		}

		[Fact]
		public void Selecting_the_script_tab_uses_the_unsaved_text_of_the_open_message_tab()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel script = await harness.OpenFileAsync("scripts/npc.ssl", Script);
				DocumentViewModel message = await harness.OpenFileAsync("scripts/npc.msg", "{100}{}{Old text}\r\n");
				await harness.Workspace.AssociateMessageFileAsync(script, false, false);
				message.TextDocument.Text = "{100}{}{Typed text}\r\n";

				harness.Workspace.ActiveDocument = message;
				harness.Workspace.ActiveDocument = script;

				Assert.True(await Eventually.TrueAsync(() => harness.Workspace.GetMessageText(script, 100) == "Typed text"));
				Assert.True(message.IsModified);
			});
		}

		[Fact]
		public void Saving_the_message_tab_refreshes_the_texts_of_the_script()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel script = await harness.OpenFileAsync("scripts/npc.ssl", Script);
				DocumentViewModel message = await harness.OpenFileAsync("scripts/npc.msg", "{100}{}{Old text}\r\n");
				await harness.Workspace.AssociateMessageFileAsync(script, false, false);
				message.TextDocument.Text = "{100}{}{Saved text}\r\n";

				await harness.Workspace.SaveAsync(message);

				Assert.Equal("Saved text", harness.Workspace.GetMessageText(script, 100));
			});
		}

		[Fact]
		public void Reloading_the_message_tab_after_an_external_change_refreshes_the_texts_of_the_script()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel script = await harness.OpenFileAsync("scripts/npc.ssl", Script);
				DocumentViewModel message = await harness.OpenFileAsync("scripts/npc.msg", "{100}{}{Old text}\r\n");
				await harness.Workspace.AssociateMessageFileAsync(script, false, false);
				File.WriteAllText(message.FilePath!, "{100}{}{External text}\r\n");

				await harness.Workspace.ReloadAsync(message);

				Assert.Equal("External text", harness.Workspace.GetMessageText(script, 100));
			});
		}

		[Fact]
		public void Checking_external_changes_refreshes_the_texts_of_the_active_script()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel script = await harness.OpenFileAsync("scripts/npc.ssl", Script);
				String msg = harness.Folder.Write("scripts/npc.msg", "{100}{}{Old text}\r\n");
				await harness.Workspace.AssociateMessageFileAsync(script, false, false);
				File.WriteAllText(msg, "{100}{}{Changed outside}\r\n");

				await harness.Workspace.CheckExternalChangesAsync();

				Assert.Equal("Changed outside", harness.Workspace.GetMessageText(script, 100));
			});
		}

		[Fact]
		public void A_deleted_message_file_keeps_the_previous_texts()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel script = await harness.OpenFileAsync("scripts/npc.ssl", Script);
				String msg = harness.Folder.Write("scripts/npc.msg", "{100}{}{Old text}\r\n");
				await harness.Workspace.AssociateMessageFileAsync(script, false, false);
				File.Delete(msg);

				await harness.Workspace.RefreshMessageTextsAsync(script);

				Assert.Equal("Old text", harness.Workspace.GetMessageText(script, 100));
			});
		}
	}
}