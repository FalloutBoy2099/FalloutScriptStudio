namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.Linq;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Tests.Shell.Support;
	using Xunit;

	public sealed class MainViewModelAssociateTests
	{
		private const String Script = "procedure start begin\r\nend\r\n";

		[Fact]
		public void Associating_without_the_message_editor_opens_the_message_file_in_a_text_tab()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Settings.Update(settings => settings.Messages.OpenEditorAutomatically = false);
				DocumentViewModel script = await harness.OpenFileAsync("scripts/npc.ssl", Script);
				String msg = harness.Folder.Write("scripts/npc.msg", "{100}{}{Hello}\r\n");
				harness.Workspace.ActiveDocument = script;

				await harness.Main.AssociateMessageFileAsync();

				DocumentViewModel message = Assert.Single(harness.Workspace.Documents, d => d.Kind == DocumentKind.Message);
				Assert.Equal(System.IO.Path.GetFullPath(msg), System.IO.Path.GetFullPath(message.FilePath!));
				Assert.Equal(System.IO.Path.GetFullPath(msg), System.IO.Path.GetFullPath(script.MessageFilePath!));
				Assert.Empty(harness.MessageEditor.Opened);
			});
		}

		[Fact]
		public void Associating_with_the_message_editor_option_opens_the_editor_instead()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Settings.Update(settings => settings.Messages.OpenEditorAutomatically = true);
				DocumentViewModel script = await harness.OpenFileAsync("scripts/npc.ssl", Script);
				harness.Folder.Write("scripts/npc.msg", "{100}{}{Hello}\r\n");
				harness.Workspace.ActiveDocument = script;

				await harness.Main.AssociateMessageFileAsync();

				Assert.NotEmpty(harness.MessageEditor.Opened);
				Assert.DoesNotContain(harness.Workspace.Documents, d => d.Kind == DocumentKind.Message);
			});
		}

		[Fact]
		public void Associating_a_script_without_a_message_file_opens_no_tab()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Settings.Update(settings => settings.Messages.OpenEditorAutomatically = false);
				DocumentViewModel script = await harness.OpenFileAsync("scripts/npc.ssl", Script);
				harness.Dialogs.ConfirmAnswer = false;
				harness.Workspace.ActiveDocument = script;

				await harness.Main.AssociateMessageFileAsync();

				Assert.Equal(new[] { script }, harness.Workspace.Documents.ToArray());
			});
		}
	}
}
