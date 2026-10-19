namespace FalloutScriptStudio.Core.Tests.Performance
{
	using System;
	using System.Collections.Generic;
	using System.Globalization;
	using System.IO;
	using System.Linq;
	using System.Text;
	using FalloutScriptStudio.Core.Building;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.TestSupport;
	using Xunit;
	using Xunit.Abstractions;

	/// <summary>The staleness check of the incremental build on a project of 5 000 scripts that share 300 headers.</summary>
	[Collection(PerformanceCollection.Name)]
	[Trait("Category", "Performance")]
	public sealed class BuildPerformanceTests : IDisposable
	{
		private const Int32 ScriptCount = 5000;
		private const Int32 HeaderCount = 300;

		private readonly PerfMeter _meter;
		private readonly TestFolder _folder = new TestFolder("build-perf");

		public BuildPerformanceTests(ITestOutputHelper output)
		{
			this._meter = new PerfMeter(output);
		}

		public void Dispose()
		{
			this._folder.Dispose();
		}

		[Fact]
		public void Stale_check_of_5000_scripts_with_a_warm_cache()
		{
			String root = this._folder.Path;
			String headers = Path.Combine(root, "headers");
			String resources = Path.Combine(root, "resources");
			Directory.CreateDirectory(headers);
			Directory.CreateDirectory(resources);
			File.WriteAllText(Path.Combine(resources, "compile.exe"), String.Empty);
			for (Int32 i = 0; i < HeaderCount; i++)
			{
				String nested = (i + 1) % 10 != 0 ? "#include \"h" + (i + 1).ToString(CultureInfo.InvariantCulture) + ".h\"\r\n" : String.Empty;
				File.WriteAllText(Path.Combine(headers, "h" + i.ToString(CultureInfo.InvariantCulture) + ".h"), nested + "#define H" + i.ToString(CultureInfo.InvariantCulture) + " 1\r\n");
			}

			CompilerOptions options = new CompilerOptions
			{
				ProgramDirectory = root,
				ResourcesDirectory = resources,
				ScratchDirectory = Path.Combine(root, "scrTemp"),
				Preprocessor = PreprocessorKind.Builtin,
				SearchIncludePath = true,
				ScriptsHeaderFile = Path.Combine(headers, "scripts.h"),
				HeadersDirectory = headers,
			};

			String cacheFile = Path.Combine(root, "fss-build.json");
			List<BuildCandidate> candidates = [];
			IncrementalBuildService writer = new IncrementalBuildService();
			for (Int32 i = 0; i < ScriptCount; i++)
			{
				// Each script includes three headers; a header includes the next ones of its group of ten, so a script depends on up to 30 files.
				String folder = Path.Combine(root, "scripts", "d" + (i % 50).ToString(CultureInfo.InvariantCulture));
				Directory.CreateDirectory(folder);
				String script = Path.Combine(folder, "s" + i.ToString(CultureInfo.InvariantCulture) + ".ssl");
				StringBuilder text = new StringBuilder();
				for (Int32 k = 0; k < 3; k++)
				{
					text.Append("#include \"h").Append((((i * 7) + (k * 31)) % HeaderCount).ToString(CultureInfo.InvariantCulture)).Append(".h\"\r\n");
				}

				text.Append("procedure start begin\r\nend\r\n");
				File.WriteAllText(script, text.ToString());
				File.WriteAllText(Path.ChangeExtension(script, ".int"), "int");
				writer.Begin(script, options, false, cacheFile).Succeeded();
				candidates.Add(new BuildCandidate(script));
			}

			Assert.True(writer.Flush(cacheFile));
			IncrementalBuildService service = new IncrementalBuildService();
			BuildPlan cold = service.Plan(candidates, options, cacheFile);
			Assert.Equal(ScriptCount, cold.UpToDate.Count);

			Double median = this._meter.Measure("IncrementalBuildService.Plan 5000 scripts, 300 headers (warm cache)", 1000, () =>
			{
				BuildPlan plan = service.Plan(candidates, options, cacheFile);
				Assert.Equal(ScriptCount, plan.UpToDate.Count);
			}, 5);

			Assert.True(median < 1000, "The stale check of 5000 scripts took " + median.ToString("F0", CultureInfo.InvariantCulture) + " ms");
		}
	}
}