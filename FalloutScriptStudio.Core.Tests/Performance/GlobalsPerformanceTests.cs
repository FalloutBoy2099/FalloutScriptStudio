namespace FalloutScriptStudio.Core.Tests.Performance
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Globals;
	using FalloutScriptStudio.Core.Indexing;
	using FalloutScriptStudio.TestSupport;
	using Xunit;
	using Xunit.Abstractions;

	/// <summary>
	/// Timings and memory of the global variable tracker on a generated project of 5 000 scripts: the cost it adds to the project index,
	/// the resolved index of the variables, the analysis and the CSV export. Budgets are several times the time measured on the development machine.
	/// </summary>
	[Collection(PerformanceCollection.Name)]
	[Trait("Category", "Performance")]
	public class GlobalsPerformanceTests
	{
		private readonly PerformanceInputs _inputs;
		private readonly PerfMeter _meter;
		private readonly ITestOutputHelper _output;

		public GlobalsPerformanceTests(PerformanceInputs inputs, ITestOutputHelper output)
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
		public async Task Indexing_5000_scripts_with_the_global_variables()
		{
			String folder = this._inputs.GlobalsProjectFolder;
			await this._meter.MeasureAsync("ProjectSymbolIndex.UpdateAsync 5000 scripts, globals tracked", 2500, async () =>
			{
				using ProjectSymbolIndex index = new ProjectSymbolIndex();
				await index.UpdateAsync(Roots(folder));
				Assert.True(index.FileCount >= 5000);
			}, 3);
			await this._meter.MeasureAsync("ProjectSymbolIndex.UpdateAsync 5000 scripts, globals not tracked", 2000, async () =>
			{
				using ProjectSymbolIndex index = new ProjectSymbolIndex(() => new ProjectIndexOptions { TrackGlobalVariables = false });
				await index.UpdateAsync(Roots(folder));
				Assert.True(index.FileCount >= 5000);
			}, 3);
		}

		[Fact]
		public async Task The_variables_of_5000_scripts_are_resolved_and_analyzed_quickly()
		{
			String folder = this._inputs.GlobalsProjectFolder;
			using ProjectSymbolIndex index = new ProjectSymbolIndex();
			await index.UpdateAsync(Roots(folder));

			GlobalVariableIndex globals = index.GetGlobalVariables();
			this._meter.Note("global variables: " + globals.Variables.Count + ", uses: " + globals.UseCount + ", files: " + globals.FileCount);
			Assert.True(globals.UseCount > 20000, "uses: " + globals.UseCount);
			Assert.True(globals.Variables.Count > 1000);
			Assert.Contains(globals.Variables, static v => v.Kind == GlobalVariableKind.GameVariable && v.Aliases.Count > 0 && v.Id is not null);
			Assert.False(globals.IsTruncated);

			IReadOnlyList<GlobalFinding> findings = new List<GlobalFinding>();
			this._meter.Measure("GlobalVariableAnalyzer.Analyze (all the rules, 2000 macros)", 600, () => findings = GlobalVariableAnalyzer.Analyze(globals), 3);
			this._meter.Note("findings: " + findings.Count);
			Assert.NotEmpty(findings);
			Assert.Contains(findings, static f => f.Kind == GlobalFindingKind.NameLength);
			Assert.Contains(findings, static f => f.Kind == GlobalFindingKind.ReadNeverWritten);

			this._meter.Measure("GlobalVariableCsv.Format (every use)", 1500, () => Assert.True(GlobalVariableCsv.Format(globals.Variables).Length > 100000), 3);
			this._meter.Measure("GlobalVariableIndex.FindByName", 5, () => Assert.NotEmpty(globals.FindByName("GVAR_QUEST_0042")), 20);
		}

		[Fact]
		public async Task Saving_one_script_keeps_the_update_fast_and_the_variables_current()
		{
			String folder = this._inputs.GlobalsProjectFolder;
			using ProjectSymbolIndex index = new ProjectSymbolIndex();
			await index.UpdateAsync(Roots(folder));
			Int32 before = index.GetGlobalVariables().UseCount;
			String file = index.Files.First(static f => f.EndsWith(".ssl", StringComparison.OrdinalIgnoreCase));
			String text = File.ReadAllText(file);

			await this._meter.MeasureAsync("ProjectSymbolIndex.UpdateFileAsync + GetGlobalVariables (one saved script of 5000)", 800, async () =>
			{
				await index.UpdateFileAsync(file, text + "\r\nprocedure extra begin\r\n\tset_global_var(GVAR_QUEST_0001, 1);\r\nend\r\n");
				Assert.True(index.GetGlobalVariables().UseCount > before);
				await index.UpdateFileAsync(file, text);
			}, 3);
		}

		[Fact]
		public async Task The_global_variables_use_a_bounded_amount_of_memory()
		{
			String folder = this._inputs.GlobalsProjectFolder;
			GC.Collect();
			Double baseline = GC.GetTotalMemory(true) / (1024.0 * 1024.0);
			using ProjectSymbolIndex without = new ProjectSymbolIndex(() => new ProjectIndexOptions { TrackGlobalVariables = false });
			await without.UpdateAsync(Roots(folder));
			GC.Collect();
			Double withoutGlobals = GC.GetTotalMemory(true) / (1024.0 * 1024.0) - baseline;

			using ProjectSymbolIndex with = new ProjectSymbolIndex();
			await with.UpdateAsync(Roots(folder));
			GlobalVariableIndex resolved = with.GetGlobalVariables();
			GC.Collect();
			Double withGlobals = GC.GetTotalMemory(true) / (1024.0 * 1024.0) - baseline - withoutGlobals;
			this._meter.Note("symbol index: " + withoutGlobals.ToString("F1", CultureInfo.InvariantCulture) + " MB; globals added: " + withGlobals.ToString("F1", CultureInfo.InvariantCulture) + " MB for " + resolved.UseCount + " uses");
			GC.KeepAlive(without);
			GC.KeepAlive(with);

			Assert.True(withGlobals < 60, "memory of the globals: " + withGlobals.ToString("F1", CultureInfo.InvariantCulture) + " MB");
		}

		[Fact]
		public void A_script_of_20000_lines_is_scanned_quickly()
		{
			String text = this._inputs.Script20kText;
			this._meter.Measure("GlobalUseScanner.ScanText 20k lines", 120, () => GlobalUseScanner.ScanText(text));

			String heavy = String.Concat(Enumerable.Range(0, 5000).Select(static i => "procedure p" + i.ToString(CultureInfo.InvariantCulture) + " begin\r\n\tset_global_var(GVAR_X" + i.ToString(CultureInfo.InvariantCulture) + ", global_var(GVAR_Y));\r\nend\r\n"));
			this._meter.Measure("GlobalUseScanner.ScanText 5000 procedures with 10000 uses", 150, () => Assert.Equal(10000, GlobalUseScanner.ScanText(heavy).Uses.Length));
		}
	}
}