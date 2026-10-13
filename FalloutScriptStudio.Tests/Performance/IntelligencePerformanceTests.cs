namespace FalloutScriptStudio.Tests.Performance
{
	using System;
	using System.Diagnostics;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Indexing;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Editor;
	using FalloutScriptStudio.Services.ToolWindows;
	using FalloutScriptStudio.TestSupport;
	using FalloutScriptStudio.Tests.Editor.Support;
	using FalloutScriptStudio.Tests.Shell.Support;
	using FalloutScriptStudio.ViewModels.ToolWindows;
	using Xunit;
	using Xunit.Abstractions;

	/// <summary>Timings of the editor and the dialogs that use the project index: the unused variable marks, the symbol list and the deletion of a procedure.</summary>
	[Collection(PerformanceCollection.Name)]
	[Trait("Category", "Performance")]
	public class IntelligencePerformanceTests
	{
		private readonly PerformanceInputs _inputs;
		private readonly PerfMeter _meter;
		private readonly ITestOutputHelper _output;

		public IntelligencePerformanceTests(PerformanceInputs inputs, ITestOutputHelper output)
		{
			this._inputs = inputs;
			this._output = output;
			this._meter = new PerfMeter(output);
		}

		[Fact]
		public void The_unused_variable_marks_of_a_20k_line_script()
		{
			String path = this._inputs.Script20kPath;
			String text = File.ReadAllText(path);
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(text, path, false);
				document.ProgramInfo = EditorPerformanceTests.ParseLikeTheApplication(text, path);
				using EditorSession session = env.StartSession(document);
				this._meter.Measure("DiagnosticsController.Update 20k lines, unused variable marks off", 40, () => session.Diagnostics.Update());

				env.SettingsService.Update(settings => settings.Editor.ShowUnusedVariables = true);
				this._meter.Measure("DiagnosticsController.Update 20k lines, unused variable marks on", 90, () => session.Diagnostics.Update());
				this._output.WriteLine("[perf] unused variable marks: " + session.Diagnostics.Renderer.Count.ToString(CultureInfo.InvariantCulture));
			});
		}

		[Fact]
		public async Task Typing_in_the_symbol_list_over_5000_files()
		{
			ProjectSymbolIndex index = new ProjectSymbolIndex();
			await index.UpdateAsync(new ProjectIndexRoots { Folders = new[] { new ProjectIndexRoot(this._inputs.SymbolProjectFolder, true) } });
			SymbolPickerViewModel viewModel = new SymbolPickerViewModel();
			viewModel.Initialize(new SymbolPickerRequest { Mode = SymbolPickerMode.Search, Index = index, RootFolder = this._inputs.SymbolProjectFolder });
			this._output.WriteLine("[perf] symbols: " + index.SymbolCount.ToString(CultureInfo.InvariantCulture));

			Int32 counter = 0;
			String[] filters = { "p", "pr", "pro", "proc", "proc_", "proc_1", "proc_1_0", "start", "shared_", "hm0" };
			this._meter.Measure("SymbolPickerViewModel.Filter (10 successive keystrokes, 200 rows each)", 300, () =>
			{
				viewModel.Filter = filters[counter++ % filters.Length] + (counter / filters.Length).ToString(CultureInfo.InvariantCulture);
				viewModel.Filter = String.Empty;
				foreach (String filter in filters)
				{
					viewModel.Filter = filter;
					Assert.NotNull(viewModel.SelectedItem);
				}
			});
			viewModel.Dispose();
			index.Dispose();
		}

		[Fact]
		public void Deleting_an_unused_procedure_checks_the_5000_files_of_the_project()
		{
			ShellStaRunner.Run(async () =>
			{
				using IntelligenceHarness harness = new IntelligenceHarness();
				String folder = this._inputs.SymbolProjectFolder;
				harness.Projects.Open(folder);
				await harness.Symbols.RefreshAsync();
				String script = harness.Index.Files.First(f => f.EndsWith(".ssl", StringComparison.OrdinalIgnoreCase) && f.Contains("file_00001", StringComparison.OrdinalIgnoreCase) == false);
				DocumentViewModel? document = await harness.Workspace.OpenAsync(script);
				Assert.NotNull(document);
				String name = new FalloutScriptStudio.Core.Parsing.InternalParser().ParseProcedures(document!.TextDocument.Text, script).First(p => p.StartLine > 0 && !p.Name.Equals("start", StringComparison.OrdinalIgnoreCase)).Name;
				harness.Dialogs.ConfirmAnswer = false;

				Stopwatch watch = Stopwatch.StartNew();
				Boolean deleted = await harness.Workspace.DeleteUnusedProcedureAsync(document, name);
				watch.Stop();

				this._output.WriteLine("[perf] DeleteUnusedProcedureAsync over 5000 files: " + watch.Elapsed.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture) + " ms");
				Assert.False(deleted);
				Assert.Single(harness.Dialogs.Confirmations);
				Assert.True(watch.Elapsed.TotalMilliseconds < 3000, "it took " + watch.Elapsed.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture) + " ms");
			});
		}
	}
}
