namespace FalloutScriptStudio.Tests.Performance
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Services.ScriptTools.Replace;
	using FalloutScriptStudio.Services.ScriptTools.Search;
	using FalloutScriptStudio.Tests.ReplaceInFiles;
	using FalloutScriptStudio.Tests.ScriptTools;
	using FalloutScriptStudio.TestSupport;
	using Xunit;
	using Xunit.Abstractions;

	/// <summary>Replace in Files over the generated project folder of 5 000 files: the preview, its memory, the replacement and the undo.</summary>
	[Collection(PerformanceCollection.Name)]
	[Trait("Category", "Performance")]
	public class ReplaceInFilesPerformanceTests
	{
		private readonly PerformanceInputs _inputs;
		private readonly PerfMeter _meter;
		private readonly ITestOutputHelper _output;

		public ReplaceInFilesPerformanceTests(PerformanceInputs inputs, ITestOutputHelper output)
		{
			this._inputs = inputs;
			this._output = output;
			this._meter = new PerfMeter(output);
		}

		[Fact]
		public void The_preview_of_5000_files()
		{
			String folder = this._inputs.ProjectFolder;
			ReplaceInFilesPlanner planner = new ReplaceInFilesPlanner();
			Int32 matches = 0;

			this._meter.Measure("ReplaceInFilesPlanner.Plan plain text (5000 files)", 6000, () => matches = planner.Plan(ReplaceInFilesPlannerTests.FolderRequest(folder, "NeedleWord_3", "Renamed_3"), null, CancellationToken.None).MatchCount, 1);
			this._meter.Measure("ReplaceInFilesPlanner.Plan whole word", 6000, () => planner.Plan(ReplaceInFilesPlannerTests.FolderRequest(folder, "NeedleWord_3", "Renamed_3", wholeWord: true), null, CancellationToken.None), 1);
			this._meter.Measure("ReplaceInFilesPlanner.Plan regex with groups", 8000, () => planner.Plan(ReplaceInFilesPlannerTests.FolderRequest(folder, @"NeedleWord_(\d+) = 1(\d\d)\b", "Renamed_$1 = $2", regex: true), null, CancellationToken.None), 1);
			this._meter.Measure("ReplaceInFilesPlanner.Plan no match", 6000, () => Assert.Empty(planner.Plan(ReplaceInFilesPlannerTests.FolderRequest(folder, "this text is nowhere", "x"), null, CancellationToken.None).Files), 1);
			this._meter.Note("replacements found: " + matches);
			Assert.True(matches > 0);
		}

		[Fact]
		public void The_preview_keeps_matches_not_file_texts()
		{
			String folder = this._inputs.ProjectFolder;
			GC.Collect();
			Double before = GC.GetTotalMemory(true) / (1024.0 * 1024.0);

			ReplacePlan plan = new ReplaceInFilesPlanner().Plan(ReplaceInFilesPlannerTests.FolderRequest(folder, "NeedleWord_3", "Renamed_3"), null, CancellationToken.None);

			GC.Collect();
			Double after = GC.GetTotalMemory(true) / (1024.0 * 1024.0);
			this._meter.Note("plan of " + plan.MatchCount + " replacements in " + plan.Files.Count + " files: managed memory grows by " + (after - before).ToString("F1", CultureInfo.InvariantCulture) + " MB, peak working set " + PerfMeter.PeakWorkingSetMegabytes.ToString("F0", CultureInfo.InvariantCulture) + " MB");
			Assert.True(after - before < 80, "The plan holds " + (after - before) + " MB.");
			GC.KeepAlive(plan);
		}

		[Fact]
		public async Task A_search_cancelled_stops_quickly()
		{
			String folder = this._inputs.ProjectFolder;
			using CancellationTokenSource cancellation = new CancellationTokenSource();
			Task<ReplacePlan> running = new ReplaceInFilesPlanner().PlanAsync(ReplaceInFilesPlannerTests.FolderRequest(folder, "NeedleWord_3", "Renamed_3"), null, cancellation.Token);
			await Task.Delay(20);
			Stopwatch watch = Stopwatch.StartNew();

			cancellation.Cancel();
			try
			{
				await running;
			}
			catch (OperationCanceledException)
			{
				// Expected unless the run had already finished.
			}

			watch.Stop();
			this._meter.Note("replace preview cancelled after 20 ms: stopped " + watch.Elapsed.TotalMilliseconds.ToString("F1", CultureInfo.InvariantCulture) + " ms after the request");
			Assert.True(watch.Elapsed.TotalMilliseconds < 2000);
		}

		[Fact]
		public async Task Replacing_and_undoing_in_500_files()
		{
			String source = this._inputs.ProjectFolder;
			using ScriptToolsTempDirectory copy = new ScriptToolsTempDirectory();
			List<String> files = Directory.EnumerateFiles(source, "*.ssl", SearchOption.AllDirectories).Where(file => File.ReadAllText(file).Contains("NeedleWord_3", StringComparison.Ordinal)).Take(500).ToList();
			Assert.True(files.Count > 50, "The generated project has only " + files.Count + " files with the needle.");
			Dictionary<String, String> copies = [];
			for (Int32 i = 0; i < files.Count; i++)
			{
				String target = copy.Combine(i.ToString(CultureInfo.InvariantCulture) + "_" + Path.GetFileName(files[i]));
				File.Copy(files[i], target);
				copies[files[i]] = target;
			}

			ReplaceInFilesPlanner planner = new ReplaceInFilesPlanner();
			ReplacePlan plan = planner.Plan(ReplaceInFilesPlannerTests.FolderRequest(copy.Path, "NeedleWord_3", "Renamed_Needle_3"), null, CancellationToken.None);
			List<ReplaceSelection> selection = plan.Files.Select(file => new ReplaceSelection(file, file.Matches)).ToList();
			ReplaceInFilesApplier applier = new ReplaceInFilesApplier(new FakeWorkspace().Interface);

			Stopwatch apply = Stopwatch.StartNew();
			ReplaceResult result = await applier.ApplyAsync(plan, selection, null, CancellationToken.None);
			apply.Stop();
			Stopwatch undo = Stopwatch.StartNew();
			ReplaceUndoResult restored = await applier.UndoAsync(result.Undo!, CancellationToken.None);
			undo.Stop();

			this._meter.Note("apply " + result.Replacements + " replacements in " + result.FilesChanged + " files: " + apply.ElapsedMilliseconds + " ms; undo: " + undo.ElapsedMilliseconds + " ms");
			Assert.Empty(result.Failures);
			Assert.Equal(plan.Files.Count, restored.Restored);
			Assert.Empty(restored.Failures);
			foreach (KeyValuePair<String, String> pair in copies)
			{
				Assert.Equal(File.ReadAllBytes(pair.Key), File.ReadAllBytes(pair.Value));
			}

			Assert.True(apply.ElapsedMilliseconds < 10000, "Applying took " + apply.ElapsedMilliseconds + " ms.");
		}
	}
}