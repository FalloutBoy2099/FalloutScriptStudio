namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Services.ScriptTools.Procedures;
	using FalloutScriptStudio.Tests.Shell.Support;
	using Xunit;

	public sealed class WorkspaceProcedureTests
	{
		private const String Script =
			"procedure start;\r\n" +
			"procedure helper;\r\n" +
			"\r\n" +
			"procedure start begin\r\n" +
			"   call helper;\r\n" +
			"end\r\n" +
			"\r\n" +
			"procedure helper begin\r\n" +
			"end\r\n";

		[Fact]
		public void Creating_a_procedure_at_the_end_adds_declaration_and_body()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				harness.ScriptTools.ProcedureAnswer = new ProcedureNameResult("fresh", "fresh", ProcedurePlacement.AtEnd, false);

				await harness.Workspace.CreateProcedureAsync(document);

				String text = document.TextDocument.Text;
				Assert.Contains("procedure fresh;", text, StringComparison.Ordinal);
				Assert.EndsWith("procedure fresh begin\r\n\r\nend\r\n", text, StringComparison.Ordinal);
				Assert.True(document.ProgramInfo!.ProcedureExists("fresh"));
				Assert.True(document.IsModified);
			});
		}

		[Fact]
		public void A_procedure_can_be_created_after_the_current_one_with_the_arguments_in_the_declaration()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				FakeEditorSurface editor = harness.AttachEditor(document);
				await harness.Workspace.RequestParseAsync(document, true);
				editor.CurrentProcedure = document.ProgramInfo!.GetProcedure("start");
				harness.ScriptTools.ProcedureAnswer = new ProcedureNameResult("middle", "middle(variable a)", ProcedurePlacement.AfterCurrent, false);

				await harness.Workspace.CreateProcedureAsync(document);

				String text = document.TextDocument.Text;
				Assert.Contains("procedure middle(variable a);", text, StringComparison.Ordinal);
				Assert.True(text.IndexOf("procedure middle(variable a) begin", StringComparison.Ordinal) > text.IndexOf("procedure start begin", StringComparison.Ordinal));
				Assert.True(text.IndexOf("procedure middle(variable a) begin", StringComparison.Ordinal) < text.IndexOf("procedure helper begin", StringComparison.Ordinal));
				Assert.True(harness.ScriptTools.LastRequest!.CanCopyBody);
			});
		}

		[Fact]
		public void Cancelling_the_procedure_prompt_changes_nothing()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				harness.ScriptTools.ProcedureAnswer = null;

				await harness.Workspace.CreateProcedureAsync(document);

				Assert.Equal(Script, document.TextDocument.Text);
			});
		}

		[Fact]
		public void Creating_a_standard_handler_asks_with_a_read_only_name()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				harness.ScriptTools.ProcedureAnswer = new ProcedureNameResult("look_at_p_proc", "look_at_p_proc", ProcedurePlacement.AtEnd, false);

				await harness.Workspace.CreateHandlerAsync(document, "look_at_p_proc");

				Assert.True(harness.ScriptTools.LastRequest!.NameReadOnly);
				Assert.Contains("script_overrides;", document.TextDocument.Text, StringComparison.Ordinal);
			});
		}

		[Fact]
		public void A_handler_that_already_exists_is_refused()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				await harness.Workspace.RequestParseAsync(document, true);

				await harness.Workspace.CreateHandlerAsync(document, "start");

				Assert.Single(harness.Dialogs.Messages);
				Assert.Equal(Script, document.TextDocument.Text);
			});
		}

		[Fact]
		public void Deleting_a_procedure_removes_declaration_and_body()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);

				Boolean deleted = await harness.Workspace.DeleteProcedureAsync(document, "helper");

				Assert.True(deleted);
				Assert.DoesNotContain("helper begin", document.TextDocument.Text, StringComparison.Ordinal);
				Assert.DoesNotContain("procedure helper;", document.TextDocument.Text, StringComparison.Ordinal);
				Assert.False(document.ProgramInfo!.GetProcedure("helper") is { StartLine: > 0 });
			});
		}

		[Fact]
		public void Moving_a_procedure_changes_the_order_of_the_bodies_in_one_undo_step()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);

				Boolean moved = await harness.Workspace.MoveProcedureAsync(document, "helper", "start");

				Assert.True(moved);
				String text = document.TextDocument.Text;
				Assert.True(text.IndexOf("procedure helper begin", StringComparison.Ordinal) < text.IndexOf("procedure start begin", StringComparison.Ordinal));
				document.TextDocument.UndoStack.Undo();
				Assert.Equal(Script, document.TextDocument.Text);
			});
		}

		[Fact]
		public void References_are_listed_with_their_line_text_and_column()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);

				await harness.Workspace.ShowReferencesAsync("helper", new[]
				{
					new SourceReference(document.FilePath, 2),
					new SourceReference(document.FilePath, 5),
				});

				Assert.Equal(2, harness.Output.SearchResults.Count);
				Assert.Equal("call helper;", harness.Output.SearchResults[1].Message);
				Assert.Equal(9, harness.Output.SearchResults[1].Column);
				Assert.Equal(6, harness.Output.SearchResults[1].Length);
				Assert.Equal("'helper' references", harness.Output.SearchTitle);
			});
		}

		[Fact]
		public void No_references_are_reported_with_a_message()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);

				await harness.Workspace.ShowReferencesAsync("helper", Array.Empty<SourceReference>());

				Assert.Single(harness.Dialogs.Messages);
				Assert.Empty(harness.Output.SearchResults);
			});
		}

		[Fact]
		public void Renaming_a_procedure_updates_every_line_that_mentions_it()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				await harness.Workspace.RequestParseAsync(document, true);
				ScriptProcedure procedure = document.ProgramInfo!.GetProcedure("helper")!;
				harness.ScriptTools.ProcedureAnswer = new ProcedureNameResult("assistant", "assistant", ProcedurePlacement.AtEnd, false);

				await harness.Workspace.RenameSymbolAsync(document, procedure);

				String text = document.TextDocument.Text;
				Assert.DoesNotContain("helper", text, StringComparison.Ordinal);
				Assert.Equal(3, text.Split("assistant").Length - 1);
			});
		}
	}
}