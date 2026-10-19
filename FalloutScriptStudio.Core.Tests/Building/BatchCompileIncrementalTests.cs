namespace FalloutScriptStudio.Core.Tests.Building
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Building;
	using FalloutScriptStudio.Core.Compilation;
	using Xunit;

	/// <summary>"Only recompile what changed" through <see cref="BatchCompileService"/>.</summary>
	public sealed class BatchCompileIncrementalTests : IDisposable
	{
		private readonly BuildWorkspace _w = new BuildWorkspace();
		private readonly WritingCompiler _compiler = new WritingCompiler();
		private List<String> _scripts = [];

		public BatchCompileIncrementalTests()
		{
			this._w.Write(@"headers\common.h", "#include \"deep.h\"\r\n");
			this._w.Write(@"headers\deep.h", "#define DEEP 1\r\n");
			this._w.Write(@"headers\other.h", "#define OTHER 1\r\n");
			this._scripts.Add(this._w.Write(@"scripts\a.ssl", "#include \"common.h\"\r\nprocedure start begin\r\nend\r\n"));
			this._scripts.Add(this._w.Write(@"scripts\b.ssl", "#include \"common.h\"\r\nprocedure start begin\r\nend\r\n"));
			this._scripts.Add(this._w.Write(@"scripts\c.ssl", "#include \"other.h\"\r\nprocedure start begin\r\nend\r\n"));
			this._scripts.Add(this._w.Write(@"scripts\d.ssl", "procedure start begin\r\nend\r\n"));
		}

		public void Dispose()
		{
			this._w.Dispose();
		}

		private BatchCompileService Service => new BatchCompileService(this._compiler, this._w.Service);

		private BatchCompileOptions Batch(Boolean onlyChanged = true)
		{
			return new BatchCompileOptions { MultiThreaded = false, OnlyChanged = onlyChanged, BuildCacheFile = this._w.CacheFile };
		}

		private Task<BatchCompileResult> RunAsync(Boolean onlyChanged = true, List<BatchCompileProgress>? progress = null, CancellationToken token = default)
		{
			IProgress<BatchCompileProgress>? reporter = progress is null ? null : new Collector(progress);
			return this.Service.CompileAsync(this._scripts, this._w.Options, this.Batch(onlyChanged), reporter, token);
		}

		private static String Name(String path)
		{
			return Path.GetFileName(path);
		}

		[Fact]
		public async Task The_second_run_compiles_nothing()
		{
			BatchCompileResult first = await this.RunAsync();
			Assert.Equal(4, first.Compiled);
			Assert.Equal(0, first.UpToDate);
			this._compiler.Reset();

			List<BatchCompileProgress> progress = [];
			BatchCompileResult second = await this.RunAsync(progress: progress);

			Assert.Empty(this._compiler.Compiled);
			Assert.Equal(4, second.Found);
			Assert.Equal(0, second.Compiled);
			Assert.Equal(4, second.UpToDate);
			Assert.Equal(0, second.Skipped);
			Assert.False(second.Cancelled);
			Assert.Equal(this._scripts.Select(Name).OrderBy(n => n), second.UpToDateScripts.Select(Name).OrderBy(n => n));
			BatchCompileProgress only = Assert.Single(progress);
			Assert.Equal(0, only.Total);
			Assert.Equal(4, only.UpToDate);
		}

		[Fact]
		public async Task Touching_a_header_recompiles_only_the_scripts_that_depend_on_it()
		{
			await this.RunAsync();
			this._compiler.Reset();

			this._w.Touch(Path.Combine(this._w.Headers, "deep.h"), BuildWorkspace.Past.AddHours(5));
			List<BatchCompileProgress> progress = [];
			BatchCompileResult result = await this.RunAsync(progress: progress);

			Assert.Equal(new[] { "a.ssl", "b.ssl" }, this._compiler.Compiled.Select(Name).OrderBy(n => n));
			Assert.Equal(2, result.Compiled);
			Assert.Equal(2, result.UpToDate);
			Assert.Equal(new[] { "c.ssl", "d.ssl" }, result.UpToDateScripts.Select(Name).OrderBy(n => n));
			Assert.Equal(3, progress.Count);
			Assert.All(progress, item => Assert.Equal(2, item.UpToDate));
			Assert.All(progress, item => Assert.Equal(2, item.Total));
			Assert.Equal(new[] { 0, 1, 2 }, progress.Select(item => item.Completed).OrderBy(n => n));
		}

		[Fact]
		public async Task Without_the_option_everything_is_compiled_but_still_remembered()
		{
			await this.RunAsync();
			this._compiler.Reset();

			BatchCompileResult all = await this.RunAsync(onlyChanged: false);
			Assert.Equal(4, this._compiler.Compiled.Count);
			Assert.Equal(4, all.Compiled);
			Assert.Equal(0, all.UpToDate);
			this._compiler.Reset();

			BatchCompileResult none = await this.RunAsync();
			Assert.Empty(this._compiler.Compiled);
			Assert.Equal(4, none.UpToDate);
		}

		[Fact]
		public async Task A_change_of_options_rebuilds_everything()
		{
			await this.RunAsync();
			this._compiler.Reset();

			BatchCompileResult result = await this.Service.CompileAsync(this._scripts, this._w.Options with { Optimization = OptimizationLevel.Full }, this.Batch());

			Assert.Equal(4, result.Compiled);
			Assert.Equal(4, this._compiler.Compiled.Count);
		}

		[Fact]
		public async Task A_failed_script_is_never_up_to_date()
		{
			this._compiler.Succeeds = path => !path.EndsWith("b.ssl", StringComparison.OrdinalIgnoreCase);
			BatchCompileResult first = await this.RunAsync();
			Assert.Equal(3, first.Compiled);
			Assert.Equal(1, first.Failed);
			this._compiler.Reset();

			BatchCompileResult second = await this.RunAsync();

			Assert.Equal(new[] { "b.ssl" }, this._compiler.Compiled.Select(Name));
			Assert.Equal(1, second.Failed);
			Assert.Equal(3, second.UpToDate);

			this._compiler.Succeeds = _ => true;
			this._compiler.Reset();
			BatchCompileResult third = await this.RunAsync();
			Assert.Equal(1, third.Compiled);
			Assert.Equal(3, third.UpToDate);
		}

		[Fact]
		public async Task A_script_that_fails_after_a_good_build_loses_its_up_to_date_state()
		{
			await this.RunAsync();
			String broken = this._scripts[3];
			this._w.Touch(broken, BuildWorkspace.Past.AddHours(2));
			this._compiler.Succeeds = path => !path.EndsWith("d.ssl", StringComparison.OrdinalIgnoreCase);
			await this.RunAsync();

			// The source is back to the state of the good build, but the failed run must not have left it "up to date".
			this._w.Touch(broken, BuildWorkspace.Past);
			this._compiler.Succeeds = _ => true;
			this._compiler.Reset();
			await this.RunAsync();

			Assert.Contains("d.ssl", this._compiler.Compiled.Select(Name));
		}

		[Fact]
		public async Task Cancelling_a_run_leaves_a_consistent_cache()
		{
			using CancellationTokenSource cancellation = new CancellationTokenSource();
			this._compiler.OnStart = (path, token) =>
			{
				if (path.EndsWith("c.ssl", StringComparison.OrdinalIgnoreCase))
				{
					cancellation.Cancel();
				}

				return Task.CompletedTask;
			};

			BatchCompileResult cancelled = await this.RunAsync(token: cancellation.Token);

			Assert.True(cancelled.Cancelled);
			Assert.Equal(2, cancelled.Compiled);
			Assert.True(File.Exists(this._w.CacheFile));

			// A new program: the cache file alone says what is done.
			this._w.Restart();
			this._compiler.OnStart = null;
			this._compiler.Reset();
			BatchCompileResult resumed = await this.RunAsync();

			Assert.Equal(new[] { "c.ssl", "d.ssl" }, this._compiler.Compiled.Select(Name).OrderBy(n => n));
			Assert.Equal(2, resumed.UpToDate);
			Assert.Equal(BuildCacheStatus.Loaded, this._w.Plan(this._scripts).CacheStatus);
		}

		[Fact]
		public async Task Cancelling_while_the_check_runs_reports_a_cancelled_run()
		{
			using CancellationTokenSource cancellation = new CancellationTokenSource();
			cancellation.Cancel();

			BatchCompileResult result = await this.RunAsync(token: cancellation.Token);

			Assert.True(result.Cancelled);
			Assert.Equal(0, result.Compiled);
			Assert.Empty(this._compiler.Compiled);
		}

		[Fact]
		public async Task Without_a_cache_file_nothing_is_checked_or_remembered()
		{
			BatchCompileOptions options = new BatchCompileOptions { MultiThreaded = false, OnlyChanged = true };

			await this.Service.CompileAsync(this._scripts, this._w.Options, options);
			BatchCompileResult second = await this.Service.CompileAsync(this._scripts, this._w.Options, options);

			Assert.Equal(4, second.Compiled);
			Assert.Equal(0, second.UpToDate);
			Assert.False(File.Exists(this._w.CacheFile));
		}

		[Fact]
		public async Task A_parallel_run_remembers_every_script()
		{
			BatchCompileOptions parallel = new BatchCompileOptions { MultiThreaded = true, MaxDegreeOfParallelism = 4, OnlyChanged = true, BuildCacheFile = this._w.CacheFile };

			BatchCompileResult first = await this.Service.CompileAsync(this._scripts, this._w.Options, parallel);
			BatchCompileResult second = await this.Service.CompileAsync(this._scripts, this._w.Options, parallel);

			Assert.Equal(4, first.Compiled);
			Assert.Equal(4, second.UpToDate);
		}

		private sealed class Collector : IProgress<BatchCompileProgress>
		{
			private readonly List<BatchCompileProgress> _target;

			public Collector(List<BatchCompileProgress> target)
			{
				this._target = target;
			}

			public void Report(BatchCompileProgress value)
			{
				lock (this._target)
				{
					this._target.Add(value);
				}
			}
		}
	}
}