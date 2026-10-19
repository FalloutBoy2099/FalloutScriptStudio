namespace FalloutScriptStudio.Core.Tests.Performance
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Analysis;
	using FalloutScriptStudio.Core.Indexing;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Core.Refactoring;
	using FalloutScriptStudio.TestSupport;
	using Xunit;
	using Xunit.Abstractions;

	/// <summary>
	/// Timings of the project index (5 000 files), the symbol search, the references, the unused variables, the unused procedure and the
	/// extraction on the generated inputs. Budgets are several times the time measured on the development machine.
	/// </summary>
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

		private static ProjectIndexRoots Roots(String folder)
		{
			return new ProjectIndexRoots { Folders = new[] { new ProjectIndexRoot(folder, true) } };
		}

		[Fact]
		public async Task Index_a_project_of_5000_files_from_scratch()
		{
			String folder = this._inputs.SymbolProjectFolder;
			await this._meter.MeasureAsync("ProjectSymbolIndex.UpdateAsync 5000 files from scratch", 1000, async () =>
			{
				using ProjectSymbolIndex index = new ProjectSymbolIndex();
				await index.UpdateAsync(Roots(folder));
				Assert.InRange(index.FileCount, 3000, 5001);
				Assert.True(index.SymbolCount > 20000, "symbols: " + index.SymbolCount);
			}, 3);
		}

		[Fact]
		public async Task Index_memory_stays_bounded_for_5000_files()
		{
			String folder = this._inputs.SymbolProjectFolder;
			GC.Collect();
			Double before = GC.GetTotalMemory(true) / (1024.0 * 1024.0);
			using ProjectSymbolIndex index = new ProjectSymbolIndex();
			await index.UpdateAsync(Roots(folder));
			GC.Collect();
			Double after = GC.GetTotalMemory(true) / (1024.0 * 1024.0);
			this._meter.Note("index of " + index.FileCount + " files and " + index.SymbolCount + " symbols: " + (after - before).ToString("F1", CultureInfo.InvariantCulture) + " MB of managed memory");
			GC.KeepAlive(index);

			Assert.True(after - before < 120, "memory: " + (after - before).ToString("F1", CultureInfo.InvariantCulture) + " MB");
		}

		[Fact]
		public async Task A_second_run_without_changes_reads_nothing()
		{
			String folder = this._inputs.SymbolProjectFolder;
			using ProjectSymbolIndex index = new ProjectSymbolIndex();
			await index.UpdateAsync(Roots(folder));
			Int32 updates = 0;
			index.Updated += (_, _) => updates++;

			await this._meter.MeasureAsync("ProjectSymbolIndex.UpdateAsync 5000 files, nothing changed", 80, () => index.UpdateAsync(Roots(folder)), 5);

			Assert.Equal(0, updates);
		}

		[Fact]
		public async Task Saving_one_file_updates_the_index_quickly()
		{
			String folder = this._inputs.SymbolProjectFolder;
			using ProjectSymbolIndex index = new ProjectSymbolIndex();
			await index.UpdateAsync(Roots(folder));
			String file = index.Files.First(f => f.EndsWith(".ssl", StringComparison.OrdinalIgnoreCase));
			String text = File.ReadAllText(file);

			await this._meter.MeasureAsync("ProjectSymbolIndex.UpdateFileAsync (one saved script of 5000)", 100, () => index.UpdateFileAsync(file, text), 5);
		}

		[Fact]
		public async Task Searching_the_symbols_of_5000_files_is_instant()
		{
			String folder = this._inputs.SymbolProjectFolder;
			using ProjectSymbolIndex index = new ProjectSymbolIndex();
			await index.UpdateAsync(Roots(folder));
			this._meter.Note("symbols: " + index.SymbolCount);

			this._meter.Measure("ProjectSymbolIndex.Search blank filter (first 200)", 20, () => Assert.Equal(200, index.Search(null, 200).Count));
			this._meter.Measure("ProjectSymbolIndex.Search \"pro\" (prefix, many matches)", 30, () => Assert.Equal(200, index.Search("pro", 200).Count));
			this._meter.Measure("ProjectSymbolIndex.Search \"_00\" (substring, many matches)", 30, () => Assert.NotEmpty(index.Search("_00", 200)));
			this._meter.Measure("ProjectSymbolIndex.Search \"st\" (two letters, subsequence tier)", 30, () => Assert.NotEmpty(index.Search("st", 200)));
			this._meter.Measure("ProjectSymbolIndex.Search \"shared lim\" (two words)", 30, () => Assert.NotEmpty(index.Search("shared lim", 200)));
			this._meter.Measure("ProjectSymbolIndex.Search no match", 30, () => Assert.Empty(index.Search("qqqqzzzz", 200)));
			this._meter.Measure("ProjectSymbolIndex.Find exact name", 5, () => Assert.NotEmpty(index.Find("start")), 20);
		}

		[Fact]
		public async Task Finding_the_references_of_a_name_in_5000_files()
		{
			String folder = this._inputs.SymbolProjectFolder;
			using ProjectSymbolIndex index = new ProjectSymbolIndex();
			await index.UpdateAsync(Roots(folder));

			await this._meter.MeasureAsync("ProjectSymbolIndex.FindOccurrencesAsync 'start' in 5000 files", 700, async () =>
			{
				IReadOnlyList<SymbolOccurrence> result = await index.FindOccurrencesAsync(new SymbolOccurrenceRequest("start"));
				Assert.True(result.Count > 2000);
			}, 3);
			await this._meter.MeasureAsync("ProjectSymbolIndex.FindOccurrencesAsync rare name", 500, async () => Assert.Empty(await index.FindOccurrencesAsync(new SymbolOccurrenceRequest("never_written_anywhere"))), 3);
			this._meter.Measure(
				"ProjectSymbolIndex.GetFilesSeeing (5000 files)",
				60,
				() => Assert.True(index.GetFilesSeeing(Path.Combine(folder, "headers", "shared.h"), new IncludeResolver(null, null)).Count > 1000),
				3);
		}

		[Fact]
		public async Task Cancelling_a_run_stops_it_quickly()
		{
			String folder = this._inputs.SymbolProjectFolder;
			for (Int32 trial = 0; trial < 3; trial++)
			{
				using ProjectSymbolIndex index = new ProjectSymbolIndex();
				using CancellationTokenSource cancel = new CancellationTokenSource();
				Task running = index.UpdateAsync(Roots(folder), cancel.Token);
				await Task.Delay(30);
				Stopwatch watch = Stopwatch.StartNew();
				cancel.Cancel();
				try
				{
					await running;
				}
				catch (OperationCanceledException)
				{
				}

				watch.Stop();
				this._output.WriteLine("[perf] cancel of the index run: " + watch.Elapsed.TotalMilliseconds.ToString("F1", CultureInfo.InvariantCulture) + " ms");
				Assert.True(watch.Elapsed.TotalMilliseconds < 400, "cancel took " + watch.Elapsed.TotalMilliseconds.ToString("F0", CultureInfo.InvariantCulture) + " ms");
			}
		}

		[Fact]
		public void Analyze_a_20k_line_script()
		{
			String text = this._inputs.Script20kText;
			this._meter.Measure("SslTokenizer.Tokenize 20k lines", 25, () => Assert.True(SslTokenizer.Tokenize(text).Count > 50000));
			this._meter.Measure("SslScriptModel.Analyze 20k lines", 30, () => Assert.True(SslScriptModel.Analyze(text).Procedures.Count >= 300));
			this._meter.Measure("UnusedVariableFinder.Find 20k lines", 80, () => Assert.NotNull(UnusedVariableFinder.Find(text)));
			this._meter.Measure("SymbolOccurrenceScanner.Scan 20k lines (hot macro, 2000 uses)", 25, () => Assert.True(SymbolOccurrenceScanner.Scan(text, "a.ssl", PerformanceInputs.HotMacro, true).Count >= 2000));
			this._meter.Measure("SymbolOccurrenceScanner.Scan 20k lines (name that is not there)", 5, () => Assert.Empty(SymbolOccurrenceScanner.Scan(text, "a.ssl", "no_such_name_here", false)));
		}

		[Fact]
		public void Plan_the_removal_of_a_procedure_and_extract_a_block_of_a_20k_line_script()
		{
			String text = this._inputs.Script20kText;
			String name = new InternalParser().ParseProcedures(text, null).First(p => p.StartLine > 5000 && p.EndLine - p.StartLine > 4).Name;
			this._meter.Measure("UnusedProcedureRemover.Plan 20k lines", 40, () => Assert.NotNull(UnusedProcedureRemover.Plan(text, name)));

			ScriptProcedure procedure = new InternalParser().ParseProcedures(text, null).First(p => p.Name == name);
			TextLineLookup lines = new TextLineLookup(text);
			Int32 start = lines.GetLineStart(procedure.StartLine + 2);
			Int32 end = lines.GetLineStart(procedure.StartLine + 3);
			this._meter.Measure("ProcedureExtractor.Analyze one line of a 20k line script", 40, () => ProcedureExtractor.Analyze(text, start, end - start));
		}

		private sealed class TextLineLookup
		{
			private readonly List<Int32> _starts = [0];

			public TextLineLookup(String text)
			{
				for (Int32 i = 0; i < text.Length; i++)
				{
					if (text[i] == '\n')
					{
						this._starts.Add(i + 1);
					}
				}
			}

			public Int32 GetLineStart(Int32 line)
			{
				return this._starts[line - 1];
			}
		}
	}
}