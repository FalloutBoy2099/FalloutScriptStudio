namespace FalloutScriptStudio.Core.Tests.Native
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Building;
	using FalloutScriptStudio.Core.Compilation;
	using Xunit;

	/// <summary>
	/// "Only recompile what changed" with the real <c>compile.exe</c> on scripts of FO2tweaksMk2 copied to a scratch folder: the second
	/// run compiles nothing, and touching a header recompiles only the scripts that include it. Skipped when the tools are absent.
	/// </summary>
	public sealed class RealIncrementalBuildTests : IDisposable
	{
		private const Int32 ScriptCount = 6;

		private readonly RealCompilerWorkspace _workspace = new RealCompilerWorkspace();
		private readonly IncrementalBuildService _incremental = new IncrementalBuildService();

		public void Dispose()
		{
			this._workspace.Dispose();
		}

		private String CacheFile => Path.Combine(this._workspace.Root, "fss-build.json");

		/// <summary>Copies the first scripts of the sample that compile; the first three include <c>common.h</c>, which includes <c>inner.h</c>.</summary>
		private async Task<List<String>> PrepareScriptsAsync()
		{
			File.WriteAllText(Path.Combine(this._workspace.Scripts, "inner.h"), "#define FSS_INNER 1\r\n");
			File.WriteAllText(Path.Combine(this._workspace.Scripts, "common.h"), "#include \"inner.h\"\r\n#define FSS_COMMON 1\r\n");
			List<String> scripts = [];
			foreach (String source in Directory.GetFiles(SslcTools.Fo2TweaksSources, "gl_g_*.ssl").OrderBy(file => file, StringComparer.OrdinalIgnoreCase))
			{
				if (scripts.Count == ScriptCount)
				{
					break;
				}

				String target = Path.Combine(this._workspace.Scripts, Path.GetFileName(source));
				String text = File.ReadAllText(source, System.Text.Encoding.Latin1);
				File.WriteAllText(target, (scripts.Count < 3 ? "#include \"common.h\"\r\n" : String.Empty) + text, System.Text.Encoding.Latin1);

				// Some of the samples need options this test does not use: only the ones that compile are kept.
				CompilationResult probe = await this._workspace.Compiler.CompileAsync(new CompilationRequest { ScriptPath = target, Options = this._workspace.CreateOptions(), Batch = true });
				File.Delete(Path.ChangeExtension(target, ".int"));
				if (probe.Success)
				{
					scripts.Add(target);
				}
				else
				{
					File.Delete(target);
				}
			}

			return scripts;
		}

		private Task<BatchCompileResult> RunAsync(IReadOnlyList<String> scripts, CompilerOptions? options = null)
		{
			BatchCompileService service = new BatchCompileService(this._workspace.Compiler, this._incremental);
			return service.CompileAsync(
				scripts,
				options ?? this._workspace.CreateOptions(),
				new BatchCompileOptions { OnlyChanged = true, BuildCacheFile = this.CacheFile });
		}

		private static DateTime Stamp(String script)
		{
			return File.GetLastWriteTimeUtc(Path.ChangeExtension(script, ".int"));
		}

		[RealToolFact(RealTools.Compiler | RealTools.Fo2TweaksSamples)]
		public async Task The_second_run_compiles_nothing_and_a_header_recompiles_only_its_dependants()
		{
			List<String> scripts = await this.PrepareScriptsAsync();
			Assert.Equal(ScriptCount, scripts.Count);

			BatchCompileResult first = await this.RunAsync(scripts);
			Assert.True(first.Failed == 0, String.Join("\n", first.Failures.Select(failure => failure.Output)));
			Assert.Equal(ScriptCount, first.Compiled);
			Dictionary<String, DateTime> built = scripts.ToDictionary(script => script, Stamp);

			BatchCompileResult second = await this.RunAsync(scripts);
			Assert.Equal(0, second.Compiled);
			Assert.Equal(ScriptCount, second.UpToDate);
			Assert.All(scripts, script => Assert.Equal(built[script], Stamp(script)));

			// The nested header changes: the three scripts that include common.h are built again, the three others are not.
			File.WriteAllText(Path.Combine(this._workspace.Scripts, "inner.h"), "#define FSS_INNER 2\r\n");
			BatchCompileResult third = await this.RunAsync(scripts);
			Assert.Equal(3, third.Compiled);
			Assert.Equal(3, third.UpToDate);
			Assert.All(scripts.Take(3), script => Assert.NotEqual(built[script], Stamp(script)));
			Assert.All(scripts.Skip(3), script => Assert.Equal(built[script], Stamp(script)));

			// A new cache object (a restart of the program) reads the same truth from the file.
			IncrementalBuildService restarted = new IncrementalBuildService();
			BuildPlan plan = restarted.Plan(scripts.Select(script => new BuildCandidate(script)).ToList(), this._workspace.CreateOptions(), this.CacheFile);
			Assert.Equal(ScriptCount, plan.UpToDate.Count);
		}

		[RealToolFact(RealTools.Compiler | RealTools.Fo2TweaksSamples)]
		public async Task A_change_of_options_and_a_deleted_output_rebuild_what_they_affect()
		{
			List<String> scripts = await this.PrepareScriptsAsync();
			await this.RunAsync(scripts);

			BatchCompileResult optimized = await this.RunAsync(scripts, this._workspace.CreateOptions() with { Optimization = OptimizationLevel.Full });
			Assert.Equal(ScriptCount, optimized.Compiled);
			Assert.Equal(0, optimized.UpToDate);

			File.Delete(Path.ChangeExtension(scripts[4], ".int"));
			BatchCompileResult repaired = await this.RunAsync(scripts, this._workspace.CreateOptions() with { Optimization = OptimizationLevel.Full });
			Assert.Equal(1, repaired.Compiled);
			Assert.Equal(ScriptCount - 1, repaired.UpToDate);
			Assert.True(File.Exists(Path.ChangeExtension(scripts[4], ".int")));
		}

		[RealToolFact(RealTools.Compiler | RealTools.Fo2TweaksSamples)]
		public async Task A_script_that_no_longer_compiles_is_built_again_until_it_is_fixed()
		{
			List<String> scripts = await this.PrepareScriptsAsync();
			await this.RunAsync(scripts);

			String broken = scripts[5];
			String original = File.ReadAllText(broken, System.Text.Encoding.Latin1);
			File.WriteAllText(broken, original + "\r\nprocedure fss_broken begin\r\n   variable x := ;\r\nend\r\n", System.Text.Encoding.Latin1);

			BatchCompileResult failed = await this.RunAsync(scripts);
			Assert.Equal(1, failed.Failed);
			Assert.Equal(ScriptCount - 1, failed.UpToDate);

			BatchCompileResult again = await this.RunAsync(scripts);
			Assert.Equal(1, again.Failed);

			File.WriteAllText(broken, original, System.Text.Encoding.Latin1);
			BatchCompileResult fixedRun = await this.RunAsync(scripts);
			Assert.Equal(1, fixedRun.Compiled);
			Assert.Equal(0, fixedRun.Failed);
		}
	}
}