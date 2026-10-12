namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Problems;
	using FalloutScriptStudio.Tests.Shell.Support;
	using FalloutScriptStudio.ViewModels.Main;
	using FalloutScriptStudio.ViewModels.ToolWindows;
	using FalloutScriptStudio.Workspace;
	using Xunit;

	/// <summary>The Errors tab as the one list of problems: groups, filters, counts, copy, next and previous, actions.</summary>
	public sealed class OutputPanelProblemsTests
	{
		private const String Script = "procedure start begin\r\nend\r\n";

		private static Problem Tool(String text, ProblemSeverity severity = ProblemSeverity.Error, ProblemAction? action = null)
		{
			return new Problem { Severity = severity, Source = ProblemSource.Tool, Text = text, Action = action, HintKey = "Str.Shell.Problem.SeeBuildLogHint" };
		}

		private static BuildError Error(BuildErrorType type, String message, String file, Int32 line)
		{
			return new BuildError(type, message, file, line);
		}

		[Fact]
		public void Problems_of_tools_files_and_settings_are_listed_in_their_own_groups_after_the_build_rows()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				document.BuildErrors = new[] { Error(BuildErrorType.Error, "build error", document.FilePath!, 1) };
				harness.Problems.Report(new Problem { Severity = ProblemSeverity.Error, Source = ProblemSource.Settings, Text = "settings" });
				harness.Problems.Report(new Problem { Severity = ProblemSeverity.Warning, Source = ProblemSource.File, Text = "file" });
				harness.Problems.Report(Tool("tool"));
				harness.Problems.Report(new Problem { Severity = ProblemSeverity.Warning, Source = ProblemSource.Message, Text = "message file" });

				Assert.Equal(new[] { "build error", "tool", "file", "settings", "message file" }, harness.Output.Errors.Select(e => e.Message));
				Assert.Equal(new[] { "Build", "Tools", "Files", "Settings", "Message files" }, harness.Output.Errors.Select(e => e.Group));
			});
		}

		[Fact]
		public void A_problem_row_carries_the_hint_and_the_action_of_its_problem()
		{
			ShellStaRunner.Run(() =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Problems.Report(Tool("compile.exe is missing", action: ProblemAction.OpenSettings(SettingsPage.Compiler)));

				ErrorItemViewModel row = Assert.Single(harness.Output.Errors);

				Assert.True(row.HasHint);
				Assert.Equal("Read the full output of the tool in the Build tab.", row.Hint);
				Assert.True(row.HasAction);
				Assert.Equal("Open the settings: Compiler", row.ActionLabel);
				Assert.Equal(ProblemSeverity.Error, row.Severity);
				Assert.Equal(ProblemSource.Tool, row.Source);
				Assert.False(row.CanNavigate);
				return Task.CompletedTask;
			});
		}

		[Fact]
		public void The_counters_count_every_row_whatever_the_filters()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				document.BuildErrors = new[]
				{
					Error(BuildErrorType.Error, "e1", document.FilePath!, 1),
					Error(BuildErrorType.Error, "e2", document.FilePath!, 2),
					Error(BuildErrorType.Warning, "w1", document.FilePath!, 3),
					Error(BuildErrorType.Message, "m1", document.FilePath!, 4),
				};
				harness.Problems.Report(Tool("tool", ProblemSeverity.Warning));

				Assert.Equal(2, harness.Output.ProblemErrorCount);
				Assert.Equal(2, harness.Output.ProblemWarningCount);
				Assert.Equal(1, harness.Output.ProblemInfoCount);

				harness.Output.ShowErrors = false;
				harness.Output.ShowInfos = false;
				harness.Output.ShowBuildEntries = false;

				Assert.Equal(new[] { "tool" }, harness.Output.Errors.Select(e => e.Message));
				Assert.Equal(2, harness.Output.ProblemErrorCount);
				Assert.Equal(2, harness.Output.ProblemWarningCount);
				Assert.Equal(1, harness.Output.ProblemInfoCount);
				Assert.Equal(1, harness.Output.ErrorCount);
			});
		}

		[Fact]
		public void Each_source_can_be_hidden_on_its_own()
		{
			ShellStaRunner.Run(() =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Problems.Report(Tool("tool"));
				harness.Problems.Report(new Problem { Severity = ProblemSeverity.Error, Source = ProblemSource.File, Text = "file" });
				harness.Problems.Report(new Problem { Severity = ProblemSeverity.Error, Source = ProblemSource.Settings, Text = "settings" });
				harness.Problems.Report(new Problem { Severity = ProblemSeverity.Error, Source = ProblemSource.Message, Text = "message" });

				harness.Output.ShowToolEntries = false;
				Assert.Equal(new[] { "file", "settings", "message" }, harness.Output.Errors.Select(e => e.Message));
				harness.Output.ShowFileEntries = false;
				harness.Output.ShowSettingsEntries = false;
				Assert.Equal(new[] { "message" }, harness.Output.Errors.Select(e => e.Message));
				harness.Output.ShowMessageEntries = false;
				Assert.Empty(harness.Output.Errors);
				harness.Output.ShowToolEntries = true;
				Assert.Equal(new[] { "tool" }, harness.Output.Errors.Select(e => e.Message));
				return Task.CompletedTask;
			});
		}

		[Fact]
		public void Next_and_previous_walk_the_errors_and_warnings_and_wrap_around()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				FakeEditorSurface editor = harness.AttachEditor(document);
				document.BuildErrors = new[]
				{
					Error(BuildErrorType.Error, "first", document.FilePath!, 2),
					Error(BuildErrorType.Warning, "second", document.FilePath!, 5),
					Error(BuildErrorType.Message, "note", document.FilePath!, 1),
				};

				await harness.Output.NextProblemCommand.ExecuteAsync(null);
				Assert.Equal("first", harness.Output.SelectedError!.Message);
				Assert.Equal((2, -1, -1), editor.LastGoTo);

				await harness.Output.NextProblemCommand.ExecuteAsync(null);
				Assert.Equal("second", harness.Output.SelectedError!.Message);
				Assert.Equal((5, -1, -1), editor.LastGoTo);

				await harness.Output.NextProblemCommand.ExecuteAsync(null);
				Assert.Equal("first", harness.Output.SelectedError!.Message);

				await harness.Output.PreviousProblemCommand.ExecuteAsync(null);
				Assert.Equal("second", harness.Output.SelectedError!.Message);
				Assert.Equal((Int32)OutputTab.Errors, harness.Output.SelectedTab);
			});
		}

		[Fact]
		public void Previous_from_nothing_selects_the_last_problem()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				document.BuildErrors = new[]
				{
					Error(BuildErrorType.Error, "first", document.FilePath!, 1),
					Error(BuildErrorType.Error, "last", document.FilePath!, 2),
				};

				await harness.Output.PreviousProblemCommand.ExecuteAsync(null);

				Assert.Equal("last", harness.Output.SelectedError!.Message);
			});
		}

		[Fact]
		public void Next_without_any_problem_says_so_in_the_status_bar()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();

				await harness.Output.NextProblemCommand.ExecuteAsync(null);

				Assert.Equal("No problems.", harness.Output.StatusMessage);
				Assert.Null(harness.Output.SelectedError);
			});
		}

		[Fact]
		public void Next_opens_the_file_of_the_problem_and_keeps_the_list_of_the_other_files()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel a = await harness.OpenFileAsync("a.ssl", Script);
				String other = harness.Folder.Write("other.ssl", Script);
				harness.Output.ShowBuildErrors(new[]
				{
					Error(BuildErrorType.Error, "in a", a.FilePath!, 1),
					Error(BuildErrorType.Error, "in other", other, 2),
				});
				await harness.Output.NextProblemCommand.ExecuteAsync(null);

				await harness.Output.NextProblemCommand.ExecuteAsync(null);

				DocumentViewModel opened = Assert.IsType<DocumentViewModel>(harness.Workspace.FindDocument(other));
				Assert.Same(opened, harness.Workspace.ActiveDocument);
				opened.BuildErrors = new[] { Error(BuildErrorType.Error, "own error of other", other, 1) };
				Assert.Equal(new[] { "in a", "in other" }, harness.Output.Errors.Select(e => e.Message));
				Assert.Equal("in other", harness.Output.SelectedError!.Message);
			});
		}

		[Fact]
		public void Going_to_a_problem_without_a_position_does_nothing()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Problems.Report(Tool("no position"));

				await harness.Output.GoToErrorCommand.ExecuteAsync(harness.Output.Errors[0]);

				Assert.Empty(harness.Workspace.Documents);
			});
		}

		[Fact]
		public void Copy_selected_adds_the_hint_and_copy_all_lists_every_row_with_its_place()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				document.BuildErrors = new[] { new BuildError(BuildErrorType.Error, "undeclared variable", document.FilePath!, 7, 3) };
				harness.Problems.Report(Tool("compile.exe is missing"));

				harness.Output.SelectedError = harness.Output.Errors[1];
				harness.Output.CopySelectedCommand.Execute(null);
				Assert.Equal("compile.exe is missing\r\nRead the full output of the tool in the Build tab.", harness.Shell.Clipboard);

				harness.Output.CopyAllCommand.Execute(null);
				String[] lines = harness.Shell.Clipboard.Split("\r\n");
				Assert.Equal(document.FilePath + "(7,3): Error: undeclared variable", lines[0]);
				Assert.Equal("Error: compile.exe is missing", lines[1]);
				Assert.Equal("    Read the full output of the tool in the Build tab.", lines[2]);
			});
		}

		[Fact]
		public void Copy_all_with_an_empty_list_leaves_the_clipboard_alone()
		{
			ShellStaRunner.Run(() =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Shell.Clipboard = "keep me";

				harness.Output.CopyAllCommand.Execute(null);

				Assert.Equal("keep me", harness.Shell.Clipboard);
				return Task.CompletedTask;
			});
		}

		[Fact]
		public void Clear_all_removes_the_problems_too()
		{
			ShellStaRunner.Run(() =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Problems.Report(Tool("a"));

				harness.Output.ClearAllCommand.Execute(null);

				Assert.Empty(harness.Problems.Problems);
				Assert.Empty(harness.Output.Errors);
				Assert.Equal(0, harness.Output.ProblemErrorCount);
				return Task.CompletedTask;
			});
		}

		[Fact]
		public void Clearing_the_errors_before_a_build_keeps_the_problems_of_tools_and_files()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				document.BuildErrors = new[] { Error(BuildErrorType.Error, "stale", document.FilePath!, 1) };
				harness.Problems.Report(Tool("still there"));

				harness.Output.ClearErrors();

				Assert.Equal(new[] { "still there" }, harness.Output.Errors.Select(e => e.Message));
				document.BuildErrors = new[] { Error(BuildErrorType.Error, "new", document.FilePath!, 2) };
				Assert.Equal(new[] { "new", "still there" }, harness.Output.Errors.Select(e => e.Message));
			});
		}

		[Fact]
		public void The_selection_survives_a_rebuild_of_the_list()
		{
			ShellStaRunner.Run(() =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Problems.Report(Tool("first"));
				harness.Problems.Report(Tool("second"));
				harness.Output.SelectedError = harness.Output.Errors[1];

				harness.Problems.Report(Tool("third"));

				Assert.Equal("second", harness.Output.SelectedError!.Message);
				return Task.CompletedTask;
			});
		}

		[Fact]
		public void An_action_that_opens_the_settings_uses_the_handler_of_the_main_window_and_shows_the_page()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Problems.Report(Tool("compile.exe is missing", action: ProblemAction.OpenSettings(SettingsPage.Compiler)));
				List<SettingsPage> pages = new List<SettingsPage>();
				harness.Output.OpenSettingsHandler = page =>
				{
					pages.Add(page);
					return Task.CompletedTask;
				};

				await harness.Output.RunProblemActionCommand.ExecuteAsync(harness.Output.Errors[0]);

				Assert.Equal(new[] { SettingsPage.Compiler }, pages);
			});
		}

		[Fact]
		public void The_main_window_opens_the_settings_page_of_a_problem_and_parses_again()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				Int32 parsesBefore = harness.Parser.Started;
				harness.Problems.Report(Tool("parser.dll", action: ProblemAction.OpenSettings(SettingsPage.Parser)));

				await harness.Output.RunProblemActionCommand.ExecuteAsync(harness.Output.Errors[0]);

				Assert.Equal(new[] { SettingsPage.Parser }, harness.ToolWindows.SettingsPages);
				Assert.True(await Eventually.TrueAsync(() => harness.Parser.Started > parsesBefore));
				Assert.NotNull(document);
			});
		}

		[Fact]
		public void An_action_that_opens_a_folder_asks_the_shell()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Problems.Report(new Problem { Severity = ProblemSeverity.Error, Source = ProblemSource.Settings, Text = "damaged", Action = ProblemAction.OpenFolder(@"C:\app\settings") });

				await harness.Output.RunProblemActionCommand.ExecuteAsync(harness.Output.Errors[0]);

				Assert.Equal(new[] { @"C:\app\settings" }, harness.Shell.Folders);
			});
		}

		[Fact]
		public void A_row_without_action_does_nothing_when_its_action_is_run()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Problems.Report(Tool("no action"));

				await harness.Output.RunProblemActionCommand.ExecuteAsync(harness.Output.Errors[0]);

				Assert.Empty(harness.ToolWindows.SettingsPages);
				Assert.Empty(harness.Shell.Folders);
			});
		}

		[Fact]
		public void A_problem_reported_from_a_background_thread_reaches_the_list_on_the_ui_thread()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();

				await Task.Run(() => harness.Problems.Report(Tool("from a worker")));

				Assert.True(await Eventually.TrueAsync(() => harness.Output.Errors.Count == 1));
				Assert.Equal("from a worker", harness.Output.Errors[0].Message);
			});
		}

		[Fact]
		public void A_damaged_settings_file_becomes_a_problem_with_an_action_that_opens_its_folder()
		{
			ShellStaRunner.Run(() =>
			{
				using ShellHarness harness = new ShellHarness();
				File.WriteAllText(harness.Paths.SettingsFile, "{ not json");

				harness.Settings.Load();

				ErrorItemViewModel row = Assert.Single(harness.Output.Errors, item => item.Source == ProblemSource.Settings);
				Assert.Equal(ProblemSeverity.Warning, row.Severity);
				Assert.Contains("settings.json", row.Message, StringComparison.Ordinal);
				Assert.Equal(ProblemActionKind.OpenFolder, row.Action!.Kind);
				return Task.CompletedTask;
			});
		}

		[Fact]
		public void Search_results_tabs_are_not_problems()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);

				harness.Output.ShowSearchResults(new[] { Error(BuildErrorType.Search, "found", document.FilePath!, 1) }, "Str.Shell.Output.SearchResults");

				Assert.Empty(harness.Output.Errors);
				Assert.Equal(0, harness.Output.ProblemErrorCount + harness.Output.ProblemWarningCount + harness.Output.ProblemInfoCount);
			});
		}
	}
}
