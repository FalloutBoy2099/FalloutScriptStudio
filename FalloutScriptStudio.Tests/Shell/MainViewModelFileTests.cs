namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Tests.Shell.Support;
	using FalloutScriptStudio.ViewModels.Main;
	using Xunit;

	public sealed class MainViewModelFileTests
	{
		private const String Script = "procedure start begin\r\nend\r\n";

		[Fact]
		public void The_recent_list_shows_the_newest_file_first_and_follows_opens()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				await harness.OpenFileAsync("one.ssl", Script);
				await harness.OpenFileAsync("two.ssl", Script);
				await harness.OpenFileAsync("three.ssl", Script);

				Assert.Equal(new[] { "three.ssl", "two.ssl", "one.ssl" }, harness.Main.RecentFiles.Select(Path.GetFileName));
			});
		}

		[Fact]
		public void Opening_a_recent_entry_activates_the_file()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel one = await harness.OpenFileAsync("one.ssl", Script);
				await harness.OpenFileAsync("two.ssl", Script);
				harness.Dialogs.ConfirmAnswer = false;

				await harness.Main.OpenRecentAsync(one.FilePath);

				Assert.Same(one, harness.Workspace.ActiveDocument);
				Assert.Equal(2, harness.Workspace.Documents.Count);
			});
		}

		[Fact]
		public void Clearing_the_recent_list_asks_first()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				await harness.OpenFileAsync("one.ssl", Script);
				harness.Dialogs.ConfirmAnswer = false;
				await harness.Main.ClearRecentAsync();
				Assert.Single(harness.Main.RecentFiles);

				harness.Dialogs.ConfirmAnswer = true;
				await harness.Main.ClearRecentAsync();

				Assert.Empty(harness.Main.RecentFiles);
				Assert.Empty(harness.Settings.Recent.Scripts);
			});
		}

		[Fact]
		public void The_template_menu_lists_the_template_files()
		{
			using ShellHarness harness = new ShellHarness();

			Assert.Equal("dialog", Assert.Single(harness.Main.Templates).Name);
		}

		[Fact]
		public void A_new_script_from_a_template_is_an_unsaved_document_with_the_template_text()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				TemplateItem template = harness.Main.Templates.Single(t => t.Name == "dialog");

				await harness.Main.NewFromTemplateAsync(template);

				DocumentViewModel document = Assert.Single(harness.Workspace.Documents);
				Assert.Null(document.FilePath);
				Assert.StartsWith("// dialog template", document.TextDocument.Text, StringComparison.Ordinal);
				Assert.True(harness.Main.HasDocument);
				Assert.True(harness.Main.IsScript);
			});
		}

		[Fact]
		public void The_open_command_opens_the_chosen_files()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Dialogs.OpenFileAnswer = harness.Folder.Write("picked.ssl", Script);

				await harness.Main.OpenAsync();

				Assert.Equal("picked.ssl", harness.Workspace.ActiveDocument!.FileName);
			});
		}

		[Fact]
		public void Saving_as_a_template_writes_into_the_templates_folder_and_refreshes_the_menu()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				await harness.OpenFileAsync("a.ssl", "// my template\r\n");
				harness.Dialogs.SaveFileAnswer = Path.Combine(harness.Paths.TemplatesDirectory, "mine.ssl");

				await harness.Main.SaveAsTemplateAsync();

				Assert.Equal("// my template\r\n", File.ReadAllText(Path.Combine(harness.Paths.TemplatesDirectory, "mine.ssl")));
				Assert.Contains(harness.Main.Templates, t => t.Name == "mine");
			});
		}

		[Fact]
		public void Close_commands_use_the_given_tab_or_the_active_one()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel a = await harness.OpenFileAsync("a.ssl", Script);
				DocumentViewModel b = await harness.OpenFileAsync("b.ssl", Script);
				DocumentViewModel c = await harness.OpenFileAsync("c.ssl", Script);

				await harness.Main.CloseDocumentAsync(a);
				Assert.Equal(new[] { b, c }, harness.Workspace.Documents.ToArray());
				await harness.Main.CloseDocumentAsync(null);
				Assert.Equal(new[] { b }, harness.Workspace.Documents.ToArray());
				await harness.Main.CloseAllButThisAsync(b);
				Assert.Single(harness.Workspace.Documents);
				await harness.Main.CloseAllAsync();
				Assert.Empty(harness.Workspace.Documents);
				Assert.False(harness.Main.HasDocument);
			});
		}

		[Fact]
		public void The_window_may_close_when_nothing_is_modified()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				await harness.OpenFileAsync("a.ssl", Script);

				Assert.True(await harness.Main.CanCloseAsync());
				Assert.Empty(harness.Workspace.Documents);
			});
		}

		[Fact]
		public void The_window_stays_open_when_the_user_cancels_a_save_question()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				document.TextDocument.Insert(0, "// x\r\n");

				Assert.False(await harness.Main.CanCloseAsync());
				Assert.Single(harness.Workspace.Documents);
			});
		}

		[Fact]
		public void Cancelling_the_message_editors_on_exit_keeps_every_tab_and_unsaved_change()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel a = await harness.OpenFileAsync("a.ssl", Script);
				DocumentViewModel b = await harness.OpenFileAsync("b.ssl", Script);
				a.TextDocument.Insert(0, "// a\r\n");
				b.TextDocument.Insert(0, "// b\r\n");
				harness.Dialogs.YesNoCancelAnswers.Enqueue(YesNoCancel.No);
				harness.Dialogs.YesNoCancelAnswers.Enqueue(YesNoCancel.Yes);
				harness.MessageEditor.CloseAllResult = false;

				Boolean canClose = await harness.Main.CanCloseAsync();

				Assert.False(canClose);
				Assert.Equal(1, harness.MessageEditor.CloseAllCount);
				Assert.Equal(2, harness.Workspace.Documents.Count);
				Assert.True(a.IsModified);
				Assert.True(b.IsModified);
				Assert.Equal(Script, File.ReadAllText(a.FilePath!));
			});
		}

		[Fact]
		public void Cancelling_a_later_save_question_on_exit_leaves_the_message_editors_open()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel a = await harness.OpenFileAsync("a.ssl", Script);
				DocumentViewModel b = await harness.OpenFileAsync("b.ssl", Script);
				a.TextDocument.Insert(0, "// a\r\n");
				b.TextDocument.Insert(0, "// b\r\n");
				harness.Dialogs.YesNoCancelAnswers.Enqueue(YesNoCancel.No);
				harness.Dialogs.YesNoCancelAnswers.Enqueue(YesNoCancel.Cancel);

				Boolean canClose = await harness.Main.CanCloseAsync();

				Assert.False(canClose);
				Assert.Equal(0, harness.MessageEditor.CloseAllCount);
				Assert.Equal(2, harness.Workspace.Documents.Count);
			});
		}

		[Fact]
		public void Opening_files_by_path_prompts_for_copies_of_open_files()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel a = await harness.OpenFileAsync("a.ssl", Script);
				harness.Dialogs.ConfirmAnswer = false;

				await harness.Main.OpenFilesAsync(new[] { a.FilePath!, harness.Folder.Write("b.ssl", Script) });

				Assert.Equal(2, harness.Workspace.Documents.Count);
				Assert.Single(harness.Dialogs.Confirmations);
			});
		}

		[Fact]
		public void The_external_editor_is_asked_for_once_and_remembered()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				String editor = harness.Folder.Write("editor.exe", "x");
				harness.Dialogs.OpenFileAnswer = editor;

				await harness.Main.OpenInExternalEditorAsync(document);

				Assert.Equal(editor, harness.Settings.Settings.Paths.ExternalEditorPath);
				Assert.Equal(editor + " \"" + document.FilePath + "\"", Assert.Single(harness.Shell.Programs));
				harness.Dialogs.OpenFileAnswer = null;
				await harness.Main.OpenInExternalEditorAsync(document);
				Assert.Equal(2, harness.Shell.Programs.Count);
			});
		}

		[Fact]
		public void Tab_commands_reveal_and_copy_the_path()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);

				harness.Main.OpenContainingFolder(document);
				harness.Main.CopyDocumentPath(document);

				Assert.Equal(document.FilePath, Assert.Single(harness.Shell.Revealed));
				Assert.Equal(document.FilePath, harness.Shell.Clipboard);
			});
		}

		[Fact]
		public void Next_and_previous_document_wrap_around()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel a = await harness.OpenFileAsync("a.ssl", Script);
				DocumentViewModel b = await harness.OpenFileAsync("b.ssl", Script);
				DocumentViewModel c = await harness.OpenFileAsync("c.ssl", Script);

				harness.Main.NextDocumentCommand.Execute(null);
				Assert.Same(a, harness.Workspace.ActiveDocument);
				harness.Main.PreviousDocumentCommand.Execute(null);
				Assert.Same(c, harness.Workspace.ActiveDocument);
				harness.Main.PreviousDocumentCommand.Execute(null);
				Assert.Same(b, harness.Workspace.ActiveDocument);
			});
		}

		[Fact]
		public void The_exit_command_raises_the_exit_request()
		{
			using ShellHarness harness = new ShellHarness();
			Int32 requested = 0;
			harness.Main.ExitRequested += (_, _) => requested++;

			harness.Main.ExitCommand.Execute(null);

			Assert.Equal(1, requested);
		}
	}
}