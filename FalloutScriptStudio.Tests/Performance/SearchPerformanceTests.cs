namespace FalloutScriptStudio.Tests.Performance
{
	using System;
	using System.Collections.Generic;
	using System.Collections.ObjectModel;
	using System.Diagnostics;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Services.ScriptTools.Search;
	using FalloutScriptStudio.TestSupport;
	using FalloutScriptStudio.Tests.ScriptTools;
	using FalloutScriptStudio.Tests.Shell.Support;
	using FalloutScriptStudio.ViewModels.Panels;
	using FalloutScriptStudio.ViewModels.ScriptTools;
	using FalloutScriptStudio.Workspace.Project;
	using Xunit;
	using Xunit.Abstractions;

	/// <summary>Timings of the folder search, the project explorer and the symbol trees on the generated project folder of 5 000 files.</summary>
	[Collection(PerformanceCollection.Name)]
	[Trait("Category", "Performance")]
	public class SearchPerformanceTests
	{
		private readonly PerformanceInputs _inputs;
		private readonly PerfMeter _meter;

		public SearchPerformanceTests(PerformanceInputs inputs, ITestOutputHelper output)
		{
			this._inputs = inputs;
			this._meter = new PerfMeter(output);
		}

		private static SearchRequest CreateRequest(String folder, String text, Boolean matchCase = true, Boolean wholeWord = false, Boolean regex = false, String masks = "*.ssl;*.h;*.msg")
		{
			Assert.True(SearchMatcher.TryCreate(new SearchOptions(text, matchCase, wholeWord, regex), out SearchMatcher? matcher, out String? error), error);
			return new SearchRequest
			{
				Matcher = matcher!,
				Folder = new SearchFolder(folder, SearchFileMasks.Parse(masks), true, new Dictionary<String, String>()),
			};
		}

		[Fact]
		public void Folder_search_over_5000_files()
		{
			String folder = this._inputs.ProjectFolder;
			ScriptSearcher searcher = new ScriptSearcher();
			this._meter.Measure("ScriptSearcher.EnumerateFiles 5000 files", 300, () => Assert.True(ScriptSearcher.EnumerateFiles(new SearchFolder(folder, SearchFileMasks.Parse("*.ssl;*.h;*.msg"), true, new Dictionary<String, String>()), CancellationToken.None).Count > 3000), 3);

			Int32 hits = 0;
			this._meter.Measure("ScriptSearcher.Search plain text, many hits", 5000, () => hits = searcher.Search(CreateRequest(folder, "NeedleWord_3"), null, CancellationToken.None).Hits.Count, 1);
			this._meter.Note("hits: " + hits);
			this._meter.Measure("ScriptSearcher.Search whole word", 5000, () => searcher.Search(CreateRequest(folder, "NeedleWord_3", true, true), null, CancellationToken.None), 1);
			this._meter.Measure("ScriptSearcher.Search regex", 5000, () => searcher.Search(CreateRequest(folder, @"NeedleWord_\d+ = 1\d\d\b", true, false, true), null, CancellationToken.None), 1);
			this._meter.Measure("ScriptSearcher.Search rare token (few hits)", 5000, () => Assert.NotEmpty(searcher.Search(CreateRequest(folder, "rare_token_zq"), null, CancellationToken.None).Hits), 1);
			this._meter.Measure("ScriptSearcher.Search case-insensitive no hit", 5000, () => Assert.Empty(searcher.Search(CreateRequest(folder, "this text is nowhere", false), null, CancellationToken.None).Hits), 1);
		}

		[Fact]
		public async Task Folder_search_cancellation_and_memory()
		{
			String folder = this._inputs.ProjectFolder;
			ScriptSearcher searcher = new ScriptSearcher();
			GC.Collect();
			Double before = GC.GetTotalMemory(true) / (1024.0 * 1024.0);
			SearchResult result = await searcher.SearchAsync(CreateRequest(folder, "NeedleWord_3"), null, CancellationToken.None);
			Double after = GC.GetTotalMemory(false) / (1024.0 * 1024.0);
			this._meter.Note("managed memory: " + before.ToString("F1", CultureInfo.InvariantCulture) + " MB before, " + after.ToString("F1", CultureInfo.InvariantCulture) + " MB after a search with " + result.Hits.Count + " hits (before collecting)");
			this._meter.Note("peak working set of the test process after the search over " + result.Hits.Count + " hits in 5000 files: " + PerfMeter.PeakWorkingSetMegabytes.ToString("F0", CultureInfo.InvariantCulture) + " MB");

			for (Int32 trial = 0; trial < 3; trial++)
			{
				using CancellationTokenSource cancellation = new CancellationTokenSource();
				Task<SearchResult> running = searcher.SearchAsync(CreateRequest(folder, "NeedleWord_3"), null, cancellation.Token);
				await Task.Delay(20);
				Stopwatch watch = Stopwatch.StartNew();
				cancellation.Cancel();
				try
				{
					await running;
				}
				catch (OperationCanceledException)
				{
				}

				watch.Stop();
				this._meter.Note("search cancelled after 20 ms: stopped " + watch.Elapsed.TotalMilliseconds.ToString("F1", CultureInfo.InvariantCulture) + " ms after the request (completed before cancelling: " + running.IsCompletedSuccessfully + ")");
				Assert.True(watch.Elapsed.TotalMilliseconds < 2000);
			}
		}

		[Fact]
		public void Advanced_search_view_model_folder_scope()
		{
			String folder = this._inputs.ProjectFolder;
			StaTestRunner.Run(async () =>
			{
				using ScriptToolsTempDirectory temp = new ScriptToolsTempDirectory();
				FakeSettingsService settings = new FakeSettingsService(temp.Path);
				FakeWorkspace workspace = new FakeWorkspace();
				AdvancedSearchViewModel viewModel = new AdvancedSearchViewModel(workspace.Interface, settings, new FalloutScriptStudio.Tests.ScriptTools.FakeDialogService(), new ScriptSearcher());
				viewModel.Scope = SearchScope.Folder;
				viewModel.FolderPath = folder;
				viewModel.MatchCase = true;
				viewModel.SearchText = "NeedleWord_3";
				await this._meter.MeasureAsync("AdvancedSearchViewModel.FindAll (folder of 5000 files, results to the list and the output panel)", 6000, async () =>
				{
					await viewModel.FindAllCommand.ExecuteAsync(null);
					Assert.NotEmpty(viewModel.Results);
				}, 1);
				this._meter.Note("results: " + viewModel.Results.Count);
			});
		}

		[Fact]
		public void Project_explorer_5000_files()
		{
			String folder = this._inputs.ProjectFolder;
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				harness.Settings.Update(settings => settings.Paths.SolutionFolder = folder);
				using ProjectExplorerViewModel explorer = new ProjectExplorerViewModel(harness.Settings, harness.Workspace, harness.Dialogs, new FakeShellService());
				explorer.FilterDelay = TimeSpan.Zero;

				this._meter.Measure("ProjectExplorerViewModel.Reload (root listed)", 20, () => explorer.Reload(), 5);
				ProjectNodeViewModel root = explorer.Nodes[0];
				this._meter.Measure("Expand every folder of the tree (500 folders)", 200, () =>
				{
					Expand(root);
				}, 1);
				this._meter.Measure("ProjectFileFilter.ListAll 5000 files", 300, () => new ProjectFileFilter(false).ListAll(folder), 3);
				List<String> all = new ProjectFileFilter(false).ListAll(folder).ToList();
				this._meter.Measure("ProjectFileFilter.FilterByName", 20, () => ProjectFileFilter.FilterByName(all, "file_01"), 20);
				await this._meter.MeasureAsync("Typing a filter text (first walk of the folder + results)", 400, async () =>
				{
					explorer.Reload();
					explorer.FilterText = "file_001";
					await explorer.FilterTask;
					Assert.NotEmpty(explorer.Nodes);
					explorer.FilterText = String.Empty;
				}, 3);
				explorer.FilterText = "file_00";
				await explorer.FilterTask;
				await this._meter.MeasureAsync("Typing another character (index reused)", 30, async () =>
				{
					explorer.FilterText = "file_001";
					await explorer.FilterTask;
					explorer.FilterText = "file_00";
					await explorer.FilterTask;
				}, 5);
			});
		}

		[Fact]
		public void Global_macro_rename_preview_over_the_5000_files_project()
		{
			String folder = this._inputs.ProjectFolder;
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				harness.Settings.Update(settings => settings.Paths.SolutionFolder = folder);
				String header = harness.Folder.Write("headers\\globals.h", "#define rare_token_zq 5\r\n");
				harness.Parser.GlobalMacros.Replace(new[] { new ScriptMacro("rare_token_zq", "rare_token_zq", "5", header, 1, null) });
				FalloutScriptStudio.Documents.DocumentViewModel document = await harness.OpenFileAsync("a.ssl", "procedure start begin\r\n   display_msg(rare_token_zq);\r\nend\r\n");
				await harness.Workspace.RequestParseAsync(document, true);
				IScriptSymbol macro = document.ProgramInfo!.GlobalMacros.Macros["rare_token_zq"];
				harness.Dialogs.PromptAnswer = "rare_token_other";
				harness.DialogueDialogs.Cancel = true;
				String source = document.TextDocument.Text;

				await this._meter.MeasureAsync("Rename of a global macro: the scripts and headers of a 5000-file project are read and the preview is built", 1500, async () =>
				{
					await harness.Workspace.RenameSymbolAsync(document, macro);
					Assert.NotNull(harness.DialogueDialogs.LastPreview);
				}, 3);

				// Equivalence: the preview lists the files that contain the macro, no more and no fewer, and the cancelled preview changed nothing.
				HashSet<String> expected = new HashSet<String>(
					new ProjectFileFilter(false).ListAll(folder)
						.Where(path => path.EndsWith(".ssl", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".h", StringComparison.OrdinalIgnoreCase))
						.Where(path => File.ReadAllText(path, Encoding.Latin1).Contains("rare_token_zq", StringComparison.Ordinal)),
					StringComparer.OrdinalIgnoreCase);
				HashSet<String> listed = new HashSet<String>(
					harness.DialogueDialogs.LastPreview!.Matches.Select(match => match.FilePath).Where(path => path.StartsWith(folder, StringComparison.OrdinalIgnoreCase)),
					StringComparer.OrdinalIgnoreCase);
				this._meter.Note("files with the macro in the preview: " + listed.Count);
				Assert.NotEmpty(expected);
				Assert.True(expected.SetEquals(listed), "the preview does not list exactly the files that contain the macro");
				Assert.Equal(source, document.TextDocument.Text);
			});
		}

		[Fact]
		public void Procedures_panel_is_updated_in_place_after_a_parse()
		{
			String text = this._inputs.Script20kText;
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				FalloutScriptStudio.Documents.DocumentViewModel document = await harness.OpenFileAsync("big.ssl", text);
				await harness.Workspace.RequestParseAsync(document, true);
				TreeGroupNodeViewModel local = harness.Procedures.Groups.Single(group => group.IsLocal);
				List<TreeNodeViewModel> before = local.Children.ToList();
				Assert.True(before.Count >= 300, "procedures in the tree: " + before.Count);
				Int32 firstLine = before.Cast<ProcedureNodeViewModel>().Select(node => node.Procedure.StartLine).Min();

				document.TextDocument.Insert(0, "// one more line\r\n");
				await this._meter.MeasureAsync("Procedures panel: a parse result of 300 procedures arrives (nodes updated in place)", 100, async () => await harness.Workspace.RequestParseAsync(document, true), 5);

				Assert.Equal(before.Count, local.Children.Count);
				for (Int32 i = 0; i < before.Count; i++)
				{
					Assert.Same(before[i], local.Children[i]);
				}

				Assert.Equal(firstLine + 1, local.Children.Cast<ProcedureNodeViewModel>().Select(node => node.Procedure.StartLine).Min());
				harness.Procedures.FilterText = "perf_proc_15";
				Assert.InRange(local.Children.Count(child => child.IsFilterVisible), 5, 20);
				harness.Procedures.FilterText = String.Empty;
				Assert.All(local.Children, child => Assert.True(child.IsFilterVisible));
				this._meter.Measure("Procedures panel: filter typed (300 procedures)", 30, () =>
				{
					harness.Procedures.FilterText = "perf_proc_15";
					harness.Procedures.FilterText = String.Empty;
				}, 5);
			});
		}

		[Fact]
		public void Functions_tree_filter_with_1200_functions()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				File.Copy(
					Path.Combine(TestFiles.RepositoryRoot, "FalloutScriptStudio", "Content", "descriptions", "functions.ini"),
					Path.Combine(harness.Paths.DescriptionsDirectory, "functions.ini"),
					true);
				await harness.Catalog.LoadAsync();
				await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ApplicationIdle);
				FunctionsPanelViewModel panel = harness.Functions;
				Int32 total = panel.Roots.Sum(Count);
				this._meter.Note("functions in the tree: " + total);
				Assert.True(total > 1000);
				Assert.Equal(panel.Roots, panel.VisibleRoots);

				String[] typed = { "o", "ob", "obj", "obj_", "obj_p", "obj_pi", "obj_pid" };
				this._meter.Measure("Functions tree: typing 'obj_pid' one letter at a time and clearing the filter", 150, () =>
				{
					foreach (String text in typed.Append(String.Empty))
					{
						panel.FilterText = text;
					}
				}, 5);

				panel.FilterText = "obj_pid";
				Int32 shown = panel.VisibleRoots.Sum(CountVisible);
				Assert.InRange(shown, 1, 20);
				panel.FilterText = String.Empty;
				Assert.Equal(total, panel.VisibleRoots.Sum(CountVisible));
			});
		}

		private static Int32 Count(FunctionNodeViewModel node)
		{
			return 1 + node.Children.Sum(Count);
		}

		private static Int32 CountVisible(FunctionNodeViewModel node)
		{
			return 1 + node.VisibleChildren.Sum(CountVisible);
		}

		private static void Expand(ProjectNodeViewModel node)
		{
			node.IsExpanded = true;
			foreach (ProjectNodeViewModel child in node.Children.ToList())
			{
				if (child.IsDirectory)
				{
					Expand(child);
				}
			}
		}

		[Fact]
		public void Symbol_trees_with_300_procedures()
		{
			String text = this._inputs.Script20kText;
			String path = this._inputs.Script20kPath;
			ProgramInfo parsed = EditorPerformanceTests.ParseLikeTheApplication(text, path);
			// Native-parser-like data: every procedure has local variables and references.
			ProgramInfo info = new ProgramInfo(
				parsed.Procedures.Select(p => new ScriptProcedure(p.Name)
				{
					DeclaredFile = path,
					StartFile = path,
					FileName = Path.GetFileName(path),
					DeclaredLine = p.DeclaredLine,
					StartLine = p.StartLine,
					EndLine = p.EndLine,
					ArgumentCount = 2,
					Variables = Enumerable.Range(0, 8).Select(v => new ScriptVariable("local_" + v, VariableScope.Local) { DeclaredFile = path, DeclaredLine = p.StartLine + v }).ToArray(),
				}).ToArray(),
				Enumerable.Range(0, 400).Select(v => new ScriptVariable("global_" + v, VariableScope.Global) { FileName = Path.GetFileName(path), DeclaredFile = path, DeclaredLine = v + 1 }).ToArray(),
				parsed.ScriptMacros,
				null,
				path).WithFlags(isParsed: true, hasNativeData: true);

			this._meter.Measure("SymbolTreeBuilder.BuildProcedureGroups 300 procedures", 20, () => SymbolTreeBuilder.BuildProcedureGroups(info, Path.GetFileName(path), true), 10);
			this._meter.Measure("SymbolTreeBuilder.BuildVariableGroups 400 variables", 20, () => SymbolTreeBuilder.BuildVariableGroups(info, Path.GetFileName(path)), 10);
			IReadOnlyList<TreeGroupNodeViewModel> groups = SymbolTreeBuilder.BuildProcedureGroups(info, Path.GetFileName(path), true);
			this._meter.Measure("TreeFilter.Apply per keystroke (300 procedures, 2400 locals)", 30, () =>
			{
				TreeFilter.Apply(groups, "perf_proc_15");
				TreeFilter.Apply(groups, "perf_proc_150");
				TreeFilter.Apply(groups, String.Empty);
			}, 10);
		}
	}
}