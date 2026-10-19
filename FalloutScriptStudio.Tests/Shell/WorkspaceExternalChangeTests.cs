namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Tests.Shell.Support;
	using Xunit;

	public sealed class WorkspaceExternalChangeTests
	{
		private const String Original = "// original\r\n";

		private static void Touch(String path, String text)
		{
			File.WriteAllText(path, text);
			File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(5));
		}

		[Fact]
		public void An_unchanged_file_is_left_alone()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				await harness.OpenFileAsync("a.ssl", Original);

				await harness.Workspace.CheckExternalChangesAsync();

				Assert.Empty(harness.Dialogs.Confirmations);
			});
		}

		[Fact]
		public void A_changed_file_is_reloaded_when_the_user_agrees_and_the_caret_line_is_kept()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", String.Join("\r\n", Enumerable.Range(1, 40).Select(i => "// old " + i)));
				FakeEditorSurface editor = harness.AttachEditor(document);
				editor.GoTo(25);
				harness.Dialogs.ConfirmAnswer = true;
				Touch(document.FilePath!, String.Join("\r\n", Enumerable.Range(1, 40).Select(i => "// new " + i)));

				await harness.Workspace.CheckExternalChangesAsync();

				Assert.StartsWith("// new 1", document.TextDocument.Text, StringComparison.Ordinal);
				Assert.False(document.IsModified);
				Assert.Equal(25, editor.LastGoTo!.Value.Line);
				Assert.Single(harness.Dialogs.Confirmations);
			});
		}

		[Fact]
		public void A_changed_file_is_kept_as_is_when_the_user_declines_and_the_question_is_not_repeated()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Original);
				harness.Dialogs.ConfirmAnswer = false;
				Touch(document.FilePath!, "// changed outside\r\n");

				await harness.Workspace.CheckExternalChangesAsync();
				await harness.Workspace.CheckExternalChangesAsync();

				Assert.Equal(Original, document.TextDocument.Text);
				Assert.Single(harness.Dialogs.Confirmations);
			});
		}

		[Fact]
		public void The_warning_mentions_lost_changes_for_a_modified_document()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Original);
				document.TextDocument.Insert(0, "// mine\r\n");
				harness.Dialogs.ConfirmAnswer = false;
				Touch(document.FilePath!, "// changed outside\r\n");

				await harness.Workspace.CheckExternalChangesAsync();

				Assert.Contains("unsaved changes", harness.Dialogs.Confirmations[0], StringComparison.OrdinalIgnoreCase);
			});
		}

		[Fact]
		public void A_deleted_file_is_ignored()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Original);
				File.Delete(document.FilePath!);

				await harness.Workspace.CheckExternalChangesAsync();

				Assert.Empty(harness.Dialogs.Confirmations);
				Assert.Single(harness.Workspace.Documents);
			});
		}
	}
}