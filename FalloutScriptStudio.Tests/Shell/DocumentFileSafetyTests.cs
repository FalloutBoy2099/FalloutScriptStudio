namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.IO;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Tests.Shell.Support;
	using FalloutScriptStudio.Workspace;
	using Xunit;

	/// <summary>Saving must neither lose characters or keystrokes nor damage the file when something goes wrong.</summary>
	public sealed class DocumentFileSafetyTests
	{
		private const String Script = "procedure start begin\r\nend\r\n";

		private static DocumentFileService CreateService(WorkspaceHarness harness, IDocumentFileWriter? writer = null)
		{
			return new DocumentFileService(harness.Settings, harness.Dialogs, null, writer);
		}

		[Fact]
		public void Typing_while_the_file_is_being_written_keeps_the_document_modified()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				document.TextDocument.Insert(0, "// first\r\n");
				CallbackDocumentFileWriter writer = new CallbackDocumentFileWriter(async (path, content) =>
				{
					document.TextDocument.Insert(0, "// typed during the write\r\n");
					await File.WriteAllBytesAsync(path, content);
				});

				Boolean written = await CreateService(harness, writer).WriteAsync(document, document.FilePath!);

				Assert.True(written);
				Assert.True(document.IsModified);
				Assert.StartsWith("// first", File.ReadAllText(document.FilePath!), StringComparison.Ordinal);
				Assert.DoesNotContain("typed during", File.ReadAllText(document.FilePath!), StringComparison.Ordinal);
				Assert.Equal(File.GetLastWriteTimeUtc(document.FilePath!), document.LastWriteTimeUtc);
			});
		}

		[Fact]
		public void A_document_that_did_not_change_during_the_write_is_marked_saved()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				document.TextDocument.Insert(0, "// first\r\n");

				Boolean written = await CreateService(harness).WriteAsync(document, document.FilePath!);

				Assert.True(written);
				Assert.False(document.IsModified);
				Assert.True(document.TextDocument.UndoStack.IsOriginalFile);
			});
		}

		[Fact]
		public void A_failed_write_does_not_trim_the_text_of_the_document()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				harness.Settings.Update(settings => settings.Editor.AutoRemoveTrailingSpaces = true);
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", "line one   \r\nline two\t\r\n");
				document.TextDocument.Insert(0, "x");
				CallbackDocumentFileWriter writer = new CallbackDocumentFileWriter((_, _) => throw new IOException("disk full"));

				Boolean written = await CreateService(harness, writer).WriteAsync(document, document.FilePath!);

				Assert.False(written);
				Assert.Equal("xline one   \r\nline two\t\r\n", document.TextDocument.Text);
				Assert.True(document.IsModified);
				Assert.Contains("disk full", Assert.Single(harness.Dialogs.Messages), StringComparison.Ordinal);
			});
		}

		[Fact]
		public void A_successful_save_trims_the_text_of_the_document_and_marks_it_saved()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				harness.Settings.Update(settings => settings.Editor.AutoRemoveTrailingSpaces = true);
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", "line one   \r\nline two\t\r\n");
				document.TextDocument.Insert(0, "x");

				Boolean written = await CreateService(harness).WriteAsync(document, document.FilePath!);

				Assert.True(written);
				Assert.Equal("xline one\r\nline two\r\n", document.TextDocument.Text);
				Assert.False(document.IsModified);
			});
		}

		[Fact]
		public void Characters_the_code_page_cannot_store_are_not_replaced_silently()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				harness.Settings.Update(settings => settings.Editor.FileEncoding = TextEncodingKind.Oem866);
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				document.TextDocument.Insert(0, "// café\r\n");
				harness.Dialogs.YesNoCancelAnswers.Enqueue(YesNoCancel.Cancel);

				Boolean written = await CreateService(harness).WriteAsync(document, document.FilePath!);

				Assert.False(written);
				Assert.True(document.IsModified);
				Assert.Equal(Script, File.ReadAllText(document.FilePath!));
				Assert.Contains("U+00E9", Assert.Single(harness.Dialogs.Confirmations), StringComparison.Ordinal);
			});
		}

		[Fact]
		public void The_user_can_save_a_script_with_unrepresentable_characters_as_utf8()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				harness.Settings.Update(settings => settings.Editor.FileEncoding = TextEncodingKind.Oem866);
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				document.TextDocument.Insert(0, "// café\r\n");
				harness.Dialogs.YesNoCancelAnswers.Enqueue(YesNoCancel.Yes);

				Boolean written = await CreateService(harness).WriteAsync(document, document.FilePath!);

				Assert.True(written);
				Assert.False(document.IsModified);
				Assert.Equal("// café\r\n" + Script, new UTF8Encoding(false, true).GetString(File.ReadAllBytes(document.FilePath!)));
				Assert.Equal(65001, document.Encoding!.CodePage);
			});
		}

		[Fact]
		public void The_user_can_save_with_replacement_characters_anyway()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				harness.Settings.Update(settings => settings.Editor.FileEncoding = TextEncodingKind.Oem866);
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				document.TextDocument.Insert(0, "// 中\r\n");
				harness.Dialogs.YesNoCancelAnswers.Enqueue(YesNoCancel.No);

				Boolean written = await CreateService(harness).WriteAsync(document, document.FilePath!);

				Assert.True(written);
				Assert.Equal("// ?\r\n" + Script, TextEncodings.Oem866.GetString(File.ReadAllBytes(document.FilePath!)));
			});
		}

		[Fact]
		public void Text_the_encoding_can_store_is_saved_without_a_question()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				harness.Settings.Update(settings => settings.Editor.FileEncoding = TextEncodingKind.Oem866);
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				document.TextDocument.Insert(0, "// Привет\r\n");

				Boolean written = await CreateService(harness).WriteAsync(document, document.FilePath!);

				Assert.True(written);
				Assert.Empty(harness.Dialogs.Confirmations);
			});
		}

		[Fact]
		public void A_message_file_read_with_a_byte_order_mark_keeps_it_when_saved()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				String path = harness.Folder.Combine("npc.msg");
				File.WriteAllBytes(path, new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes("{100}{}{Привет}\r\n")).ToArray());
				DocumentViewModel document = (await harness.Workspace.OpenAsync(path))!;
				document.TextDocument.Insert(document.TextDocument.TextLength, "{101}{}{да}\r\n");

				Boolean saved = await harness.Workspace.SaveAsync(document);

				Assert.True(saved);
				Byte[] bytes = File.ReadAllBytes(path);
				Assert.Equal(new Byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3).ToArray());
				Assert.Equal("{100}{}{Привет}\r\n{101}{}{да}\r\n", Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3));
			});
		}

		[Fact]
		public void A_message_file_read_as_utf8_is_not_rewritten_in_the_ansi_code_page()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				String path = harness.Folder.Combine("npc.msg");
				File.WriteAllBytes(path, new UTF8Encoding(false).GetBytes("{100}{}{Привет}\r\n"));
				DocumentViewModel document = (await harness.Workspace.OpenAsync(path))!;
				document.TextDocument.Insert(document.TextDocument.TextLength, "{101}{}{да}\r\n");

				Boolean saved = await harness.Workspace.SaveAsync(document);

				Assert.True(saved);
				Assert.Equal("{100}{}{Привет}\r\n{101}{}{да}\r\n", new UTF8Encoding(false, true).GetString(File.ReadAllBytes(path)));
				Assert.Empty(harness.Dialogs.Confirmations);
			});
		}

		[Fact]
		public void A_message_file_in_a_code_page_follows_the_message_encoding_setting()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				harness.Settings.Update(settings => settings.Editor.FileEncoding = TextEncodingKind.Oem866);
				String path = harness.Folder.Combine("npc.msg");
				File.WriteAllBytes(path, TextEncodings.Oem866.GetBytes("{100}{}{Привет}\r\n"));
				DocumentViewModel document = (await harness.Workspace.OpenAsync(path))!;
				document.TextDocument.Insert(document.TextDocument.TextLength, "{101}{}{да}\r\n");

				await harness.Workspace.SaveAsync(document);

				Assert.Equal(TextEncodings.Oem866.GetBytes("{100}{}{Привет}\r\n{101}{}{да}\r\n"), File.ReadAllBytes(path));
			});
		}

		[Fact]
		public void The_atomic_writer_replaces_the_content_keeps_the_attributes_and_leaves_no_temporary_file()
		{
			ShellStaRunner.Run(async () =>
			{
				using TempFolder folder = new TempFolder();
				String path = folder.Write("a.ssl", "old");
				File.SetAttributes(path, FileAttributes.Hidden | FileAttributes.Archive);

				await new AtomicDocumentFileWriter().WriteAsync(path, new Byte[] { 0x6E, 0x65, 0x77 });

				Assert.Equal("new", File.ReadAllText(path));
				Assert.True((File.GetAttributes(path) & FileAttributes.Hidden) != 0);
				Assert.Equal(new[] { path }, Directory.GetFiles(folder.Path));
				await Task.CompletedTask;
			});
		}

		[Fact]
		public void The_atomic_writer_refuses_a_read_only_file_and_a_missing_folder()
		{
			ShellStaRunner.Run(async () =>
			{
				using TempFolder folder = new TempFolder();
				String path = folder.Write("a.ssl", "old");
				File.SetAttributes(path, FileAttributes.ReadOnly);
				AtomicDocumentFileWriter writer = new AtomicDocumentFileWriter();

				await Assert.ThrowsAsync<UnauthorizedAccessException>(() => writer.WriteAsync(path, new Byte[] { 1 }));
				await Assert.ThrowsAsync<DirectoryNotFoundException>(() => writer.WriteAsync(folder.Combine("missing", "b.ssl"), new Byte[] { 1 }));

				Assert.Equal("old", File.ReadAllText(path));
				Assert.False(Directory.Exists(folder.Combine("missing")));
				File.SetAttributes(path, FileAttributes.Normal);
			});
		}
	}
}
