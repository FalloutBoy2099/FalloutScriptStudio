namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Tests.Shell.Support;
	using FalloutScriptStudio.Workspace;
	using Xunit;

	/// <summary>"Build Changed", "Rebuild All" and the compile profiles through the build coordinator and the main view model.</summary>
	public sealed class IncrementalBuildTests
	{
		private const String Script = "procedure start begin\r\nend\r\n";

		private static ShellHarness CreateHarness()
		{
			ShellHarness harness = new ShellHarness();
			harness.Settings.Update(settings => settings.Compiler.CompileToSourceDirectory = true);
			harness.Compiler.WritesOutput = true;
			return harness;
		}

		private static String Name(String path)
		{
			return Path.GetFileName(path);
		}

		[Fact]
		public void Build_changed_compiles_everything_once_and_then_nothing()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = CreateHarness();
				await harness.OpenFileAsync("a.ssl", Script);
				await harness.OpenFileAsync("b.ssl", Script);

				Assert.True(await harness.Build.BuildChangedAsync());
				Assert.Equal(2, harness.Compiler.Compiled.Count);
				Assert.Equal("2 compiled, 0 up to date, 0 failed", harness.Output.StatusMessage);

				Assert.True(await harness.Build.BuildChangedAsync());
				Assert.Equal(2, harness.Compiler.Compiled.Count);
				Assert.Equal("0 compiled, 2 up to date, 0 failed", harness.Output.StatusMessage);
				Assert.Contains("2 script(s) up to date, not compiled.", harness.Output.BuildText, StringComparison.Ordinal);
				Assert.DoesNotContain("a.ssl", harness.Output.BuildText, StringComparison.Ordinal);
				Assert.False(harness.Build.IsBusy);
			});
		}

		[Fact]
		public void The_build_log_names_the_skipped_scripts_when_the_option_asks_for_it()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = CreateHarness();
				harness.Settings.Update(settings => settings.Compiler.ListUpToDateScripts = true);
				await harness.OpenFileAsync("a.ssl", Script);
				await harness.OpenFileAsync("b.ssl", Script);
				await harness.Build.BuildChangedAsync();

				await harness.Build.BuildChangedAsync();

				Assert.Contains("  a.ssl", harness.Output.BuildText, StringComparison.Ordinal);
				Assert.Contains("  b.ssl", harness.Output.BuildText, StringComparison.Ordinal);
			});
		}

		[Fact]
		public void Compile_all_open_still_compiles_everything_and_remembers_it()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = CreateHarness();
				await harness.OpenFileAsync("a.ssl", Script);
				await harness.OpenFileAsync("b.ssl", Script);

				await harness.Build.CompileAllOpenAsync();
				await harness.Build.CompileAllOpenAsync();
				Assert.Equal(4, harness.Compiler.Compiled.Count);

				await harness.Build.BuildChangedAsync();
				Assert.Equal(4, harness.Compiler.Compiled.Count);
			});
		}

		[Fact]
		public void A_script_compiled_with_the_compile_command_is_up_to_date()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = CreateHarness();
				DocumentViewModel a = await harness.OpenFileAsync("a.ssl", Script);
				await harness.OpenFileAsync("b.ssl", Script);
				await harness.Build.CompileAsync(a);
				harness.Compiler.Compiled.Clear();

				await harness.Build.BuildChangedAsync();

				Assert.Equal(new[] { "b.ssl" }, harness.Compiler.Compiled.Select(Name));
			});
		}

		[Fact]
		public void A_script_with_unsaved_changes_is_saved_and_compiled_the_others_are_not()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = CreateHarness();
				DocumentViewModel a = await harness.OpenFileAsync("a.ssl", Script);
				await harness.OpenFileAsync("b.ssl", Script);
				await harness.Build.BuildChangedAsync();
				harness.Compiler.Compiled.Clear();

				a.TextDocument.Insert(0, "// edit\r\n");
				await harness.Build.BuildChangedAsync();

				Assert.Equal(new[] { "a.ssl" }, harness.Compiler.Compiled.Select(Name));
				Assert.False(a.IsModified);
				Assert.Equal("1 compiled, 1 up to date, 0 failed", harness.Output.StatusMessage);
			});
		}

		[Fact]
		public void A_changed_header_rebuilds_only_the_scripts_that_include_it()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = CreateHarness();
				String header = harness.Folder.Write("common.h", "#define COMMON 1\r\n");
				await harness.OpenFileAsync("a.ssl", "#include \"common.h\"\r\n" + Script);
				await harness.OpenFileAsync("b.ssl", Script);
				await harness.Build.BuildChangedAsync();
				harness.Compiler.Compiled.Clear();

				File.WriteAllText(header, "#define COMMON 2\r\n");
				File.SetLastWriteTimeUtc(header, DateTime.UtcNow.AddMinutes(5));
				await harness.Build.BuildChangedAsync();

				Assert.Equal(new[] { "a.ssl" }, harness.Compiler.Compiled.Select(Name));
			});
		}

		[Fact]
		public void Another_profile_makes_every_script_stale()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = CreateHarness();
				await harness.OpenFileAsync("a.ssl", Script);
				await harness.OpenFileAsync("b.ssl", Script);
				await harness.Build.BuildChangedAsync();
				harness.Compiler.Compiled.Clear();

				harness.Settings.Update(settings => settings.Compiler.ActiveProfile = "Release");
				await harness.Build.BuildChangedAsync();

				Assert.Equal(2, harness.Compiler.Compiled.Count);
				Assert.Equal(OptimizationLevel.Full, harness.Compiler.LastRequest!.Options.Optimization);
				Assert.Equal("RELEASE", harness.Compiler.LastRequest.Options.PreprocessorDefine);

				harness.Compiler.Compiled.Clear();
				await harness.Build.BuildChangedAsync();
				Assert.Empty(harness.Compiler.Compiled);
			});
		}

		[Fact]
		public void A_failed_script_is_compiled_again_until_it_works()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = CreateHarness();
				await harness.OpenFileAsync("a.ssl", Script);
				harness.Compiler.CompileSucceeds = false;

				Assert.False(await harness.Build.BuildChangedAsync());
				Assert.Equal("0 compiled, 0 up to date, 1 failed", harness.Output.StatusMessage);
				Assert.False(await harness.Build.BuildChangedAsync());
				Assert.Equal(2, harness.Compiler.Compiled.Count);

				harness.Compiler.CompileSucceeds = true;
				Assert.True(await harness.Build.BuildChangedAsync());
				Assert.Equal(3, harness.Compiler.Compiled.Count);
				Assert.True(await harness.Build.BuildChangedAsync());
				Assert.Equal(3, harness.Compiler.Compiled.Count);
			});
		}

		[Fact]
		public void The_status_bar_counts_the_scripts_to_compile_and_the_ones_that_are_up_to_date()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = CreateHarness();
				await harness.OpenFileAsync("a.ssl", Script);
				await harness.OpenFileAsync("b.ssl", Script);
				DocumentViewModel c = await harness.OpenFileAsync("c.ssl", Script);
				await harness.Build.BuildChangedAsync();
				File.SetLastWriteTimeUtc(c.FilePath!, DateTime.UtcNow.AddMinutes(5));
				harness.Compiler.Hang = true;

				Task run = harness.Build.BuildChangedAsync();
				Assert.True(await Eventually.TrueAsync(() => harness.Operations.Current is { Index: 1 }));

				Assert.Equal("Compiling c.ssl (1/1), 2 up to date", harness.Main.OperationText);
				Assert.Equal(1, harness.Operations.Current!.Total);
				Assert.Equal(2, harness.Operations.Current.UpToDate);
				harness.Build.Cancel();
				await run;
				Assert.False(harness.Build.IsBusy);
			});
		}

		[Fact]
		public void Cancelling_keeps_what_was_finished_and_builds_the_rest_next_time()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = CreateHarness();
				await harness.OpenFileAsync("a.ssl", Script);
				await harness.OpenFileAsync("b.ssl", Script);
				harness.Compiler.HangWhen = run => run == 2;

				Task<Boolean> first = harness.Build.BuildChangedAsync();
				Assert.True(await Eventually.TrueAsync(() => harness.Compiler.HangingRuns == 1));
				harness.Build.Cancel();
				Assert.False(await first);
				Assert.Contains("cancelled", harness.Output.StatusMessage, StringComparison.OrdinalIgnoreCase);

				harness.Compiler.HangWhen = null;
				harness.Compiler.Compiled.Clear();
				Assert.True(await harness.Build.BuildChangedAsync());

				Assert.Equal(new[] { "b.ssl" }, harness.Compiler.Compiled.Select(Name));
			});
		}

		[Fact]
		public void Rebuild_all_asks_the_batch_window_for_every_script_and_mass_compile_does_not()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = CreateHarness();
				harness.Folder.Write("project/a.ssl", Script);
				harness.Settings.Update(settings =>
				{
					settings.Paths.OutputDirectory = harness.Folder.Combine("out");
					settings.Paths.SolutionFolder = harness.Folder.Combine("project");
				});

				await harness.Main.MassCompileCommand.ExecuteAsync(null);
				await harness.Main.RebuildAllCommand.ExecuteAsync(null);

				Assert.Equal(new[] { false, true }, harness.ToolWindows.RebuildAllRequests);
			});
		}

		[Fact]
		public void A_mass_compile_that_skipped_scripts_says_so_in_the_status_and_the_build_log()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = CreateHarness();
				String a = harness.Folder.Write("project/a.ssl", Script);
				String b = harness.Folder.Write("project/b.ssl", Script);
				harness.Folder.Write("project/c.ssl", Script);
				harness.Settings.Update(settings =>
				{
					settings.Paths.OutputDirectory = harness.Folder.Combine("out");
					settings.Paths.SolutionFolder = harness.Folder.Combine("project");
					settings.Compiler.ListUpToDateScripts = true;
				});
				harness.ToolWindows.BatchUpToDate.AddRange(new[] { a, b });

				await harness.Build.MassCompileAsync();

				Assert.Equal("1 compiled, 2 up to date, 0 failed", harness.Output.StatusMessage);
				Assert.Contains("2 script(s) up to date, not compiled.", harness.Output.BuildText, StringComparison.Ordinal);
				Assert.Contains("  a.ssl", harness.Output.BuildText, StringComparison.Ordinal);
				Assert.Contains("  b.ssl", harness.Output.BuildText, StringComparison.Ordinal);
			});
		}

		[Fact]
		public void The_status_bar_shows_the_skipped_scripts_of_a_mass_compilation()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = CreateHarness();
				harness.Folder.Write("project/a.ssl", Script);
				harness.Folder.Write("project/b.ssl", Script);
				harness.Settings.Update(settings =>
				{
					settings.Paths.OutputDirectory = harness.Folder.Combine("out");
					settings.Paths.SolutionFolder = harness.Folder.Combine("project");
				});
				harness.ToolWindows.BatchHangs = true;
				harness.ToolWindows.BatchProgress.Add(new BatchCompileProgress(harness.Folder.Combine("project/a.ssl"), true, 1, 0, 4) { UpToDate = 9 });

				Task run = harness.Build.MassCompileAsync();
				Assert.True(await Eventually.TrueAsync(() => harness.Operations.Current is { Index: 1 }));

				Assert.Equal("Mass compile (1/4), 9 up to date: a.ssl", harness.Main.OperationText);
				harness.Build.Cancel();
				await run;
			});
		}

		[Fact]
		public void The_menu_toggle_for_mass_compile_reads_and_writes_the_setting()
		{
			ShellStaRunner.Run(() =>
			{
				using ShellHarness harness = CreateHarness();
				Assert.True(harness.Main.MassCompileOnlyChanged);

				harness.Main.MassCompileOnlyChanged = false;

				Assert.False(harness.Settings.Settings.Compiler.MassCompileOnlyChanged);
				harness.Settings.Update(settings => settings.Compiler.MassCompileOnlyChanged = true);
				Assert.True(harness.Main.MassCompileOnlyChanged);
				return Task.CompletedTask;
			});
		}

		[Fact]
		public void The_toolbar_offers_the_profiles_and_the_choice_changes_the_effective_options()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = CreateHarness();
				await harness.OpenFileAsync("a.ssl", Script);

				Assert.Equal(new[] { String.Empty, "Debug", "Release" }, harness.Main.CompileProfileOptions.Select(option => option.Value));
				Assert.Equal(String.Empty, harness.Main.SelectedCompileProfile);
				Assert.True(harness.Main.CanChooseDefine);

				harness.Main.SelectedCompileProfile = "Release";

				Assert.Equal("Release", harness.Settings.Settings.Compiler.ActiveProfile);
				Assert.Equal(OptimizationLevel.Full, harness.Settings.Effective.Settings.Compiler.Optimization);
				Assert.Equal(OptimizationLevel.Basic, harness.Settings.Settings.Compiler.Optimization);
				Assert.Equal("RELEASE", harness.Main.SelectedPreprocessorDefine);
				Assert.False(harness.Main.CanChooseDefine);

				harness.Main.SelectedCompileProfile = String.Empty;

				Assert.Null(harness.Settings.Settings.Compiler.ActiveProfile);
				Assert.Equal(OptimizationLevel.Basic, harness.Settings.Effective.Settings.Compiler.Optimization);
				Assert.True(harness.Main.CanChooseDefine);
			});
		}

		[Fact]
		public void A_profile_added_in_the_settings_appears_in_the_toolbar_after_the_settings_window_closes()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = CreateHarness();
				harness.Settings.Update(settings => settings.Compiler.Profiles.Add(new CompileProfile { Name = "Fast", Optimization = OptimizationLevel.Experimental }));

				await harness.Main.SettingsCommand.ExecuteAsync(null);

				Assert.Equal(new[] { String.Empty, "Debug", "Release", "Fast" }, harness.Main.CompileProfileOptions.Select(option => option.Value));
			});
		}

		[Fact]
		public void A_project_that_chooses_its_profile_keeps_the_choice_in_its_file()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = CreateHarness();
				String folder = harness.Folder.Combine("mod");
				Directory.CreateDirectory(folder);
				File.WriteAllText(
					Path.Combine(folder, ProjectSettings.FileName),
					"{ \"activeCompileProfile\": \"Mine\", \"compileProfiles\": [ { \"name\": \"Mine\", \"optimization\": \"none\" } ] }");
				await harness.Main.OpenRecentProjectAsync(folder);

				Assert.Equal(new[] { String.Empty, "Debug", "Release", "Mine" }, harness.Main.CompileProfileOptions.Select(option => option.Value));
				Assert.Equal("Mine", harness.Main.SelectedCompileProfile);
				Assert.Equal(OptimizationLevel.None, harness.Settings.Effective.Settings.Compiler.Optimization);

				harness.Main.SelectedCompileProfile = "Release";

				Assert.Null(harness.Settings.Settings.Compiler.ActiveProfile);
				Assert.Equal(OptimizationLevel.Full, harness.Settings.Effective.Settings.Compiler.Optimization);
				Assert.Contains("\"activeCompileProfile\": \"Release\"", File.ReadAllText(Path.Combine(folder, ProjectSettings.FileName)), StringComparison.Ordinal);
				Assert.Contains("\"name\": \"Mine\"", File.ReadAllText(Path.Combine(folder, ProjectSettings.FileName)), StringComparison.Ordinal);
			});
		}

		[Fact]
		public void The_build_cache_of_a_project_is_written_in_its_folder_and_without_a_project_next_to_the_settings()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = CreateHarness();
				await harness.OpenFileAsync("a.ssl", Script);
				await harness.Build.BuildChangedAsync();

				Assert.True(File.Exists(harness.Paths.BuildCacheFile));

				String folder = harness.Folder.Combine("mod");
				Directory.CreateDirectory(folder);
				File.WriteAllText(Path.Combine(folder, ProjectSettings.FileName), "{ }");
				await harness.Main.OpenRecentProjectAsync(folder);
				DocumentViewModel inProject = await harness.OpenFileAsync("mod/b.ssl", Script);
				await harness.Build.CompileAsync(inProject);

				Assert.True(File.Exists(Path.Combine(folder, "fss-build.json")));
			});
		}
	}
}