namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Tests.Shell.Support;
	using FalloutScriptStudio.ViewModels.Main;
	using FalloutScriptStudio.Workspace;
	using Xunit;

	public sealed class OutputPanelViewModelTests
	{
		private const String Script = "procedure start begin\r\nend\r\n";

		private static BuildError Error(BuildErrorType type, String message, String file, Int32 line)
		{
			return new BuildError(type, message, file, line);
		}

		[Fact]
		public void The_panel_shows_the_logs_and_errors_of_the_active_document()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);

				document.ParserLog = "parser text";
				document.BuildLog = "build text";
				document.ParserErrors = new[] { Error(BuildErrorType.Warning, "parser warning", document.FilePath!, 1) };
				document.BuildErrors = new[]
				{
					Error(BuildErrorType.Message, "note", document.FilePath!, 3),
					Error(BuildErrorType.Error, "boom", document.FilePath!, 2),
				};

				Assert.Equal("parser text", harness.Output.ParserText);
				Assert.Equal("build text", harness.Output.BuildText);
				Assert.Equal(new[] { "parser warning", "boom", "note" }, harness.Output.Errors.Select(e => e.Message));
				Assert.Equal(new[] { "Parser", "Build", "Build" }, harness.Output.Errors.Select(e => e.Group));
				Assert.Equal(3, harness.Output.ErrorCount);
			});
		}

		[Fact]
		public void Switching_tabs_swaps_the_content_when_auto_refresh_is_on()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel a = await harness.OpenFileAsync("a.ssl", Script);
				DocumentViewModel b = await harness.OpenFileAsync("b.ssl", Script);
				a.BuildErrors = new[] { Error(BuildErrorType.Error, "in a", a.FilePath!, 1) };
				b.BuildErrors = new[] { Error(BuildErrorType.Error, "in b", b.FilePath!, 1) };

				harness.Workspace.ActiveDocument = a;
				Assert.Equal("in a", Assert.Single(harness.Output.Errors).Message);
				harness.Workspace.ActiveDocument = b;
				Assert.Equal("in b", Assert.Single(harness.Output.Errors).Message);
			});
		}

		[Fact]
		public void Search_results_open_their_own_tab_and_leave_the_errors_tab_alone()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				document.BuildErrors = new[] { Error(BuildErrorType.Error, "boom", document.FilePath!, 1) };
				Int32 shown = 0;
				harness.Output.ShowRequested += (_, _) => shown++;

				harness.Output.ShowSearchResults(new[] { Error(BuildErrorType.Search, "found it", document.FilePath!, 7) }, "Str.Shell.Output.SearchResults");
				await harness.Workspace.RequestParseAsync(document, true);

				ResultsTabViewModel tab = Assert.IsType<ResultsTabViewModel>(harness.Output.Tabs[(Int32)OutputTab.Errors + 1]);
				Assert.Equal("found it", Assert.Single(tab.Results).Message);
				Assert.Equal("Search results", tab.Title);
				Assert.Equal("boom", Assert.Single(harness.Output.Errors).Message);
				Assert.Equal(harness.Output.Tabs.Count - 1, harness.Output.SelectedTab);
				Assert.Equal(1, shown);
			});
		}

		[Fact]
		public void Every_search_and_every_references_list_gets_its_own_tab()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);

				harness.Output.ShowSearchResults(new[] { Error(BuildErrorType.Search, "first", document.FilePath!, 1) }, "Str.Shell.Output.SearchResults");
				harness.Output.ShowSearchResults(new[] { Error(BuildErrorType.Search, "ref", document.FilePath!, 2) }, "Str.Shell.Output.References", "start");

				Assert.Equal(5, harness.Output.Tabs.Count);
				Assert.Equal(new[] { "Search results", "'start' references" }, harness.Output.Tabs.OfType<ResultsTabViewModel>().Select(t => t.Title));
				Assert.Equal(4, harness.Output.SelectedTab);
			});
		}

		[Fact]
		public void Results_tabs_survive_refresh_clear_and_the_next_build()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				harness.Output.ShowSearchResults(new[] { Error(BuildErrorType.Search, "kept", document.FilePath!, 1) }, "Str.Shell.Output.SearchResults");

				harness.Output.RefreshCommand.Execute(null);
				harness.Output.ClearAllCommand.Execute(null);
				harness.Output.ClearErrors();
				await harness.Build.CompileAsync(document);

				ResultsTabViewModel tab = Assert.Single(harness.Output.Tabs.OfType<ResultsTabViewModel>());
				Assert.Equal("kept", Assert.Single(tab.Results).Message);
			});
		}

		[Fact]
		public void Closing_a_results_tab_selects_the_tab_before_it()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				harness.Output.ShowSearchResults(new[] { Error(BuildErrorType.Search, "one", document.FilePath!, 1) }, "Str.Shell.Output.SearchResults");
				harness.Output.ShowSearchResults(new[] { Error(BuildErrorType.Search, "two", document.FilePath!, 2) }, "Str.Shell.Output.SearchResults");
				ResultsTabViewModel second = (ResultsTabViewModel)harness.Output.Tabs[4];

				second.CloseCommand.Execute(null);

				Assert.Equal(4, harness.Output.Tabs.Count);
				Assert.Equal(3, harness.Output.SelectedTab);
			});
		}

		[Fact]
		public void Closing_a_tab_before_the_selected_one_keeps_the_selection_on_the_same_tab()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				harness.Output.ShowSearchResults(new[] { Error(BuildErrorType.Search, "one", document.FilePath!, 1) }, "Str.Shell.Output.SearchResults");
				harness.Output.ShowSearchResults(new[] { Error(BuildErrorType.Search, "two", document.FilePath!, 2) }, "Str.Shell.Output.SearchResults");
				ResultsTabViewModel first = (ResultsTabViewModel)harness.Output.Tabs[3];
				ResultsTabViewModel second = (ResultsTabViewModel)harness.Output.Tabs[4];

				first.CloseCommand.Execute(null);

				Assert.Same(second, harness.Output.Tabs[harness.Output.SelectedTab]);
			});
		}

		[Fact]
		public void The_permanent_tabs_cannot_be_closed_and_keep_their_indexes()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				await harness.OpenFileAsync("a.ssl", Script);

				Assert.IsType<ParserLogTabViewModel>(harness.Output.Tabs[(Int32)OutputTab.Parser]);
				Assert.IsType<BuildLogTabViewModel>(harness.Output.Tabs[(Int32)OutputTab.Build]);
				Assert.IsType<ErrorsTabViewModel>(harness.Output.Tabs[(Int32)OutputTab.Errors]);
				Assert.Equal(OutputTab.Errors, harness.Output.Tabs[(Int32)OutputTab.Errors].Kind);
				Assert.Null(new ResultsTabViewModel(harness.Output, "Str.Shell.Output.SearchResults", null, Array.Empty<BuildError>()).Kind);
			});
		}

		[Fact]
		public void The_selected_result_of_a_results_tab_is_copied_to_the_clipboard()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				harness.Output.ShowSearchResults(new[] { Error(BuildErrorType.Search, "copy this", document.FilePath!, 3) }, "Str.Shell.Output.SearchResults");
				ResultsTabViewModel tab = (ResultsTabViewModel)harness.Output.Tabs[3];
				tab.SelectedResult = tab.Results[0];

				tab.CopySelectedCommand.Execute(null);

				Assert.Equal("copy this", harness.Shell.Clipboard);
			});
		}

		[Fact]
		public void Build_errors_of_several_scripts_replace_the_errors_tab_until_the_next_refresh()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel a = await harness.OpenFileAsync("a.ssl", Script);
				DocumentViewModel b = await harness.OpenFileAsync("b.ssl", Script);
				b.BuildErrors = new[] { Error(BuildErrorType.Error, "only b", b.FilePath!, 1) };

				harness.Output.ShowBuildErrors(new[]
				{
					Error(BuildErrorType.Error, "in a", a.FilePath!, 1),
					Error(BuildErrorType.Warning, "in b", b.FilePath!, 2),
				});
				b.BuildErrors = new[] { Error(BuildErrorType.Error, "late b", b.FilePath!, 1) };

				Assert.Equal(new[] { "in a", "in b" }, harness.Output.Errors.Select(e => e.Message));
				Assert.All(harness.Output.Errors, e => Assert.Equal("Build", e.Group));

				harness.Output.RefreshCommand.Execute(null);
				Assert.Equal("late b", Assert.Single(harness.Output.Errors).Message);
			});
		}

		[Fact]
		public void Selecting_another_script_replaces_the_aggregated_errors_with_its_own()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel a = await harness.OpenFileAsync("a.ssl", Script);
				DocumentViewModel b = await harness.OpenFileAsync("b.ssl", Script);
				a.BuildErrors = new[] { Error(BuildErrorType.Error, "own a", a.FilePath!, 1) };
				harness.Output.ShowBuildErrors(new[] { Error(BuildErrorType.Error, "x", a.FilePath!, 1), Error(BuildErrorType.Error, "y", b.FilePath!, 1) });

				harness.Workspace.ActiveDocument = a;

				Assert.Equal("own a", Assert.Single(harness.Output.Errors).Message);
			});
		}

		[Fact]
		public void Clearing_errors_before_a_build_lets_the_new_build_results_appear()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				document.BuildErrors = new[] { Error(BuildErrorType.Error, "stale", document.FilePath!, 1) };

				harness.Output.ClearErrors();
				document.BuildErrors = new[] { Error(BuildErrorType.Error, "new", document.FilePath!, 2) };

				Assert.Equal("new", Assert.Single(harness.Output.Errors).Message);
			});
		}

		[Fact]
		public void The_entry_filters_hide_parser_or_build_rows()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				document.ParserErrors = new[] { Error(BuildErrorType.Error, "p", document.FilePath!, 1) };
				document.BuildErrors = new[] { Error(BuildErrorType.Error, "b", document.FilePath!, 1) };

				harness.Output.ShowParserEntries = false;
				Assert.Equal(new[] { "b" }, harness.Output.Errors.Select(e => e.Message));
				harness.Output.ShowParserEntries = true;
				harness.Output.ShowBuildEntries = false;
				Assert.Equal(new[] { "p" }, harness.Output.Errors.Select(e => e.Message));
			});
		}

		[Fact]
		public void Clear_all_empties_the_list_and_the_document_errors()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				document.BuildErrors = new[] { Error(BuildErrorType.Error, "b", document.FilePath!, 1) };

				harness.Output.ClearAllCommand.Execute(null);

				Assert.Empty(harness.Output.Errors);
				Assert.Empty(document.BuildErrors);
				Assert.Empty(document.ParserErrors);
			});
		}

		[Fact]
		public void The_selected_message_is_copied_to_the_clipboard()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				document.BuildErrors = new[] { Error(BuildErrorType.Error, "copy me", document.FilePath!, 1) };
				harness.Output.SelectedError = harness.Output.Errors[0];

				harness.Output.CopySelectedCommand.Execute(null);

				Assert.Equal("copy me", harness.Shell.Clipboard);
			});
		}

		[Fact]
		public void Going_to_an_error_navigates_to_its_file_and_line()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", String.Join("\r\n", Enumerable.Range(1, 20).Select(i => "// " + i)));
				FakeEditorSurface editor = harness.AttachEditor(document);
				document.BuildErrors = new[] { new BuildError(BuildErrorType.Error, "e", document.FilePath!, 9, 3, 2) };

				await harness.Output.GoToErrorAsync(harness.Output.Errors[0]);

				Assert.Equal((9, 3, 2), editor.LastGoTo);
			});
		}

		[Fact]
		public void Entries_without_a_line_cannot_be_navigated()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				FakeEditorSurface editor = harness.AttachEditor(document);
				document.BuildErrors = new[] { new BuildError(BuildErrorType.Message, "just text", BuildError.NoFile, -1) };

				await harness.Output.GoToErrorAsync(harness.Output.Errors[0]);

				Assert.Null(editor.LastGoTo);
				Assert.Equal(String.Empty, harness.Output.Errors[0].FileName);
				Assert.False(harness.Output.Errors[0].CanNavigate);
			});
		}

		[Fact]
		public void Build_lines_can_be_appended_and_replaced()
		{
			using ShellHarness harness = new ShellHarness();

			harness.Output.AppendBuildLine("one");
			harness.Output.AppendBuildLine("two");
			Assert.Equal("one\r\ntwo", harness.Output.BuildText);
			harness.Output.SetBuildText(String.Empty);
			Assert.Equal(String.Empty, harness.Output.BuildText);
		}

		[Fact]
		public void Showing_a_tab_requests_the_panel()
		{
			using ShellHarness harness = new ShellHarness();
			Int32 shown = 0;
			harness.Output.ShowRequested += (_, _) => shown++;

			harness.Output.ShowTab(OutputTab.Build);

			Assert.Equal((Int32)OutputTab.Build, harness.Output.SelectedTab);
			Assert.Equal(1, shown);
		}
	}
}
