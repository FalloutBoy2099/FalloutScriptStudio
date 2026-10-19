namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.IO;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.IO;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Tests.Shell.Support;
	using FalloutScriptStudio.Workspace;
	using ICSharpCode.AvalonEdit.Document;
	using Xunit;

	public sealed class DocumentFileServiceTests
	{
		[Fact]
		public void The_save_filter_starts_with_the_type_of_the_document()
		{
			String[] script = DocumentFileService.BuildSaveFilter(DocumentKind.Script).Split('|');
			String[] message = DocumentFileService.BuildSaveFilter(DocumentKind.Message).Split('|');

			Assert.Equal(script.Length, message.Length);
			Assert.NotEqual(script[0], message[0]);
			Assert.Contains("*.ssl", script[1], StringComparison.Ordinal);
			Assert.Contains("*.msg", message[1], StringComparison.Ordinal);
		}

		[Fact]
		public void The_built_in_template_is_used_when_the_template_file_is_missing()
		{
			using WorkspaceHarness harness = new WorkspaceHarness();
			File.Delete(Path.Combine(harness.Paths.TemplatesDirectory, "newScript.txt"));
			DocumentFileService files = new DocumentFileService(harness.Settings, harness.Dialogs);

			String template = files.ReadDefaultTemplate();

			Assert.Contains("procedure start", template, StringComparison.Ordinal);
		}

		[Fact]
		public void Reading_a_script_normalizes_the_new_lines()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentFileService files = new DocumentFileService(harness.Settings, harness.Dialogs);
				String path = harness.Folder.Write("a.ssl", "one\ntwo\r\nthree");

				DecodedText decoded = await files.ReadAsync(path);

				Assert.Equal("one\r\ntwo\r\nthree", decoded.Text);
				await Task.CompletedTask;
			});
		}

		[Fact]
		public void Writing_a_document_saves_the_text_and_marks_the_document_as_saved()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentFileService files = new DocumentFileService(harness.Settings, harness.Dialogs);
				String path = harness.Folder.Combine("saved.ssl");
				DocumentViewModel document = new DocumentViewModel(null, DocumentKind.Script, new TextDocument("procedure start;\r\n")) { Encoding = TextEncodings.Ansi };
				document.IsModified = true;

				Boolean written = await files.WriteAsync(document, path);

				Assert.True(written);
				Assert.False(document.IsModified);
				Assert.Equal(File.GetLastWriteTimeUtc(path), document.LastWriteTimeUtc);
				Assert.StartsWith("procedure start;", File.ReadAllText(path), StringComparison.Ordinal);
			});
		}

		[Fact]
		public void Writing_to_an_unwritable_path_reports_an_error()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentFileService files = new DocumentFileService(harness.Settings, harness.Dialogs);
				DocumentViewModel document = new DocumentViewModel(null, DocumentKind.Script, new TextDocument("x")) { Encoding = TextEncodings.Ansi };
				document.IsModified = true;

				Boolean written = await files.WriteAsync(document, harness.Folder.Combine("missing-folder", "a.ssl"));

				Assert.False(written);
				Assert.True(document.IsModified);
				Assert.Single(harness.Dialogs.Messages);
			});
		}

		[Fact]
		public void Only_a_header_directly_in_the_headers_folder_is_global()
		{
			using WorkspaceHarness harness = new WorkspaceHarness();
			String headers = harness.Folder.Combine("headers");
			harness.Settings.Settings.Paths.HeadersDirectory = headers;
			DocumentFileService files = new DocumentFileService(harness.Settings, harness.Dialogs);

			Assert.True(files.IsGlobalHeader(Path.Combine(headers, "define.h")));
			Assert.False(files.IsGlobalHeader(Path.Combine(headers, "define.ssl")));
			Assert.False(files.IsGlobalHeader(Path.Combine(headers, "sub", "define.h")));
		}
	}
}