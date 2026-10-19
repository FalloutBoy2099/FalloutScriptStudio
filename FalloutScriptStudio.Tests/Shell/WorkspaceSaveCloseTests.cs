namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Tests.Shell.Support;
	using Xunit;

	public sealed class WorkspaceSaveCloseTests
	{
		private const String Script = "procedure start;\r\n\r\nprocedure start begin\r\nend\r\n";

		[Fact]
		public void Editing_marks_the_document_modified_and_undoing_everything_clears_the_mark()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);

				document.TextDocument.Insert(0, "// edit\r\n");
				Assert.True(document.IsModified);
				Assert.EndsWith("*", document.Title, StringComparison.Ordinal);

				document.TextDocument.UndoStack.Undo();
				Assert.False(document.IsModified);
			});
		}

		[Fact]
		public void Saving_writes_the_file_and_clears_the_modified_mark()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				document.TextDocument.Insert(0, "// edit\r\n");

				Boolean saved = await harness.Workspace.SaveAsync(document);

				Assert.True(saved);
				Assert.False(document.IsModified);
				Assert.StartsWith("// edit", File.ReadAllText(document.FilePath!), StringComparison.Ordinal);
				Assert.Equal(File.GetLastWriteTimeUtc(document.FilePath!), document.LastWriteTimeUtc);
			});
		}

		[Fact]
		public void Saving_removes_trailing_spaces_when_the_option_is_on()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				harness.Settings.Update(settings => settings.Editor.AutoRemoveTrailingSpaces = true);
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", "line one   \r\nline two\t\r\n");
				document.TextDocument.Insert(0, "x");

				await harness.Workspace.SaveAsync(document);

				Assert.Equal("xline one\r\nline two\r\n", File.ReadAllText(document.FilePath!));
			});
		}

		[Fact]
		public void Saving_as_utf8_writes_without_a_byte_order_mark()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				harness.Settings.Update(settings => settings.Editor.SaveScriptsAsUtf8 = true);
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", "// plain\r\n");
				document.TextDocument.Insert(0, "// é\r\n");

				await harness.Workspace.SaveAsync(document);

				Byte[] bytes = File.ReadAllBytes(document.FilePath!);
				Assert.NotEqual(0xEF, bytes[0]);
				Assert.Contains("é", System.Text.Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
			});
		}

		[Fact]
		public void Saving_an_unsaved_document_asks_for_a_file_name()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel document = harness.Workspace.CreateNew("// new\r\n");
				harness.Dialogs.SaveFileAnswer = harness.Folder.Combine("saved.ssl");

				Boolean saved = await harness.Workspace.SaveAsync(document);

				Assert.True(saved);
				Assert.Equal(harness.Folder.Combine("saved.ssl"), document.FilePath);
				Assert.False(document.IsModified);
				Assert.Contains(document.FilePath, harness.Settings.Recent.Scripts);
			});
		}

		[Fact]
		public void Cancelling_the_save_dialog_keeps_the_document_unsaved()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel document = harness.Workspace.CreateNew("// new\r\n");
				harness.Dialogs.SaveFileAnswer = null;

				Boolean saved = await harness.Workspace.SaveAsync(document);

				Assert.False(saved);
				Assert.Null(document.FilePath);
				Assert.True(document.IsModified);
			});
		}

		[Fact]
		public void Save_as_changes_the_path_and_the_document_kind()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				harness.Dialogs.SaveFileAnswer = harness.Folder.Combine("renamed.h");

				Boolean saved = await harness.Workspace.SaveAsAsync(document);

				Assert.True(saved);
				Assert.Equal(DocumentKind.Header, document.Kind);
				Assert.Equal("renamed.h", document.FileName);
				Assert.True(File.Exists(harness.Folder.Combine("renamed.h")));
			});
		}

		[Fact]
		public void Closing_a_clean_document_removes_the_tab_without_asking()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				DocumentViewModel? closed = null;
				harness.Workspace.DocumentClosed += (_, closedDocument) => closed = closedDocument;

				Boolean result = await harness.Workspace.CloseAsync(document);

				Assert.True(result);
				Assert.Empty(harness.Workspace.Documents);
				Assert.Null(harness.Workspace.ActiveDocument);
				Assert.Same(document, closed);
				Assert.Empty(harness.Dialogs.Confirmations);
			});
		}

		[Fact]
		public void Closing_a_modified_document_saves_it_when_the_user_says_yes()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				document.TextDocument.Insert(0, "// edit\r\n");
				harness.Dialogs.YesNoCancelAnswers.Enqueue(YesNoCancel.Yes);

				Boolean result = await harness.Workspace.CloseAsync(document);

				Assert.True(result);
				Assert.StartsWith("// edit", File.ReadAllText(harness.Folder.Combine("a.ssl")), StringComparison.Ordinal);
			});
		}

		[Fact]
		public void Closing_a_modified_document_discards_changes_when_the_user_says_no()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				document.TextDocument.Insert(0, "// edit\r\n");
				harness.Dialogs.YesNoCancelAnswers.Enqueue(YesNoCancel.No);

				Boolean result = await harness.Workspace.CloseAsync(document);

				Assert.True(result);
				Assert.Equal(Script, File.ReadAllText(harness.Folder.Combine("a.ssl")));
			});
		}

		[Fact]
		public void Cancelling_the_save_question_keeps_the_tab_open()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				document.TextDocument.Insert(0, "// edit\r\n");
				harness.Dialogs.YesNoCancelAnswers.Enqueue(YesNoCancel.Cancel);

				Boolean result = await harness.Workspace.CloseAsync(document);

				Assert.False(result);
				Assert.Single(harness.Workspace.Documents);
			});
		}

		[Fact]
		public void Closing_the_active_tab_selects_the_previously_active_one()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel a = await harness.OpenFileAsync("a.ssl", Script);
				DocumentViewModel b = await harness.OpenFileAsync("b.ssl", Script);
				DocumentViewModel c = await harness.OpenFileAsync("c.ssl", Script);
				harness.Workspace.ActiveDocument = a;
				harness.Workspace.ActiveDocument = c;

				await harness.Workspace.CloseAsync(c);

				Assert.Same(a, harness.Workspace.ActiveDocument);
				Assert.True(a.IsActive);
				Assert.False(b.IsActive);
			});
		}

		[Fact]
		public void Closing_all_but_one_keeps_only_that_tab()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				await harness.OpenFileAsync("a.ssl", Script);
				DocumentViewModel keep = await harness.OpenFileAsync("b.ssl", Script);
				await harness.OpenFileAsync("c.ssl", Script);

				Boolean result = await harness.Workspace.CloseAllAsync(keep);

				Assert.True(result);
				Assert.Single(harness.Workspace.Documents);
				Assert.Same(keep, harness.Workspace.Documents[0]);
			});
		}

		[Fact]
		public void Closing_all_stops_when_the_user_cancels()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel a = await harness.OpenFileAsync("a.ssl", Script);
				DocumentViewModel b = await harness.OpenFileAsync("b.ssl", Script);
				a.TextDocument.Insert(0, "// a\r\n");
				b.TextDocument.Insert(0, "// b\r\n");
				harness.Dialogs.YesNoCancelAnswers.Enqueue(YesNoCancel.No);
				harness.Dialogs.YesNoCancelAnswers.Enqueue(YesNoCancel.Cancel);

				Boolean result = await harness.Workspace.CloseAllAsync();

				Assert.False(result);
				Assert.Equal(2, harness.Workspace.Documents.Count);
				Assert.True(a.IsModified);
				Assert.True(b.IsModified);
			});
		}

		[Fact]
		public void Cancelling_the_close_of_several_modified_documents_loses_no_change()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel a = await harness.OpenFileAsync("a.ssl", Script);
				DocumentViewModel b = await harness.OpenFileAsync("b.ssl", Script);
				a.TextDocument.Insert(0, "// a\r\n");
				b.TextDocument.Insert(0, "// b\r\n");
				harness.Dialogs.YesNoCancelAnswers.Enqueue(YesNoCancel.Yes);
				harness.Dialogs.YesNoCancelAnswers.Enqueue(YesNoCancel.Cancel);

				Boolean result = await harness.Workspace.CloseAllAsync();

				Assert.False(result);
				Assert.Equal(2, harness.Workspace.Documents.Count);
				Assert.StartsWith("// b", b.TextDocument.Text, StringComparison.Ordinal);
				Assert.Equal(Script, File.ReadAllText(b.FilePath!));
				Assert.Equal(Script, File.ReadAllText(a.FilePath!));
			});
		}

		[Fact]
		public void Closing_all_saves_and_closes_only_after_every_question_was_answered()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel a = await harness.OpenFileAsync("a.ssl", Script);
				DocumentViewModel b = await harness.OpenFileAsync("b.ssl", Script);
				a.TextDocument.Insert(0, "// a\r\n");
				b.TextDocument.Insert(0, "// b\r\n");
				harness.Dialogs.YesNoCancelAnswers.Enqueue(YesNoCancel.Yes);
				harness.Dialogs.YesNoCancelAnswers.Enqueue(YesNoCancel.No);

				Boolean result = await harness.Workspace.CloseAllAsync();

				Assert.True(result);
				Assert.Empty(harness.Workspace.Documents);
				Assert.Equal(Script, File.ReadAllText(a.FilePath!));
				Assert.StartsWith("// b", File.ReadAllText(b.FilePath!), StringComparison.Ordinal);
			});
		}

		[Fact]
		public void A_refusal_of_the_step_before_closing_leaves_every_tab_and_file_alone()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel a = await harness.OpenFileAsync("a.ssl", Script);
				DocumentViewModel b = await harness.OpenFileAsync("b.ssl", Script);
				a.TextDocument.Insert(0, "// a\r\n");
				harness.Dialogs.YesNoCancelAnswers.Enqueue(YesNoCancel.Yes);
				Int32 calls = 0;

				Boolean result = await harness.Workspace.CloseAllAsync(null, () =>
				{
					calls++;
					return Task.FromResult(false);
				});

				Assert.False(result);
				Assert.Equal(1, calls);
				Assert.Equal(new[] { a, b }, harness.Workspace.Documents.ToArray());
				Assert.True(a.IsModified);
				Assert.Equal(Script, File.ReadAllText(a.FilePath!));
			});
		}

		[Fact]
		public void Save_as_uses_the_encoding_and_the_message_refresh_of_the_new_file_type()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				harness.Settings.Update(settings => settings.Editor.FileEncoding = FalloutScriptStudio.Core.Configuration.TextEncodingKind.Oem866);
				DocumentViewModel script = await harness.OpenFileAsync("scripts/npc.ssl", Script);
				String target = harness.Folder.Combine("scripts", "npc.msg");
				script.MessageFilePath = target;
				DocumentViewModel untitled = harness.Workspace.CreateNew("{100}{}{\u041f\u0440\u0438\u0432\u0435\u0442}\r\n");
				harness.Dialogs.SaveFileAnswer = target;

				Boolean saved = await harness.Workspace.SaveAsAsync(untitled);

				Assert.True(saved);
				Assert.Equal(DocumentKind.Message, untitled.Kind);
				Assert.Equal(FalloutScriptStudio.Core.IO.TextEncodings.Oem866.GetBytes("{100}{}{\u041f\u0440\u0438\u0432\u0435\u0442}\r\n"), File.ReadAllBytes(target));
				Assert.Equal("\u041f\u0440\u0438\u0432\u0435\u0442", harness.Workspace.GetMessageText(script, 100));
			});
		}

		[Fact]
		public void Saving_all_saves_every_modified_document()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel a = await harness.OpenFileAsync("a.ssl", Script);
				DocumentViewModel b = await harness.OpenFileAsync("b.ssl", Script);
				DocumentViewModel c = await harness.OpenFileAsync("c.ssl", Script);
				a.TextDocument.Insert(0, "// a\r\n");
				c.TextDocument.Insert(0, "// c\r\n");

				Boolean result = await harness.Workspace.SaveAllAsync();

				Assert.True(result);
				Assert.False(a.IsModified);
				Assert.False(c.IsModified);
				Assert.False(b.IsModified);
				Assert.StartsWith("// c", File.ReadAllText(c.FilePath!), StringComparison.Ordinal);
			});
		}

		[Fact]
		public void Closing_remembers_the_caret_line_of_the_script()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel document = await harness.OpenFileAsync("long.ssl", String.Join("\r\n", Enumerable.Range(1, 60).Select(i => "// line " + i)));
				FakeEditorSurface editor = harness.AttachEditor(document);
				editor.GoTo(33);

				await harness.Workspace.CloseAsync(document);

				Assert.Equal(33, harness.Settings.ScriptPositions.Get("long.ssl"));
				Assert.Contains("commit", editor.Calls);
			});
		}
	}
}