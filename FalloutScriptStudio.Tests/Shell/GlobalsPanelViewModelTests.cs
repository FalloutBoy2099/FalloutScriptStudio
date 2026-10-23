namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.ComponentModel;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Globals;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Problems;
	using FalloutScriptStudio.Tests.Localization;
	using FalloutScriptStudio.Tests.Shell.Support;
	using FalloutScriptStudio.ViewModels.Main;
	using FalloutScriptStudio.ViewModels.Panels;
	using Xunit;

	[Collection(LanguageCollection.Name)]
	public sealed class GlobalsPanelViewModelTests
	{
		private const String Header = "#define GVAR_QUEST (27)\r\n";

		private static GlobalVariableIndex Index(String root)
		{
			return GlobalVariableIndex.FromTexts(
			[
				new(Path.Combine(root, "h.h"), Header),
				new(Path.Combine(root, "a.ssl"), "procedure start begin\r\n\tset_global_var(GVAR_QUEST, 1);\r\n\tvariable v := global_var(27);\r\n\tset_sfall_global(\"abcdefgh\", 3);\r\n\tset_local_var(0, 1);\r\nend\r\n"),
				new(Path.Combine(root, "b.ssl"), "procedure start begin\r\n\tvariable v := global_var(GVAR_QUEST);\r\n\tv := get_sfall_global_int(\"abcdefgh\");\r\n\tv := global_var(GVAR_NEVER);\r\nend\r\n"),
			]);
		}

		private static void Publish(ShellHarness harness)
		{
			harness.SymbolIndex.GlobalVariables = Index(harness.Folder.Path);
		}

		[Fact]
		public void Without_any_variable_the_panel_is_empty()
		{
			using ShellHarness harness = new ShellHarness();

			Assert.False(harness.Globals.HasData);
			Assert.Empty(harness.Globals.Groups);
		}

		[Fact]
		public void The_groups_are_the_families_that_have_variables_in_a_fixed_order()
		{
			using ShellHarness harness = new ShellHarness();

			harness.Globals.Rebuild(Index(harness.Folder.Path), Array.Empty<GlobalFinding>());

			Assert.True(harness.Globals.HasData);
			Assert.Equal(new[] { "globals:SfallGlobal", "globals:GameVariable", "globals:LocalVariable" }, harness.Globals.Groups.Select(g => g.Key).ToArray());
			Assert.Equal(Loc.Format("Str.Panels.GlobalsGroupGame", 2), harness.Globals.Groups[1].Title);
			Assert.Equal(new[] { "GVAR_NEVER", "GVAR_QUEST (27)" }, harness.Globals.Groups[1].Children.Select(c => c.Title).ToArray());
		}

		[Fact]
		public void The_counts_are_shown_in_the_summary()
		{
			using ShellHarness harness = new ShellHarness();

			harness.Globals.Rebuild(Index(harness.Folder.Path), Array.Empty<GlobalFinding>());

			Assert.Equal(Loc.Format("Str.Panels.GlobalsSummary", 4, 7, 2), harness.Globals.SummaryText);
		}

		[Fact]
		public void The_uses_of_a_variable_are_created_when_it_is_expanded()
		{
			using ShellHarness harness = new ShellHarness();
			harness.Globals.Rebuild(Index(harness.Folder.Path), Array.Empty<GlobalFinding>());
			GlobalVariableNodeViewModel node = harness.Globals.Groups[1].Children.OfType<GlobalVariableNodeViewModel>().Single(n => n.Variable.Key == "27");

			Assert.Single(node.Children);
			Assert.False(node.Children[0].IsSymbol);

			node.IsExpanded = true;

			Assert.Equal(3, node.Children.Count);
			GlobalUseNodeViewModel first = Assert.IsType<GlobalUseNodeViewModel>(node.Children[0]);
			Assert.Equal("a.ssl:2", first.Title);
			Assert.Equal("start", first.Procedure);
			Assert.True(first.Writes);
			Assert.False(first.Reads);
			Assert.Equal(Loc.Get("Str.Panels.GlobalAccessWrite") + " = 1", first.AccessText);
			Assert.True(first.IsSymbol);
		}

		[Fact]
		public void The_filter_matches_names_numbers_and_file_names()
		{
			using ShellHarness harness = new ShellHarness();
			harness.Globals.Rebuild(Index(harness.Folder.Path), Array.Empty<GlobalFinding>());
			IEnumerable<GlobalVariableNodeViewModel> Visible() => harness.Globals.Groups.SelectMany(g => g.Children).OfType<GlobalVariableNodeViewModel>().Where(n => n.IsFilterVisible);

			harness.Globals.FilterText = "never";
			Assert.Equal(new[] { "GVAR_NEVER" }, Visible().Select(n => n.Title).ToArray());

			harness.Globals.FilterText = "27";
			Assert.Equal(new[] { "GVAR_QUEST (27)" }, Visible().Select(n => n.Title).ToArray());

			harness.Globals.FilterText = "b.ssl";
			Assert.Equal(3, Visible().Count());

			harness.Globals.FilterText = "nothing like this";
			Assert.Empty(Visible());

			harness.Globals.FilterText = String.Empty;
			Assert.Equal(4, Visible().Count());
		}

		[Fact]
		public void Double_clicking_a_use_goes_to_its_line()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", "procedure start begin\r\n\tset_global_var(5, 1);\r\n\tvariable v := global_var(5);\r\nend\r\n");
				FakeEditorSurface editor = harness.AttachEditor(document);
				harness.Globals.Rebuild(GlobalVariableIndex.FromTexts([new(document.FilePath!, document.TextDocument.Text)]), Array.Empty<GlobalFinding>());
				GlobalVariableNodeViewModel node = harness.Globals.Groups[0].Children.OfType<GlobalVariableNodeViewModel>().Single();
				node.IsExpanded = true;

				await harness.Globals.NavigateAsync(node.Children[1]);

				Assert.Equal(3, editor.LastGoTo?.Line);
			});
		}

		[Fact]
		public void Navigating_from_a_variable_goes_to_its_first_use_and_groups_do_nothing()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", "\r\n\r\nprocedure start begin\r\n\tset_global_var(5, 1);\r\nend\r\n");
				FakeEditorSurface editor = harness.AttachEditor(document);
				harness.Globals.Rebuild(GlobalVariableIndex.FromTexts([new(document.FilePath!, document.TextDocument.Text)]), Array.Empty<GlobalFinding>());

				await harness.Globals.NavigateAsync(harness.Globals.Groups[0]);
				Assert.Null(editor.LastGoTo);

				await harness.Globals.NavigateAsync(harness.Globals.Groups[0].Children[0]);
				Assert.Equal(4, editor.LastGoTo?.Line);
			});
		}

		[Fact]
		public void Find_all_lists_every_use_whatever_node_is_chosen()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", "procedure start begin\r\n\tset_global_var(5, 1);\r\n\tvariable v := global_var(5);\r\nend\r\n");
				harness.Globals.Rebuild(GlobalVariableIndex.FromTexts([new(document.FilePath!, document.TextDocument.Text)]), Array.Empty<GlobalFinding>());
				GlobalVariableNodeViewModel node = harness.Globals.Groups[0].Children.OfType<GlobalVariableNodeViewModel>().Single();
				node.IsExpanded = true;

				await harness.Globals.FindAllAsync(node.Children[0]);

				ResultsTabViewModel tab = Assert.Single(harness.Output.Tabs.OfType<ResultsTabViewModel>());
				Assert.Equal(2, tab.Results.Count);
			});
		}

		[Fact]
		public void Copying_a_name_puts_the_macro_or_the_name_on_the_clipboard()
		{
			using ShellHarness harness = new ShellHarness();
			harness.Globals.Rebuild(Index(harness.Folder.Path), Array.Empty<GlobalFinding>());

			harness.Globals.CopyName(harness.Globals.Groups[1].Children.First(n => n.Title.StartsWith("GVAR_QUEST", StringComparison.Ordinal)));
			Assert.Equal("GVAR_QUEST", harness.Shell.Clipboard);

			harness.Globals.CopyName(harness.Globals.Groups[0].Children[0]);
			Assert.Equal("abcdefgh", harness.Shell.Clipboard);

			harness.Shell.Clipboard = "unchanged";
			harness.Globals.CopyName(harness.Globals.Groups[0]);
			Assert.Equal("unchanged", harness.Shell.Clipboard);
		}

		[Fact]
		public void The_findings_are_badges_on_the_variables_they_belong_to()
		{
			using ShellHarness harness = new ShellHarness();
			GlobalVariableIndex index = Index(harness.Folder.Path);
			IReadOnlyList<GlobalFinding> findings = GlobalVariableAnalyzer.Analyze(index);

			harness.Globals.Rebuild(index, findings);

			GlobalVariableNodeViewModel[] nodes = harness.Globals.Groups.SelectMany(g => g.Children).OfType<GlobalVariableNodeViewModel>().ToArray();
			GlobalVariableNodeViewModel never = nodes.Single(n => n.Title == "GVAR_NEVER");
			Assert.Equal(GlobalNodeSeverity.Warning, never.Severity);
			Assert.Contains(ProblemFactory.GlobalVariable(never.Findings[0]).Message, never.FindingText, StringComparison.Ordinal);
			Assert.Equal(GlobalNodeSeverity.None, nodes.Single(n => n.Title == "GVAR_QUEST (27)").Severity);
			Assert.Equal(findings.Count, harness.Globals.FindingCount);
		}

		[Fact]
		public void Checking_lists_the_findings_in_the_problems_and_reports_the_count()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				Publish(harness);

				await harness.Globals.CheckAsync();

				Problem[] problems = harness.Problems.Problems.Where(p => p.Origin == ProblemOrigins.GlobalVariables).ToArray();
				Assert.NotEmpty(problems);
				Assert.All(problems, p => Assert.NotNull(p.File));
				Assert.Contains(problems, p => p.Severity == ProblemSeverity.Warning && p.MessageKey == "Str.Shell.Problem.GlobalReadNeverWritten");
				Assert.Equal(problems.Length, harness.Globals.FindingCount);

				harness.SymbolIndex.GlobalVariables = GlobalVariableIndex.Empty;
				await harness.Globals.CheckAsync();
				Assert.DoesNotContain(harness.Problems.Problems, p => p.Origin == ProblemOrigins.GlobalVariables);
			});
		}

		[Fact]
		public void Ignoring_a_variable_adds_it_to_the_settings_once_and_removes_its_findings()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				Publish(harness);
				await harness.Globals.RefreshAsync();
				GlobalVariableNodeViewModel never = harness.Globals.Groups.SelectMany(g => g.Children).OfType<GlobalVariableNodeViewModel>().Single(n => n.Title == "GVAR_NEVER");
				Assert.Equal(GlobalNodeSeverity.Warning, never.Severity);

				await harness.Globals.IgnoreAsync(never);
				await harness.Globals.IgnoreAsync(never);

				Assert.Equal(new[] { "GVAR_NEVER" }, harness.Settings.Settings.Editor.GlobalVariableIgnore.ToArray());
				GlobalVariableNodeViewModel again = harness.Globals.Groups.SelectMany(g => g.Children).OfType<GlobalVariableNodeViewModel>().Single(n => n.Title == "GVAR_NEVER");
				Assert.Equal(GlobalNodeSeverity.None, again.Severity);
			});
		}

		[Fact]
		public void Exporting_writes_the_variables_that_pass_the_filter_as_csv()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Globals.Rebuild(Index(harness.Folder.Path), Array.Empty<GlobalFinding>());
				String path = harness.Folder.Combine("export", "globals.csv");
				Directory.CreateDirectory(Path.GetDirectoryName(path)!);
				harness.Dialogs.SaveFileAnswer = path;
				harness.Globals.FilterText = "GVAR_QUEST";

				await harness.Globals.ExportCsvAsync();

				String text = File.ReadAllText(path);
				Assert.StartsWith("Kind,Variable,Number,Macros,Access,Function,Type,Value,File,Line,Procedure", text, StringComparison.Ordinal);
				Assert.Contains("GVAR_QUEST", text, StringComparison.Ordinal);
				Assert.DoesNotContain("abcdefgh", text, StringComparison.Ordinal);
				Assert.Equal(new Byte[] { 0xEF, 0xBB, 0xBF }, File.ReadAllBytes(path).Take(3).ToArray());
			});
		}

		[Fact]
		public void Cancelling_the_export_dialog_writes_nothing()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Globals.Rebuild(Index(harness.Folder.Path), Array.Empty<GlobalFinding>());
				harness.Dialogs.SaveFileAnswer = null;

				await harness.Globals.ExportCsvAsync();

				Assert.Empty(harness.Dialogs.Messages);
			});
		}

		[Fact]
		public void A_file_that_cannot_be_written_is_reported_in_a_message()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Globals.Rebuild(Index(harness.Folder.Path), Array.Empty<GlobalFinding>());
				harness.Dialogs.SaveFileAnswer = harness.Folder.Combine("missing folder", "deeper", "globals.csv");

				await harness.Globals.ExportCsvAsync();

				Assert.True(harness.Dialogs.Messages.Count == 1 || File.Exists(harness.Dialogs.SaveFileAnswer));
			});
		}

		[Fact]
		public void The_panel_follows_the_index_after_a_pause()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				Assert.False(harness.Globals.HasData);

				Publish(harness);
				harness.SymbolIndex.RaiseUpdated();

				await Eventually.TrueAsync(() => harness.Globals.HasData);
				Assert.Equal(3, harness.Globals.Groups.Count);
			});
		}

		[Fact]
		public void Several_updates_in_a_row_make_one_change()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				Int32 changes = 0;
				harness.GlobalsService.Changed += (_, _) => changes++;

				for (Int32 i = 0; i < 5; i++)
				{
					harness.SymbolIndex.RaiseUpdated();
				}

				await Eventually.TrueAsync(() => changes > 0);
				await Task.Delay(200);
				Assert.Equal(1, changes);
			});
		}

		[Fact]
		public void The_texts_are_recomputed_when_the_language_changes()
		{
			LanguageCollection.CollectGarbage();
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				Publish(harness);
				await harness.Globals.RefreshAsync();
				TreeGroupNodeViewModel before = harness.Globals.Groups[1];
				Boolean summaryRaised = false;
				((INotifyPropertyChanged)harness.Globals).PropertyChanged += (_, e) => summaryRaised |= e.PropertyName == nameof(harness.Globals.SummaryText);
				try
				{
					Loc.SetLanguage("fr");

					Assert.True(summaryRaised);
					Assert.NotSame(before, harness.Globals.Groups[1]);
					Assert.Equal(Loc.Format("Str.Panels.GlobalsGroupGame", 2), harness.Globals.Groups[1].Title);
				}
				finally
				{
					Loc.SetLanguage(Loc.DefaultLanguage);
				}
			});
		}

		[Fact]
		public void The_expanded_nodes_stay_expanded_when_the_tree_is_rebuilt()
		{
			using ShellHarness harness = new ShellHarness();
			GlobalVariableIndex index = Index(harness.Folder.Path);
			harness.Globals.Rebuild(index, Array.Empty<GlobalFinding>());
			harness.Globals.Groups[0].IsExpanded = false;
			harness.Globals.Groups[1].Children[1].IsExpanded = true;

			harness.Globals.Rebuild(index, Array.Empty<GlobalFinding>());

			Assert.False(harness.Globals.Groups[0].IsExpanded);
			Assert.True(harness.Globals.Groups[1].IsExpanded);
			Assert.True(harness.Globals.Groups[1].Children[1].IsExpanded);
			Assert.False(harness.Globals.Groups[1].Children[0].IsExpanded);
		}

		[Fact]
		public void The_menu_commands_show_the_tab_and_check_the_variables()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				Publish(harness);
				harness.Main.ShowRightBrowser = false;

				harness.Main.ShowGlobalVariablesCommand.Execute(null);
				Assert.True(harness.Main.ShowRightBrowser);
				Assert.Equal(MainViewModel.GlobalsTabIndex, harness.Main.RightTabIndex);

				harness.Main.RightTabIndex = 0;
				await harness.Main.CheckGlobalVariablesAsync();

				Assert.Equal(MainViewModel.GlobalsTabIndex, harness.Main.RightTabIndex);
				Assert.Contains(harness.Problems.Problems, p => p.Origin == ProblemOrigins.GlobalVariables);
			});
		}
	}
}