namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Tests.Shell.Support;
	using ICSharpCode.AvalonEdit.Document;
	using Xunit;

	public sealed class ParseCoordinatorTests
	{
		private const String ScriptA = "procedure alpha;\r\n\r\nprocedure alpha begin\r\nend\r\n";
		private const String ScriptB = "procedure beta;\r\n\r\nprocedure beta begin\r\nend\r\n";

		private static DocumentViewModel CreateScript(String text, DocumentKind kind = DocumentKind.Script)
		{
			String? path = kind == DocumentKind.Script ? @"C:\scripts\test.ssl" : @"C:\scripts\test.msg";
			return new DocumentViewModel(path, kind, new TextDocument(text));
		}

		[Fact]
		public void Rapid_edits_are_coalesced_into_one_parse()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness(TimeSpan.FromMilliseconds(600));
				DocumentViewModel document = CreateScript(ScriptA);
				harness.Coordinator.Attach(document);

				document.TextDocument.Insert(0, "// 1\r\n");
				document.TextDocument.Insert(0, "// 2\r\n");
				document.TextDocument.Insert(0, "// 3\r\n");
				Assert.True(document.NeedsParse);

				Assert.True(await Eventually.TrueAsync(() => document.ProgramInfo is not null));
				await Task.Delay(1000);

				Assert.Equal(1, harness.Parser.Started);
				Assert.StartsWith("// 3", harness.Parser.Texts[0], StringComparison.Ordinal);
				Assert.False(document.NeedsParse);
			});
		}

		[Fact]
		public void A_newer_edit_cancels_the_running_parse_and_only_the_latest_result_is_used()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness(TimeSpan.FromMilliseconds(10));
				TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
				harness.Parser.Gate = release.Task;
				DocumentViewModel document = CreateScript(ScriptA);
				harness.Coordinator.Attach(document);

				Task first = harness.Coordinator.ParseAsync(document, true);
				await harness.Parser.WaitForStartAsync();
				document.TextDocument.Text = ScriptB;
				await first;
				release.SetResult();
				Assert.True(await Eventually.TrueAsync(() => !harness.Coordinator.IsPending(document)));

				Assert.NotNull(document.ProgramInfo);
				Assert.True(document.ProgramInfo!.ProcedureExists("beta"));
				Assert.False(document.ProgramInfo.ProcedureExists("alpha"));
			});
		}

		[Fact]
		public void Immediate_parse_does_not_wait_for_the_debounce_delay()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness(TimeSpan.FromSeconds(30));
				DocumentViewModel document = CreateScript(ScriptA);

				await harness.Coordinator.ParseAsync(document, true);

				Assert.NotNull(document.ProgramInfo);
				Assert.True(document.ProgramInfo!.ProcedureExists("alpha"));
				Assert.False(document.NeedsParse);
			});
		}

		[Fact]
		public void Detaching_cancels_the_pending_parse()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness(TimeSpan.FromMilliseconds(150));
				DocumentViewModel document = CreateScript(ScriptA);
				harness.Coordinator.Attach(document);

				document.TextDocument.Insert(0, "// edit\r\n");
				Assert.True(harness.Coordinator.IsPending(document));
				harness.Coordinator.Detach(document);
				await Task.Delay(350);

				Assert.Equal(0, harness.Parser.Started);
				Assert.Null(document.ProgramInfo);
				document.TextDocument.Insert(0, "// again\r\n");
				Assert.False(harness.Coordinator.IsPending(document));
			});
		}

		[Fact]
		public void Message_files_are_not_parsed()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness(TimeSpan.FromMilliseconds(10));
				DocumentViewModel document = CreateScript("{100}{}{Hello}", DocumentKind.Message);

				await harness.Coordinator.ParseAsync(document, true);

				Assert.Equal(0, harness.Parser.Started);
				Assert.Null(document.ProgramInfo);
			});
		}

		[Fact]
		public void The_parsed_event_is_raised_and_the_status_is_updated()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness(TimeSpan.FromMilliseconds(10));
				DocumentViewModel document = CreateScript(ScriptA);
				DocumentViewModel? raised = null;
				harness.Coordinator.Parsed += (_, parsed) => raised = parsed;

				await harness.Coordinator.ParseAsync(document, true, true);

				Assert.Same(document, raised);
				Assert.NotEqual(ParserStatusKind.None, document.ParserStatus.Kind);
				Assert.StartsWith("Parser:", document.ParserStatus.ToText(), StringComparison.Ordinal);
			});
		}

		[Fact]
		public void Disabling_the_parser_reports_macros_only()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness(TimeSpan.FromMilliseconds(10));
				harness.Settings.Update(settings => settings.Editor.EnableParser = false);
				DocumentViewModel document = CreateScript(ScriptA);

				await harness.Coordinator.ParseAsync(document, true);

				Assert.Equal(ParserStatusKind.OffMacros, document.ParserStatus.Kind);
				Assert.Contains("macros only", document.ParserStatus.ToText(), StringComparison.Ordinal);
			});
		}

		[Fact]
		public void Parser_diagnostics_become_error_entries_of_the_document()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness(TimeSpan.FromMilliseconds(10));
				DocumentViewModel document = CreateScript("procedure broken begin\r\n   x := 1;\r\n");

				await harness.Coordinator.ParseAsync(document, true);

				Assert.NotEmpty(document.ParserErrors);
				Assert.All(document.ParserErrors, error => Assert.Equal(document.FilePath, error.FileName));
				Assert.False(String.IsNullOrWhiteSpace(document.ParserLog));
			});
		}
	}
}