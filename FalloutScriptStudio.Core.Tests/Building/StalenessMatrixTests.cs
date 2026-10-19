namespace FalloutScriptStudio.Core.Tests.Building
{
	using System;
	using System.IO;
	using FalloutScriptStudio.Core.Building;
	using FalloutScriptStudio.Core.Compilation;
	using Xunit;

	/// <summary>The cases in which a compiled script is up to date or stale.</summary>
	public sealed class StalenessMatrixTests : IDisposable
	{
		private static readonly DateTime _later = BuildWorkspace.Past.AddHours(3);

		private readonly BuildWorkspace _w = new BuildWorkspace();

		public void Dispose()
		{
			this._w.Dispose();
		}

		private String Script(String include = "#include \"lib.h\"\r\n")
		{
			this._w.Write(@"headers\lib.h", "#define LIB 1\r\n");
			return this._w.Write(@"scripts\a.ssl", include + "procedure start begin\r\nend\r\n");
		}

		[Fact]
		public void Unchanged_script_is_up_to_date_after_a_build()
		{
			String script = this.Script();
			this._w.Build(script);

			BuildDecision decision = this._w.Check(script);

			Assert.Equal(StaleReason.UpToDate, decision.Reason);
			Assert.True(decision.IsUpToDate);
			Assert.Equal(this._w.Output(script), decision.OutputPath);
		}

		[Fact]
		public void A_script_that_was_never_built_is_stale()
		{
			String script = this.Script();

			Assert.Equal(StaleReason.OutputMissing, this._w.Check(script).Reason);
			File.WriteAllText(this._w.Output(script), "int left by something else");
			Assert.Equal(StaleReason.NotRecorded, this._w.Check(script).Reason);
		}

		[Fact]
		public void Source_newer_is_stale()
		{
			String script = this.Script();
			this._w.Build(script);

			this._w.Touch(script, _later);

			Assert.Equal(StaleReason.SourceChanged, this._w.Check(script).Reason);
		}

		[Fact]
		public void Source_with_new_content_and_the_same_time_is_stale()
		{
			String script = this.Script();
			this._w.Build(script);

			File.WriteAllText(script, "#include \"lib.h\"\r\nprocedure start begin\r\n  display_msg(\"x\");\r\nend\r\n");
			this._w.Touch(script, BuildWorkspace.Past);

			Assert.Equal(StaleReason.SourceChanged, this._w.Check(script).Reason);
		}

		[Fact]
		public void Header_newer_is_stale_and_names_the_header()
		{
			String script = this.Script();
			this._w.Build(script);

			this._w.Touch(Path.Combine(this._w.Headers, "lib.h"), _later);

			BuildDecision decision = this._w.Check(script);
			Assert.Equal(StaleReason.DependencyChanged, decision.Reason);
			Assert.Equal(Path.Combine(this._w.Headers, "lib.h"), decision.Detail);
		}

		[Fact]
		public void Nested_include_newer_is_stale()
		{
			this._w.Write(@"headers\inner.h", "#define INNER 1\r\n");
			this._w.Write(@"headers\middle.h", "#include \"inner.h\"\r\n");
			String script = this._w.Write(@"scripts\a.ssl", "#include \"middle.h\"\r\nprocedure start begin\r\nend\r\n");
			this._w.Build(script);
			Assert.Equal(StaleReason.UpToDate, this._w.Check(script).Reason);

			this._w.Touch(Path.Combine(this._w.Headers, "inner.h"), _later);

			BuildDecision decision = this._w.Check(script);
			Assert.Equal(StaleReason.DependencyChanged, decision.Reason);
			Assert.Equal(Path.Combine(this._w.Headers, "inner.h"), decision.Detail);
		}

		[Fact]
		public void An_include_added_to_a_header_is_stale()
		{
			String script = this.Script();
			this._w.Build(script);

			this._w.Write(@"headers\extra.h", "#define EXTRA 1\r\n");
			this._w.Write(@"headers\lib.h", "#include \"extra.h\"\r\n#define LIB 1\r\n", _later);

			Assert.Equal(StaleReason.DependenciesChanged, this._w.Check(script).Reason);
		}

		[Fact]
		public void A_header_that_is_deleted_after_the_build_is_stale()
		{
			String script = this.Script();
			this._w.Build(script);

			File.Delete(Path.Combine(this._w.Headers, "lib.h"));

			BuildDecision decision = this._w.Check(script);
			Assert.Equal(StaleReason.UnresolvedInclude, decision.Reason);
			Assert.Equal("lib.h", decision.Detail);
		}

		[Fact]
		public void A_script_with_a_missing_include_is_never_remembered()
		{
			String script = this._w.Write(@"scripts\a.ssl", "#include \"nowhere.h\"\r\nprocedure start begin\r\nend\r\n");

			BuildTicket ticket = this._w.Build(script);

			Assert.False(ticket.IsCacheable);
			Assert.Equal(StaleReason.NotRecorded, this._w.Check(script).Reason);
		}

		[Fact]
		public void An_include_that_is_a_macro_cannot_be_trusted()
		{
			String script = this._w.Write(@"scripts\a.ssl", "#define HEADER \"lib.h\"\r\n#include HEADER\r\nprocedure start begin\r\nend\r\n");
			this._w.Write(@"headers\lib.h", "#define LIB 1\r\n");

			BuildTicket ticket = this._w.Build(script);

			Assert.False(ticket.IsCacheable);
			Assert.False(this._w.Check(script).IsUpToDate);
		}

		[Fact]
		public void A_conditional_include_that_does_not_exist_does_not_block_but_a_new_file_makes_the_script_stale()
		{
			String script = this._w.Write(@"scripts\a.ssl", "#ifdef DEBUG\r\n#include \"debug.h\"\r\n#endif\r\nprocedure start begin\r\nend\r\n");
			this._w.Build(script);
			Assert.Equal(StaleReason.UpToDate, this._w.Check(script).Reason);

			this._w.Write(@"scripts\debug.h", "#define DEBUGGING 1\r\n");

			Assert.Equal(StaleReason.DependencyChanged, this._w.Check(script).Reason);
		}

		[Fact]
		public void A_header_that_starts_to_resolve_to_another_file_is_stale()
		{
			String script = this.Script();
			this._w.Build(script);

			// The folder of the script comes before the headers folder in the search order.
			this._w.Write(@"scripts\lib.h", "#define LIB 2\r\n");

			Assert.Equal(StaleReason.DependenciesChanged, this._w.Check(script).Reason);
		}

		[Fact]
		public void Option_changes_make_the_script_stale()
		{
			String script = this.Script();
			this._w.Build(script);

			Assert.Equal(StaleReason.OptionsChanged, this._w.Check(script, this._w.Options with { Optimization = OptimizationLevel.Full }).Reason);
			Assert.Equal(StaleReason.OptionsChanged, this._w.Check(script, this._w.Options with { DebugInfo = false }).Reason);
			Assert.Equal(StaleReason.OptionsChanged, this._w.Check(script, this._w.Options with { ShortCircuit = true }).Reason);
			Assert.Equal(StaleReason.OptionsChanged, this._w.Check(script, this._w.Options with { BackwardCompatibility = true }).Reason);
			Assert.Equal(StaleReason.OptionsChanged, this._w.Check(script, this._w.Options with { PreprocessorDefine = "RELEASE" }).Reason);
			Assert.Equal(StaleReason.OptionsChanged, this._w.Check(script, this._w.Options with { Preprocessor = PreprocessorKind.Watcom }).Reason);
			Assert.Equal(StaleReason.OptionsChanged, this._w.Check(script, this._w.Options with { SearchIncludePath = false }).Reason);
			Assert.Equal(StaleReason.UpToDate, this._w.Check(script, this._w.Options with { ShowWarnings = false }).Reason);
			Assert.Equal(StaleReason.UpToDate, this._w.Check(script).Reason);
		}

		[Fact]
		public void The_headers_folder_is_part_of_the_options()
		{
			String script = this.Script();
			this._w.Build(script);
			String other = Path.Combine(this._w.Root, "headers2");
			Directory.CreateDirectory(other);

			BuildDecision decision = this._w.Check(script, this._w.Options with { HeadersDirectory = other });

			Assert.Equal(StaleReason.OptionsChanged, decision.Reason);
		}

		[Fact]
		public void A_script_that_asks_for_short_circuit_itself_is_built_with_it()
		{
			String script = this.Script();
			this._w.Build(script, shortCircuit: true);

			Assert.Equal(StaleReason.OptionsChanged, this._w.Check(script).Reason);
			BuildPlan plan = this._w.Service.Plan(new[] { new BuildCandidate(script, true) }, this._w.Options, this._w.CacheFile);
			Assert.True(plan.Decisions[0].IsUpToDate);
		}

		[Fact]
		public void A_replaced_compiler_makes_every_script_stale()
		{
			String script = this.Script();
			this._w.Build(script);

			String compiler = Path.Combine(this._w.Resources, "compile.exe");
			File.WriteAllText(compiler, "a newer compiler");
			this._w.Touch(compiler, _later);

			Assert.Equal(StaleReason.CompilerChanged, this._w.Check(script).Reason);
		}

		[Fact]
		public void A_replaced_preprocessor_makes_the_scripts_stale_only_when_it_is_in_use()
		{
			CompilerOptions mcpp = this._w.Options with { Preprocessor = PreprocessorKind.Mcpp };
			String script = this.Script();
			this._w.Build(script, mcpp);

			this._w.Touch(Path.Combine(this._w.Resources, "mcpp.exe"), _later);

			Assert.Equal(StaleReason.CompilerChanged, this._w.Check(script, mcpp).Reason);
			Assert.Equal(StaleReason.OptionsChanged, this._w.Check(script).Reason);
		}

		[Fact]
		public void A_deleted_or_replaced_output_is_stale()
		{
			String script = this.Script();
			this._w.Build(script);
			String output = this._w.Output(script);

			File.Delete(output);
			Assert.Equal(StaleReason.OutputMissing, this._w.Check(script).Reason);

			this._w.Build(script);
			File.WriteAllText(output, "someone else compiled this");
			Assert.Equal(StaleReason.OutputChanged, this._w.Check(script).Reason);
		}

		[Fact]
		public void An_output_older_than_the_build_made_it_is_stale()
		{
			String script = this.Script();
			this._w.Build(script);

			this._w.Touch(this._w.Output(script), BuildWorkspace.Past.AddDays(-30));

			Assert.Equal(StaleReason.OutputChanged, this._w.Check(script).Reason);
		}

		[Fact]
		public void A_future_time_stamp_does_not_make_the_script_stale_for_ever_but_any_change_does()
		{
			DateTime future = DateTime.UtcNow.AddDays(400);
			String script = this.Script();
			this._w.Touch(Path.Combine(this._w.Headers, "lib.h"), future);
			this._w.Build(script);
			Assert.Equal(StaleReason.UpToDate, this._w.Check(script).Reason);

			// The clock was corrected and the header was restored from a backup that is older than the compiled script.
			this._w.Touch(Path.Combine(this._w.Headers, "lib.h"), BuildWorkspace.Past.AddDays(-100));

			Assert.Equal(StaleReason.DependencyChanged, this._w.Check(script).Reason);
		}

		[Fact]
		public void A_script_with_a_future_time_stamp_is_up_to_date_once_built()
		{
			String script = this.Script();
			this._w.Touch(script, DateTime.UtcNow.AddDays(30));

			this._w.Build(script);

			Assert.Equal(StaleReason.UpToDate, this._w.Check(script).Reason);
		}

		[Fact]
		public void A_failed_build_is_never_up_to_date()
		{
			String script = this.Script();
			this._w.Build(script);
			this._w.Touch(script, _later);

			BuildTicket ticket = this._w.Service.Begin(script, this._w.Options, false, this._w.CacheFile);
			ticket.Failed();

			Assert.False(this._w.Check(script).IsUpToDate);
			Assert.Equal(StaleReason.NotRecorded, this._w.Check(script).Reason);
		}

		[Fact]
		public void A_file_edited_during_the_build_is_stale_afterwards()
		{
			String script = this.Script();
			BuildTicket ticket = this._w.Service.Begin(script, this._w.Options, false, this._w.CacheFile);

			// The compiler runs; meanwhile the header is saved again.
			File.WriteAllText(this._w.Output(script), "int");
			this._w.Touch(Path.Combine(this._w.Headers, "lib.h"), _later);
			ticket.Succeeded();

			Assert.Equal(StaleReason.DependencyChanged, this._w.Check(script).Reason);
		}

		[Fact]
		public void Rebuild_all_forces_every_script()
		{
			String script = this.Script();
			this._w.Build(script);

			Assert.Equal(StaleReason.Forced, this._w.Check(script, force: true).Reason);
		}

		[Fact]
		public void The_user_batch_file_is_never_cached()
		{
			String script = this.Script();
			CompilerOptions user = this._w.Options with { UseUserCommand = true };

			BuildTicket ticket = this._w.Build(script, user);

			Assert.False(ticket.IsCacheable);
			Assert.Equal(StaleReason.NotCacheable, this._w.Check(script, user).Reason);
		}

		[Fact]
		public void A_missing_script_and_an_unset_output_folder_are_reported()
		{
			String missing = Path.Combine(this._w.Scripts, "gone.ssl");
			Assert.Equal(StaleReason.SourceMissing, this._w.Check(missing).Reason);

			String script = this.Script();
			CompilerOptions noFolder = this._w.Options with { CompileToSourceDirectory = false, OutputDirectory = null };
			Assert.Equal(StaleReason.OutputFolderNotSet, this._w.Check(script, noFolder).Reason);
		}

		[Fact]
		public void The_output_folder_decides_which_file_is_checked()
		{
			String script = this.Script();
			String folder = Path.Combine(this._w.Root, "out");
			Directory.CreateDirectory(folder);
			CompilerOptions toFolder = this._w.Options with { CompileToSourceDirectory = false, OutputDirectory = folder };
			this._w.Build(script, toFolder);

			Assert.Equal(StaleReason.UpToDate, this._w.Check(script, toFolder).Reason);
			Assert.Equal(StaleReason.OutputMissing, this._w.Check(script).Reason);
		}

		[Fact]
		public void The_cache_survives_a_restart()
		{
			String script = this.Script();
			this._w.Build(script);
			Assert.True(this._w.Service.Flush(this._w.CacheFile));

			this._w.Restart();

			Assert.Equal(StaleReason.UpToDate, this._w.Check(script).Reason);
			this._w.Touch(Path.Combine(this._w.Headers, "lib.h"), _later);
			Assert.Equal(StaleReason.DependencyChanged, this._w.Check(script).Reason);
		}

		[Fact]
		public void A_damaged_cache_makes_everything_stale_and_is_repaired_by_the_next_build()
		{
			String script = this.Script();
			this._w.Build(script);
			this._w.Service.Flush(this._w.CacheFile);
			File.WriteAllText(this._w.CacheFile, "{ this is not a cache");
			this._w.Restart();

			BuildPlan plan = this._w.Plan(new[] { script });

			Assert.Equal(BuildCacheStatus.Corrupt, plan.CacheStatus);
			Assert.Equal(StaleReason.NotRecorded, plan.Decisions[0].Reason);

			this._w.Build(script);
			this._w.Service.Flush(this._w.CacheFile);
			this._w.Restart();
			Assert.Equal(StaleReason.UpToDate, this._w.Check(script).Reason);
			Assert.Equal(BuildCacheStatus.Loaded, this._w.Plan(new[] { script }).CacheStatus);
		}

		[Fact]
		public void A_cache_of_another_version_or_with_wrong_data_is_ignored()
		{
			String script = this.Script();
			this._w.Build(script);
			this._w.Service.Flush(this._w.CacheFile);
			String text = File.ReadAllText(this._w.CacheFile);

			foreach (String damaged in new[]
			{
				text.Replace("\"version\": 1", "\"version\": 99", StringComparison.Ordinal).Replace("\"version\":1", "\"version\":99", StringComparison.Ordinal),
				text.Replace("\"entries\"", "\"entry\"", StringComparison.Ordinal),
				"[]",
				String.Empty,
				"{\"version\":1,\"paths\":[\"a\"],\"entries\":[{\"script\":7}]}",
			})
			{
				File.WriteAllText(this._w.CacheFile, damaged);
				this._w.Restart();
				BuildPlan plan = this._w.Plan(new[] { script });
				Assert.False(plan.Decisions[0].IsUpToDate);
				Assert.Equal(BuildCacheStatus.Corrupt, plan.CacheStatus);
			}
		}

		[Fact]
		public void A_locked_cache_gives_a_full_rebuild_and_never_throws()
		{
			String script = this.Script();
			this._w.Build(script);
			this._w.Service.Flush(this._w.CacheFile);
			this._w.Restart();

			using (new FileStream(this._w.CacheFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
			{
				BuildPlan plan = this._w.Plan(new[] { script });
				Assert.Equal(BuildCacheStatus.Unreadable, plan.CacheStatus);
				Assert.False(plan.Decisions[0].IsUpToDate);

				// Saving a new record fails quietly and is retried later.
				this._w.Build(script);
				Assert.False(this._w.Service.Flush(this._w.CacheFile));
			}

			Assert.True(this._w.Service.Flush(this._w.CacheFile));
			this._w.Restart();
			Assert.Equal(StaleReason.UpToDate, this._w.Check(script).Reason);
		}

		[Fact]
		public void The_cache_file_is_replaced_atomically_and_leaves_no_temporary_file()
		{
			String script = this.Script();
			this._w.Build(script);
			this._w.Service.Flush(this._w.CacheFile);

			Assert.True(File.Exists(this._w.CacheFile));
			Assert.Empty(Directory.GetFiles(this._w.Root, "*.tmp"));
		}

		[Fact]
		public void The_cache_written_by_another_program_is_read_again()
		{
			String script = this.Script();
			this._w.Build(script);
			this._w.Service.Flush(this._w.CacheFile);
			this._w.Check(script);
			String saved = File.ReadAllText(this._w.CacheFile);

			// Another editor instance wrote a different cache (here: an empty one).
			File.WriteAllText(this._w.CacheFile, "{\"version\":1,\"paths\":[],\"entries\":[]}");
			this._w.Touch(this._w.CacheFile, DateTime.UtcNow.AddMinutes(1));
			Assert.Equal(StaleReason.NotRecorded, this._w.Check(script).Reason);

			File.WriteAllText(this._w.CacheFile, saved);
			this._w.Touch(this._w.CacheFile, DateTime.UtcNow.AddMinutes(2));
			Assert.Equal(StaleReason.UpToDate, this._w.Check(script).Reason);
		}

		[Fact]
		public void Records_of_scripts_that_no_longer_exist_are_dropped()
		{
			String script = this.Script();
			this._w.Build(script);
			File.Delete(script);

			Assert.Equal(StaleReason.SourceMissing, this._w.Check(script).Reason);
			Assert.False(this._w.Service.GetCache(this._w.CacheFile).TryGet(script, this._w.Output(script), out _));
		}
	}
}