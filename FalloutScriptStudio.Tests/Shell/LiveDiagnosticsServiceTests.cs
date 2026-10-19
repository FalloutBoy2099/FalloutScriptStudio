namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Analysis;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Problems;
	using FalloutScriptStudio.Tests.Shell.Support;
	using Xunit;

	/// <summary>The pipeline of the semantic diagnostics: parse, background analysis, document state, problem list, stale results and the quick fixes.</summary>
	public sealed class LiveDiagnosticsServiceTests
	{
		private const String WithTypo = "procedure start begin\r\n\tdisplay_msgg(\"a\");\r\nend\r\n";

		[Fact]
		public void A_parsed_script_gets_its_findings_in_the_document_and_in_the_problems()
		{
			ShellStaRunner.Run(async () =>
			{
				using DiagnosticsHarness harness = new DiagnosticsHarness();
				String path = harness.Write("project/a.ssl", WithTypo + "procedure other begin\r\n\tcall start(1);\r\nend\r\n");

				DocumentViewModel document = await harness.OpenAsync(path);

				Assert.Equal(new[] { "SSL1002", "SSL1001" }, document.LiveDiagnostics.Select(d => d.Code));
				Problem[] problems = harness.Findings();
				Assert.Equal(2, problems.Length);
				Assert.All(problems, problem =>
				{
					Assert.Equal(ProblemSource.Parse, problem.Source);
					Assert.Equal(ProblemSeverity.Error, problem.Severity);
					Assert.Equal(path, problem.File);
					Assert.Same(document, problem.Owner);
					Assert.NotNull(problem.Hint);
					Assert.True(problem.Line > 0 && problem.Column > 0 && problem.Length > 0);
				});
				Problem typo = problems.Single(p => p.MessageKey == "Str.Editor.Diag.UndefinedProcedure");
				Assert.Equal(2, typo.Line);
				Assert.Equal(2, typo.Column);
				Assert.Contains("display_msgg", typo.Message, StringComparison.Ordinal);
				Assert.Equal(ProblemActionKind.ApplyQuickFix, typo.Action!.Kind);
				Assert.Equal("Did you mean 'display_msg'?", typo.Action.Label);
				Assert.Null(problems.Single(p => p.MessageKey == "Str.Editor.Diag.WrongArgumentCount").Action);
			});
		}

		[Fact]
		public void A_script_without_findings_has_none()
		{
			ShellStaRunner.Run(async () =>
			{
				using DiagnosticsHarness harness = new DiagnosticsHarness();
				String path = harness.Write("project/a.ssl", "procedure start begin\r\n\tdisplay_msg(\"a\");\r\nend\r\n");

				DocumentViewModel document = await harness.OpenAsync(path);

				Assert.Empty(document.LiveDiagnostics);
				Assert.Empty(harness.Findings());
			});
		}

		[Fact]
		public void The_next_analysis_replaces_the_findings_of_the_previous_one()
		{
			ShellStaRunner.Run(async () =>
			{
				using DiagnosticsHarness harness = new DiagnosticsHarness();
				String path = harness.Write("project/a.ssl", WithTypo);
				DocumentViewModel document = await harness.OpenAsync(path);
				Assert.Single(harness.Findings());

				document.TextDocument.Text = "procedure start begin\r\n\tdisplay_msg(\"a\");\r\nend\r\n";
				await harness.Workspace.RequestParseAsync(document, true);
				await harness.WaitAsync(document);

				Assert.Empty(document.LiveDiagnostics);
				Assert.Empty(harness.Findings());
			});
		}

		[Fact]
		public void The_level_errors_only_drops_the_warnings_and_off_shows_nothing()
		{
			ShellStaRunner.Run(async () =>
			{
				using DiagnosticsHarness harness = new DiagnosticsHarness();
				String path = harness.Write("project/a.ssl", "procedure start begin\r\n\tdisplay_msg(\"a\", \"b\");\r\n\tundefined_thing(1);\r\nend\r\n");
				DocumentViewModel document = await harness.OpenAsync(path);
				Assert.Equal(new[] { "SSL2001", "SSL1002" }, document.LiveDiagnostics.Select(d => d.Code).OrderBy(c => c == "SSL2001" ? 0 : 1));

				harness.Settings.Settings.Editor.LiveDiagnostics = LiveDiagnosticsLevel.ErrorsOnly;
				await harness.Workspace.RequestParseAsync(document, true);
				await harness.WaitAsync(document);
				Assert.Equal(new[] { "SSL1002" }, document.LiveDiagnostics.Select(d => d.Code));
				Assert.Single(harness.Findings());

				harness.Settings.Settings.Editor.LiveDiagnostics = LiveDiagnosticsLevel.Off;
				await harness.Workspace.RequestParseAsync(document, true);
				await harness.WaitAsync(document);
				Assert.Empty(document.LiveDiagnostics);
				Assert.Empty(harness.Findings());
			});
		}

		[Fact]
		public void Only_scripts_are_analysed_not_headers()
		{
			ShellStaRunner.Run(async () =>
			{
				using DiagnosticsHarness harness = new DiagnosticsHarness();
				String path = harness.Write("project/a.h", "procedure helper begin\r\n\tundefined_thing(1);\r\nend\r\n");

				DocumentViewModel document = await harness.OpenAsync(path);

				Assert.Empty(document.LiveDiagnostics);
				Assert.Empty(harness.Findings());
			});
		}

		[Fact]
		public void A_result_for_a_text_that_was_edited_meanwhile_is_dropped()
		{
			ShellStaRunner.Run(async () =>
			{
				using DiagnosticsHarness harness = new DiagnosticsHarness(TimeSpan.FromMilliseconds(150));
				String path = harness.Write("project/a.ssl", WithTypo);
				DocumentViewModel document = await harness.OpenAsync(path);
				Assert.Single(document.LiveDiagnostics);

				harness.Coordinator.Detach(document);
				harness.Diagnostics.Schedule(document);
				document.TextDocument.Insert(0, "// edited\r\n");
				await harness.WaitAsync(document);

				// The result computed for the old text is not published: the findings stay where they were.
				Assert.Single(document.LiveDiagnostics);
				Assert.Equal(2, document.LiveDiagnostics[0].Line);
				Assert.Equal(2, harness.Findings().Single().Line);
			});
		}

		[Fact]
		public void A_newer_request_cancels_the_analysis_that_waits()
		{
			ShellStaRunner.Run(async () =>
			{
				using DiagnosticsHarness harness = new DiagnosticsHarness(TimeSpan.FromMilliseconds(200));
				String path = harness.Write("project/a.ssl", "procedure start begin\r\nend\r\n");
				DocumentViewModel document = await harness.OpenAsync(path);
				Assert.Empty(document.LiveDiagnostics);

				harness.Coordinator.Detach(document);
				document.TextDocument.Text = WithTypo;
				harness.Diagnostics.Schedule(document);
				Task first = harness.Diagnostics.GetPending(document)!;
				document.TextDocument.Text = WithTypo + "procedure two begin\r\n\tother_typo(1);\r\nend\r\n";
				harness.Diagnostics.Schedule(document);
				Task second = harness.Diagnostics.GetPending(document)!;
				Assert.NotSame(first, second);
				await first;
				Assert.Empty(document.LiveDiagnostics);
				await second;

				Assert.Equal(2, document.LiveDiagnostics.Count);
			});
		}

		[Fact]
		public void The_analysis_waits_for_the_pause_after_the_parse()
		{
			ShellStaRunner.Run(async () =>
			{
				using DiagnosticsHarness harness = new DiagnosticsHarness(TimeSpan.FromMilliseconds(120));
				String path = harness.Write("project/a.ssl", WithTypo);
				DocumentViewModel document = await harness.OpenAsync(path);
				document.TextDocument.Text = "procedure start begin\r\n\tdisplay_msg(\"a\");\r\nend\r\n";

				harness.Diagnostics.Schedule(document);

				Assert.NotNull(harness.Diagnostics.GetPending(document));
				Assert.Single(document.LiveDiagnostics);
				await harness.WaitAsync(document);
				Assert.Empty(document.LiveDiagnostics);
			});
		}

		[Fact]
		public void A_finding_the_parser_already_reports_on_the_same_line_is_not_repeated_unless_it_brings_a_fix()
		{
			ShellStaRunner.Run(async () =>
			{
				using DiagnosticsHarness harness = new DiagnosticsHarness();
				String path = harness.Write("project/a.ssl", "procedure start begin\r\n\tdisplay_msgg(\"a\");\r\n\tcall start(1, 2);\r\nend\r\n");
				DocumentViewModel document = await harness.OpenAsync(path);
				Assert.Equal(2, document.LiveDiagnostics.Count);

				document.ParserErrors = new[] { new BuildError(BuildErrorType.Error, "parser says", path, 3, 7), new BuildError(BuildErrorType.Error, "parser says", path, 2, 2) };
				harness.Diagnostics.Schedule(document);
				await harness.WaitAsync(document);

				ScriptDiagnostic only = Assert.Single(document.LiveDiagnostics);
				Assert.Equal("SSL1002", only.Code);
			});
		}

		[Fact]
		public void Closing_the_script_removes_its_findings()
		{
			ShellStaRunner.Run(async () =>
			{
				using DiagnosticsHarness harness = new DiagnosticsHarness();
				String path = harness.Write("project/a.ssl", WithTypo);
				DocumentViewModel document = await harness.OpenAsync(path);
				Assert.Single(harness.Findings());

				await harness.Workspace.CloseAsync(document);

				Assert.Empty(harness.Findings());
				Assert.Empty(document.LiveDiagnostics);
			});
		}

		[Fact]
		public void The_headers_of_the_script_are_followed_so_their_names_are_known()
		{
			ShellStaRunner.Run(async () =>
			{
				using DiagnosticsHarness harness = new DiagnosticsHarness();
				harness.Write("project/lib.h", "variable lib_counter;\r\nprocedure lib_helper(variable a) begin return a; end\r\n#define LIB_LIMIT 5\r\n");
				String path = harness.Write(
					"project/a.ssl",
					"#include \"lib.h\"\r\nprocedure start begin\r\n\tvariable x;\r\n\tx := lib_helper(LIB_LIMIT);\r\n\tlib_counter := x;\r\n\tx := lib_helper(1, 2);\r\n\tx := not_in_any_header;\r\nend\r\n");

				DocumentViewModel document = await harness.OpenAsync(path);

				Assert.Equal(new[] { ("SSL1001", 6), ("SSL1003", 7) }, document.LiveDiagnostics.Select(d => (d.Code, d.Line)));
			});
		}

		[Fact]
		public void A_header_that_cannot_be_found_silences_the_missing_names_but_not_the_structure()
		{
			ShellStaRunner.Run(async () =>
			{
				using DiagnosticsHarness harness = new DiagnosticsHarness();
				String path = harness.Write("project/a.ssl", "#include \"nowhere.h\"\r\nprocedure start begin\r\n\tfrom_the_missing_header(1);\r\nend\r\nend\r\n");

				DocumentViewModel document = await harness.OpenAsync(path);

				ScriptDiagnostic only = Assert.Single(document.LiveDiagnostics);
				Assert.Equal("SSL1011", only.Code);
			});
		}

		[Fact]
		public void A_header_of_another_folder_is_found_through_the_project_index()
		{
			ShellStaRunner.Run(async () =>
			{
				using DiagnosticsHarness harness = new DiagnosticsHarness();
				harness.Write("project/headers/extra.h", "procedure from_extra(variable a);\r\n");
				String path = harness.Write("project/scripts/a.ssl", "#include \"extra.h\"\r\nprocedure start begin\r\n\tcall from_extra(1, 2);\r\nend\r\n");
				harness.Projects.Open(harness.Folder.Combine("project"));
				await harness.Symbols.RefreshAsync();

				DocumentViewModel document = await harness.OpenAsync(path);

				ScriptDiagnostic only = Assert.Single(document.LiveDiagnostics);
				Assert.Equal("SSL1001", only.Code);
			});
		}

		[Fact]
		public void The_unsaved_text_of_an_open_header_is_what_counts()
		{
			ShellStaRunner.Run(async () =>
			{
				using DiagnosticsHarness harness = new DiagnosticsHarness();
				String header = harness.Write("project/lib.h", "procedure on_disk;\r\n");
				String path = harness.Write("project/a.ssl", "#include \"lib.h\"\r\nprocedure start begin\r\n\tcall only_unsaved;\r\nend\r\n");
				DocumentViewModel library = await harness.OpenAsync(header);
				library.TextDocument.Text = "procedure only_unsaved begin end\r\n";
				library.IsModified = true;

				DocumentViewModel document = await harness.OpenAsync(path);

				Assert.Empty(document.LiveDiagnostics);
			});
		}

		[Fact]
		public void The_quick_fix_at_the_caret_renames_the_typo_and_the_script_is_parsed_again()
		{
			ShellStaRunner.Run(async () =>
			{
				using DiagnosticsHarness harness = new DiagnosticsHarness();
				String path = harness.Write("project/a.ssl", WithTypo);
				DocumentViewModel document = await harness.OpenAsync(path);
				Int32 offset = document.TextDocument.Text.IndexOf("display_msgg", StringComparison.Ordinal) + 3;

				Boolean applied = await harness.Workspace.ApplyQuickFixAtAsync(document, offset);
				await harness.WaitAsync(document);

				Assert.True(applied);
				Assert.Equal("procedure start begin\r\n\tdisplay_msg(\"a\");\r\nend\r\n", document.TextDocument.Text);
				Assert.Empty(document.LiveDiagnostics);
				Assert.Contains(harness.Output.Statuses, status => status.StartsWith("Quick fix applied: Did you mean 'display_msg'?", StringComparison.Ordinal));
				document.TextDocument.UndoStack.Undo();
				Assert.Equal(WithTypo, document.TextDocument.Text);
			});
		}

		[Fact]
		public void The_fix_of_a_problem_opens_the_script_and_fixes_the_finding_at_its_place()
		{
			ShellStaRunner.Run(async () =>
			{
				using DiagnosticsHarness harness = new DiagnosticsHarness();
				String path = harness.Write("project/a.ssl", "procedure start begin\r\n\tcall late(1);\r\nend\r\nprocedure late(variable a) begin end\r\n");
				DocumentViewModel opened = await harness.OpenAsync(path);
				Problem problem = Assert.Single(harness.Findings());
				Assert.Equal(ProblemActionKind.ApplyQuickFix, problem.Action!.Kind);
				await harness.Workspace.CloseAsync(opened);

				await harness.Workspace.ApplyQuickFixAsync(problem.Action.FilePath!, problem.Action.Line, problem.Action.Column);

				DocumentViewModel document = harness.Workspace.FindDocument(path)!;
				Assert.StartsWith("procedure late(variable a);\r\nprocedure start begin", document.TextDocument.Text, StringComparison.Ordinal);
				await harness.WaitAsync(document);
				Assert.Empty(document.LiveDiagnostics);
			});
		}

		[Fact]
		public void The_quick_fix_says_so_when_there_is_nothing_to_fix_at_the_place()
		{
			ShellStaRunner.Run(async () =>
			{
				using DiagnosticsHarness harness = new DiagnosticsHarness();
				String path = harness.Write("project/a.ssl", "procedure start begin\r\n\tdisplay_msg(\"a\");\r\nend\r\n");
				DocumentViewModel document = await harness.OpenAsync(path);
				String before = document.TextDocument.Text;

				Boolean applied = await harness.Workspace.ApplyQuickFixAtAsync(document, 5);

				Assert.False(applied);
				Assert.Equal(before, document.TextDocument.Text);
				Assert.Contains("No quick fix is available here.", harness.Output.Statuses);
			});
		}

		[Fact]
		public void A_fix_found_in_a_stale_text_is_not_applied_to_the_new_one()
		{
			ShellStaRunner.Run(async () =>
			{
				using DiagnosticsHarness harness = new DiagnosticsHarness();
				String path = harness.Write("project/a.ssl", WithTypo);
				DocumentViewModel document = await harness.OpenAsync(path);
				Problem problem = Assert.Single(harness.Findings());
				document.TextDocument.Text = "procedure start begin\r\n\tdisplay_msg(\"a\");\r\nend\r\n";

				await harness.Workspace.ApplyQuickFixAsync(path, problem.Line, problem.Column);

				Assert.Equal("procedure start begin\r\n\tdisplay_msg(\"a\");\r\nend\r\n", document.TextDocument.Text);
			});
		}

		[Fact]
		public void The_function_descriptions_are_read_from_the_descriptions_folder_and_follow_a_change_of_the_file()
		{
			ShellStaRunner.Run(async () =>
			{
				using DiagnosticsHarness harness = new DiagnosticsHarness();
				System.IO.Directory.CreateDirectory(harness.Paths.DescriptionsDirectory);
				System.IO.File.WriteAllText(System.IO.Path.Combine(harness.Paths.DescriptionsDirectory, "opcodes.txt"), "void my_engine_call (int a) | - test\r\n");
				String path = harness.Write("project/a.ssl", "procedure start begin\r\n\tmy_engine_call(1, 2);\r\nend\r\n");

				DocumentViewModel document = await harness.OpenAsync(path);

				ScriptDiagnostic first = Assert.Single(document.LiveDiagnostics);
				Assert.Equal("SSL2001", first.Code);

				System.IO.File.WriteAllText(System.IO.Path.Combine(harness.Paths.DescriptionsDirectory, "opcodes.txt"), "void my_engine_call (int a, int b) | - test\r\n");
				await harness.Workspace.RequestParseAsync(document, true);
				await harness.WaitAsync(document);

				Assert.Empty(document.LiveDiagnostics);
			});
		}

		[Fact]
		public void The_default_level_shows_errors_and_warnings()
		{
			Assert.Equal(LiveDiagnosticsLevel.ErrorsAndWarnings, new EditorSettings().LiveDiagnostics);
			ShellStaRunner.Run(() =>
			{
				using DiagnosticsHarness harness = new DiagnosticsHarness();
				Assert.Equal(LiveDiagnosticsLevel.ErrorsAndWarnings, harness.Diagnostics.Level);
				return Task.CompletedTask;
			});
		}

		[Fact]
		public void The_codes_of_the_findings_are_stable_and_documented_by_the_rules()
		{
			ShellStaRunner.Run(async () =>
			{
				using DiagnosticsHarness harness = new DiagnosticsHarness();
				String path = harness.Write("project/a.ssl", "procedure start begin\r\n\tundefined_thing;\r\nend\r\n");

				DocumentViewModel document = await harness.OpenAsync(path);

				List<String> codes = document.LiveDiagnostics.Select(d => d.Code).ToList();
				Assert.All(codes, code => Assert.NotNull(ScriptDiagnosticCodes.Find(code)));
			});
		}
	}
}