namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.IO;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Messages;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Services.MessageEditor;
	using FalloutScriptStudio.Tests.Shell.Support;
	using Xunit;

	public sealed class WorkspaceMessageTests
	{
		private const String Script = "procedure start begin\r\nend\r\n";

		[Fact]
		public void The_message_file_next_to_the_script_is_found_and_its_texts_are_cached()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel document = await harness.OpenFileAsync("scripts/npc.ssl", Script);
				harness.Folder.Write("scripts/npc.msg", "{100}{}{Hello there.}\r\n{101}{}{Bye.}\r\n");

				String? path = await harness.Workspace.AssociateMessageFileAsync(document, false, false);

				Assert.Equal(harness.Folder.Combine("scripts", "npc.msg"), path);
				Assert.Equal(path, document.MessageFilePath);
				Assert.Equal("Hello there.", harness.Workspace.GetMessageText(document, 100));
				Assert.Null(harness.Workspace.GetMessageText(document, 999));
			});
		}

		[Fact]
		public void A_missing_message_file_is_created_when_the_user_confirms()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel document = await harness.OpenFileAsync("scripts/npc.ssl", Script);
				harness.Dialogs.ConfirmAnswer = true;

				String? path = await harness.Workspace.AssociateMessageFileAsync(document, true, false);

				Assert.NotNull(path);
				Assert.True(File.Exists(path));
			});
		}

		[Fact]
		public void A_missing_message_file_is_not_created_without_confirmation()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel document = await harness.OpenFileAsync("scripts/npc.ssl", Script);
				harness.Dialogs.ConfirmAnswer = false;

				String? path = await harness.Workspace.AssociateMessageFileAsync(document, true, false);

				Assert.Null(path);
				Assert.Null(document.MessageFilePath);
			});
		}

		[Fact]
		public void Headers_cannot_have_a_message_file()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel document = await harness.OpenFileAsync("scripts/defs.h", "#define A 1\r\n");

				String? path = await harness.Workspace.AssociateMessageFileAsync(document, true, true);

				Assert.Null(path);
				Assert.Single(harness.Dialogs.Messages);
			});
		}

		[Fact]
		public void The_message_editor_is_opened_with_a_target_that_inserts_numbers_into_the_script()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel document = await harness.OpenFileAsync("scripts/npc.ssl", Script);
				FakeEditorSurface editor = harness.AttachEditor(document);
				harness.Folder.Write("scripts/npc.msg", "{100}{}{Hello.}\r\n");

				await harness.Workspace.OpenMessageEditorAsync(document);

				IMessageLineTarget target = Assert.Single(harness.MessageEditor.Targets)!;
				Assert.Equal(document.FilePath, target.ScriptPath);
				Assert.True(target.CanAcceptMessageLine);
				target.AcceptMessageLine(101, "Text");
				Assert.StartsWith("101", document.TextDocument.Text, StringComparison.Ordinal);
				Assert.Contains("focus", editor.Calls);
			});
		}

		[Fact]
		public void Messages_saved_by_the_editor_refresh_the_cache()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel document = await harness.OpenFileAsync("scripts/npc.ssl", Script);
				IMessageLineTarget target = harness.Workspace.CreateMessageTarget(document);
				MessageDocument messages = MessageDocument.Parse(new[] { "{200}{}{New text}" });

				target.MessagesUpdated(harness.Folder.Combine("scripts", "npc.msg"), messages);

				Assert.Equal("New text", harness.Workspace.GetMessageText(document, 200));
				Assert.Equal(harness.Folder.Combine("scripts", "npc.msg"), document.MessageFilePath);
			});
		}

		[Fact]
		public void With_auto_open_the_message_file_is_opened_in_a_background_tab()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				harness.Settings.Update(settings => settings.Messages.AutoOpenMessageFiles = true);
				String msg = harness.Folder.Write("scripts/npc.msg", "{100}{}{Hello.}\r\n");

				DocumentViewModel script = await harness.OpenFileAsync("scripts/npc.ssl", Script);

				Assert.True(await Eventually.TrueAsync(() => harness.Workspace.FindDocument(msg) is not null));
				Assert.Same(script, harness.Workspace.ActiveDocument);
				Assert.Equal("Hello.", harness.Workspace.GetMessageText(script, 100));
			});
		}

		[Fact]
		public void With_auto_open_and_the_editor_option_the_message_editor_is_used_instead()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				harness.Settings.Update(settings =>
				{
					settings.Messages.AutoOpenMessageFiles = true;
					settings.Messages.OpenEditorAutomatically = true;
				});
				harness.Folder.Write("scripts/npc.msg", "{100}{}{Hello.}\r\n");

				DocumentViewModel script = await harness.OpenFileAsync("scripts/npc.ssl", Script);

				Assert.True(await Eventually.TrueAsync(() => harness.MessageEditor.Opened.Count == 1));
				Assert.Single(harness.Workspace.Documents);
				Assert.Same(script, harness.Workspace.ActiveDocument);
			});
		}

		[Fact]
		public void A_message_file_can_be_opened_as_text_from_the_editor()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel document = await harness.OpenFileAsync("scripts/npc.ssl", Script);
				String msg = harness.Folder.Write("scripts/npc.msg", "{100}{}{Hello.}\r\n");
				IMessageLineTarget target = harness.Workspace.CreateMessageTarget(document);

				Boolean opened = target.TryOpenAsText(msg);
				Assert.True(opened);
				Assert.True(await Eventually.TrueAsync(() => harness.Workspace.FindDocument(msg) is not null));
				Assert.Equal(DocumentKind.Message, harness.Workspace.FindDocument(msg)!.Kind);
			});
		}
	}
}