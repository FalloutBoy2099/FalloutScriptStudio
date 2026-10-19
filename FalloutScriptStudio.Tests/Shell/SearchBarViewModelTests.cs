namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Tests.Shell.Support;
	using FalloutScriptStudio.ViewModels.Main;
	using Xunit;

	public sealed class SearchBarViewModelTests
	{
		private const String Text = "alpha beta alpha\r\ngamma Alpha\r\n";

		private static async Task<(ShellHarness Harness, DocumentViewModel Document, FakeEditorSurface Editor)> CreateAsync()
		{
			ShellHarness harness = new ShellHarness();
			DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Text);
			FakeEditorSurface editor = harness.AttachEditor(document);
			harness.Search.MatchCase = false;
			return (harness, document, editor);
		}

		[Fact]
		public void Find_next_selects_matches_in_order_and_wraps_around()
		{
			ShellStaRunner.Run(async () =>
			{
				(ShellHarness harness, DocumentViewModel _, FakeEditorSurface editor) = await CreateAsync();
				using ShellHarness _h = harness;
				harness.Search.SearchText = "alpha";

				harness.Search.FindNext();
				Assert.Equal((0, 5), (editor.SelectionStart, editor.SelectionLength));
				harness.Search.FindNext();
				Assert.Equal(11, editor.SelectionStart);
				harness.Search.FindNext();
				Assert.Equal(Text.IndexOf("Alpha", StringComparison.Ordinal), editor.SelectionStart);
				harness.Search.FindNext();
				Assert.Equal(0, editor.SelectionStart);
				Assert.Contains("wrapped", harness.Search.StatusText, StringComparison.OrdinalIgnoreCase);
			});
		}

		[Fact]
		public void Find_previous_walks_backwards_and_wraps()
		{
			ShellStaRunner.Run(async () =>
			{
				(ShellHarness harness, DocumentViewModel _, FakeEditorSurface editor) = await CreateAsync();
				using ShellHarness _h = harness;
				harness.Search.SearchText = "alpha";
				editor.Select(0, 0);

				harness.Search.FindPrevious();

				Assert.Equal(Text.IndexOf("Alpha", StringComparison.Ordinal), editor.SelectionStart);
				harness.Search.FindPrevious();
				Assert.Equal(11, editor.SelectionStart);
			});
		}

		[Fact]
		public void A_missing_text_plays_the_not_found_sound()
		{
			ShellStaRunner.Run(async () =>
			{
				(ShellHarness harness, DocumentViewModel _, FakeEditorSurface _) = await CreateAsync();
				using ShellHarness _h = harness;
				harness.Search.SearchText = "zzz";

				harness.Search.FindNext();

				Assert.Equal(1, harness.Sounds.NotFoundCount);
				Assert.Equal("Not found", harness.Search.StatusText);
			});
		}

		[Fact]
		public void Match_case_and_whole_word_change_the_result_and_are_saved_in_the_settings()
		{
			ShellStaRunner.Run(async () =>
			{
				(ShellHarness harness, DocumentViewModel _, FakeEditorSurface editor) = await CreateAsync();
				using ShellHarness _h = harness;
				harness.Search.SearchText = "Alpha";
				harness.Search.MatchCase = true;
				harness.Search.FindNext();
				Assert.Equal(Text.IndexOf("Alpha", StringComparison.Ordinal), editor.SelectionStart);
				Assert.False(harness.Settings.Settings.Editor.SearchIgnoreCase);

				harness.Search.WholeWord = true;
				Assert.True(harness.Settings.Settings.Editor.SearchWholeWord);
				harness.Search.MatchCase = false;
				Assert.True(harness.Settings.Settings.Editor.SearchIgnoreCase);
			});
		}

		[Fact]
		public void Replace_replaces_the_current_match_and_moves_on()
		{
			ShellStaRunner.Run(async () =>
			{
				(ShellHarness harness, DocumentViewModel document, FakeEditorSurface editor) = await CreateAsync();
				using ShellHarness _h = harness;
				harness.Search.SearchText = "alpha";
				harness.Search.ReplaceText = "OMEGA";
				harness.Search.FindNext();

				harness.Search.Replace();

				Assert.StartsWith("OMEGA beta alpha", document.TextDocument.Text, StringComparison.Ordinal);
				Assert.Equal(11, editor.SelectionStart);
			});
		}

		[Fact]
		public void Replace_without_a_current_match_only_finds_the_next_one()
		{
			ShellStaRunner.Run(async () =>
			{
				(ShellHarness harness, DocumentViewModel document, FakeEditorSurface editor) = await CreateAsync();
				using ShellHarness _h = harness;
				harness.Search.SearchText = "alpha";
				harness.Search.ReplaceText = "X";

				harness.Search.Replace();

				Assert.Equal(Text, document.TextDocument.Text);
				Assert.Equal(0, editor.SelectionStart);
			});
		}

		[Fact]
		public void Replace_all_is_one_undo_step_and_reports_the_count()
		{
			ShellStaRunner.Run(async () =>
			{
				(ShellHarness harness, DocumentViewModel document, FakeEditorSurface _) = await CreateAsync();
				using ShellHarness _h = harness;
				harness.Search.SearchText = "alpha";
				harness.Search.ReplaceText = "z";

				harness.Search.ReplaceAll();

				Assert.Equal("z beta z\r\ngamma z\r\n", document.TextDocument.Text);
				Assert.Equal("3 replaced", harness.Search.StatusText);
				document.TextDocument.UndoStack.Undo();
				Assert.Equal(Text, document.TextDocument.Text);
			});
		}

		[Fact]
		public void Find_all_lists_the_matching_lines_in_a_search_results_tab()
		{
			ShellStaRunner.Run(async () =>
			{
				(ShellHarness harness, DocumentViewModel document, FakeEditorSurface _) = await CreateAsync();
				using ShellHarness _h = harness;
				harness.Search.SearchText = "alpha";

				harness.Search.FindAll();

				ResultsTabViewModel tab = Assert.Single(harness.Output.Tabs.OfType<ResultsTabViewModel>());
				Assert.Equal(2, tab.Results.Count);
				Assert.All(tab.Results, row => Assert.Equal(BuildErrorType.Search, row.Type));
				Assert.Equal(document.FilePath, tab.Results[0].FullPath);
				Assert.Empty(harness.Output.Errors);
			});
		}

		[Fact]
		public void Searches_are_remembered_newest_first_without_duplicates()
		{
			ShellStaRunner.Run(async () =>
			{
				(ShellHarness harness, DocumentViewModel _, FakeEditorSurface _) = await CreateAsync();
				using ShellHarness _h = harness;

				foreach (String query in new[] { "alpha", "beta", "alpha" })
				{
					harness.Search.SearchText = query;
					harness.Search.FindNext();
				}

				Assert.Equal(new[] { "alpha", "beta" }, harness.Search.History);
				Assert.Equal(new[] { "alpha", "beta" }, harness.Settings.SearchHistory.Queries);
			});
		}

		[Fact]
		public void The_matches_are_highlighted_while_the_bar_is_visible_and_cleared_when_it_closes()
		{
			ShellStaRunner.Run(async () =>
			{
				(ShellHarness harness, DocumentViewModel _, FakeEditorSurface editor) = await CreateAsync();
				using ShellHarness _h = harness;

				harness.Search.Show(false);
				harness.Search.SearchText = "alpha";
				Assert.Equal(3, editor.Matches.Count);
				Assert.Equal(11, editor.Matches[1].Offset);
				Assert.Equal(5, editor.Matches[1].Length);

				harness.Search.HideCommand.Execute(null);
				Assert.Empty(editor.Matches);
				Assert.False(harness.Search.IsVisible);
				Assert.Contains("focus", editor.Calls);
			});
		}

		[Fact]
		public void Showing_the_bar_takes_the_word_at_the_caret()
		{
			ShellStaRunner.Run(async () =>
			{
				(ShellHarness harness, DocumentViewModel _, FakeEditorSurface editor) = await CreateAsync();
				using ShellHarness _h = harness;
				editor.WordAtCaret = "gamma";
				Int32 focusRequests = 0;
				harness.Search.FocusRequested += (_, _) => focusRequests++;

				harness.Search.Show(true);

				Assert.Equal("gamma", harness.Search.SearchText);
				Assert.True(harness.Search.IsVisible);
				Assert.Equal(1, focusRequests);
			});
		}

		[Fact]
		public void A_catastrophic_regex_reports_a_slow_pattern_instead_of_freezing()
		{
			ShellStaRunner.Run(async () =>
			{
				ShellHarness harness = new ShellHarness();
				using ShellHarness _h = harness;
				DocumentViewModel document = await harness.OpenFileAsync("slow.ssl", new String('a', 45) + "c\r\n");
				FakeEditorSurface editor = harness.AttachEditor(document);
				harness.Search.UseRegex = true;
				harness.Search.Show(false);

				harness.Search.SearchText = "(a+)+b";
				Assert.Contains("too slow", harness.Search.StatusText, StringComparison.OrdinalIgnoreCase);

				harness.Search.SearchText = "(a+)+";
				Assert.Equal(String.Empty, harness.Search.StatusText);

				harness.Search.SearchText = "(a+)+b";
				harness.Search.FindNext();
				Assert.Contains("too slow", harness.Search.StatusText, StringComparison.OrdinalIgnoreCase);
				Assert.Equal(0, harness.Sounds.NotFoundCount);
				Assert.Equal(0, editor.SelectionLength);
			});
		}

		[Fact]
		public void The_bar_does_not_open_without_a_document()
		{
			using ShellHarness harness = new ShellHarness();

			harness.Search.Show(true);

			Assert.False(harness.Search.IsVisible);
		}

		[Fact]
		public void Closing_the_last_document_hides_the_bar()
		{
			ShellStaRunner.Run(async () =>
			{
				(ShellHarness harness, DocumentViewModel document, FakeEditorSurface _) = await CreateAsync();
				using ShellHarness _h = harness;
				harness.Search.Show(false);

				await harness.Workspace.CloseAsync(document);

				Assert.False(harness.Search.IsVisible);
			});
		}
	}
}