namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.IO;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Tests.Shell.Support;
	using FalloutScriptStudio.Workspace;
	using Xunit;

	/// <summary>Opening, decompiling and background work must not duplicate tabs, lose files or fail silently.</summary>
	public sealed class WorkspaceRobustnessTests
	{
		private const String Script = "procedure start begin\r\nend\r\n";

		[Fact]
		public void Two_simultaneous_opens_of_the_same_file_give_one_tab()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				String path = harness.Folder.Write("a.ssl", Script);

				Task<DocumentViewModel?> first = harness.Workspace.OpenAsync(path, new OpenDocumentOptions());
				Task<DocumentViewModel?> second = harness.Workspace.OpenAsync(path, new OpenDocumentOptions());
				DocumentViewModel?[] results = await Task.WhenAll(first, second);

				Assert.Single(harness.Workspace.Documents);
				Assert.NotNull(results[0]);
				Assert.Same(results[0], results[1]);
			});
		}

		[Fact]
		public void Decompiling_onto_an_open_script_replaces_its_text_in_the_same_tab_after_confirmation()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel open = await harness.OpenFileAsync("scripts/npc.ssl", "// old\r\n");
				String compiled = harness.Folder.Write("scripts/npc.int", "binary");
				harness.Compiler.DecompiledText = Script;
				harness.Dialogs.SaveFileAnswer = open.FilePath;
				harness.Dialogs.ConfirmAnswer = true;

				DocumentViewModel? result = await harness.Workspace.OpenAsync(compiled);

				Assert.Same(open, result);
				Assert.Single(harness.Workspace.Documents);
				Assert.Equal(Script, open.TextDocument.Text);
				Assert.Equal(Script, File.ReadAllText(open.FilePath!));
				Assert.False(open.IsModified);
			});
		}

		[Fact]
		public void Declining_the_overwrite_of_an_open_script_keeps_the_file_and_the_tab()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel open = await harness.OpenFileAsync("scripts/npc.ssl", "// old\r\n");
				String compiled = harness.Folder.Write("scripts/npc.int", "binary");
				harness.Compiler.DecompiledText = Script;
				harness.Dialogs.SaveFileAnswer = open.FilePath;
				harness.Dialogs.ConfirmAnswer = false;

				DocumentViewModel? result = await harness.Workspace.OpenAsync(compiled);

				Assert.Same(open, result);
				Assert.Single(harness.Workspace.Documents);
				Assert.Equal("// old\r\n", File.ReadAllText(open.FilePath!));
				Assert.Equal("// old\r\n", open.TextDocument.Text);
			});
		}

		[Fact]
		public void A_decompiled_script_that_cannot_be_written_is_reported_and_kept_as_an_unsaved_tab()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				String compiled = harness.Folder.Write("npc.int", "binary");
				harness.Compiler.DecompiledText = Script;
				harness.Dialogs.SaveFileAnswer = harness.Folder.Combine("missing-folder", "npc.ssl");

				DocumentViewModel? document = await harness.Workspace.OpenAsync(compiled);

				Assert.NotNull(document);
				Assert.Null(document!.FilePath);
				Assert.True(document.IsModified);
				Assert.Equal(Script, document.TextDocument.Text);
				Assert.Single(harness.Dialogs.Messages);
			});
		}

		[Fact]
		public void A_copy_of_an_open_file_that_cannot_be_read_is_reported_instead_of_throwing()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel open = await harness.OpenFileAsync("a.ssl", Script);
				harness.Dialogs.ConfirmAnswer = true;
				using FileStream locked = new FileStream(open.FilePath!, FileMode.Open, FileAccess.Read, FileShare.None);

				DocumentViewModel? copy = await harness.Workspace.OpenAsync(open.FilePath!, new OpenDocumentOptions { PromptIfAlreadyOpen = true });

				Assert.Null(copy);
				Assert.Single(harness.Workspace.Documents);
				Assert.Single(harness.Dialogs.Messages);
			});
		}

		[Fact]
		public void A_failing_background_operation_is_reported_in_the_status_bar()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();

				await harness.Workspace.GuardAsync(() => throw new InvalidOperationException("boom"));

				Assert.Contains("boom", Assert.Single(harness.Output.Statuses), StringComparison.Ordinal);
			});
		}

		[Fact]
		public void A_cancelled_background_operation_is_not_reported()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();

				await harness.Workspace.GuardAsync(() => throw new OperationCanceledException());

				Assert.Empty(harness.Output.Statuses);
			});
		}

		[Fact]
		public void A_failure_while_opening_the_message_editor_after_a_file_is_opened_reaches_the_status_bar()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				harness.Settings.Update(settings =>
				{
					settings.Messages.AutoOpenMessageFiles = true;
					settings.Messages.OpenEditorAutomatically = true;
				});
				harness.MessageEditor.OpenForScriptFailure = new InvalidOperationException("editor exploded");

				await harness.OpenFileAsync("a.ssl", Script);

				Assert.True(await Eventually.TrueAsync(() => harness.Output.Statuses.Any(s => s.Contains("editor exploded", StringComparison.Ordinal))));
			});
		}

		[Fact]
		public void A_message_file_read_that_ends_after_the_script_was_closed_does_not_bring_the_script_back()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel script = await harness.OpenFileAsync("scripts/npc.ssl", Script);
				harness.Folder.Write("scripts/npc.msg", "{100}{}{Hello}\r\n");
				await harness.Workspace.AssociateMessageFileAsync(script, false, false);
				Assert.Equal("Hello", harness.Workspace.GetMessageText(script, 100));
				harness.Folder.Write("scripts/npc.msg", "{100}{}{Changed}\r\n");

				Task refresh = harness.Workspace.RefreshMessageTextsAsync(script);
				await harness.Workspace.CloseAsync(script);
				await refresh;

				Assert.Null(harness.Workspace.GetMessageText(script, 100));
			});
		}

		[Fact]
		public void References_are_read_with_the_configured_file_encoding()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				harness.Settings.Update(settings => settings.Editor.FileEncoding = TextEncodingKind.Oem866);
				String path = harness.Folder.Combine("a.ssl");
				File.WriteAllBytes(path, TextEncodings.Oem866.GetBytes("// Привет start\r\n"));
				ReferenceResultsPresenter presenter = new ReferenceResultsPresenter(harness.Dialogs, harness.Output, harness.Settings);

				await presenter.ShowAsync("start", new[] { SourceReference.Create(path, 1) }, new Dictionary<String, String>());

				Assert.Equal("// Привет start", Assert.Single(harness.Output.SearchResults).Message);
			});
		}
	}
}