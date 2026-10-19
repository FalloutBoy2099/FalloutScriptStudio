namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Problems;
	using FalloutScriptStudio.Tests.Shell.Support;
	using FalloutScriptStudio.ViewModels.Main;
	using FalloutScriptStudio.ViewModels.ToolWindows;
	using FalloutScriptStudio.Workspace;
	using Xunit;

	/// <summary>Every build command is one cancellable operation: it stops for real, frees the UI and explains tool failures.</summary>
	public sealed class BuildCancellationTests
	{
		private const String Script = "procedure start begin\r\nend\r\n";

		private static ShellHarness CreateHarness()
		{
			ShellHarness harness = new ShellHarness();
			harness.Settings.Update(settings => settings.Compiler.CompileToSourceDirectory = true);
			return harness;
		}

		[Fact]
		public void Cancelling_a_compile_stops_it_frees_the_ui_and_says_so()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = CreateHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				harness.Compiler.Hang = true;

				Task<Boolean> run = harness.Build.CompileAsync(document);
				Assert.True(await Eventually.TrueAsync(() => harness.Compiler.HangingRuns == 1));
				Assert.True(harness.Build.IsBusy);
				Assert.True(harness.Build.Cancel());
				Boolean result = await run;

				Assert.False(result);
				Assert.Equal(1, harness.Compiler.CancelledRuns);
				Assert.False(harness.Build.IsBusy);
				Assert.False(harness.Operations.IsRunning);
				Assert.Equal("Compilation of a.ssl cancelled.", harness.Output.StatusMessage);
				Assert.Equal("Compilation cancelled by the user.", document.BuildLog);
				Assert.Equal(0, harness.Sounds.CompileFailedCount);
				Assert.Empty(harness.Output.Errors);

				// The UI is not stuck: the next compile works.
				harness.Compiler.Hang = false;
				Assert.True(await harness.Build.CompileAsync(document));
			});
		}

		[Fact]
		public void The_status_bar_knows_what_runs_and_the_cancel_command_follows_the_state()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = CreateHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				harness.Compiler.Hang = true;
				Assert.False(harness.Main.IsOperationRunning);
				Assert.False(harness.Main.CancelOperationCommand.CanExecute(null));
				Assert.True(harness.Main.CompileCommand.CanExecute(null));

				Task<Boolean> run = harness.Build.CompileAsync(document);
				Assert.True(await Eventually.TrueAsync(() => harness.Compiler.HangingRuns == 1));

				Assert.True(harness.Main.IsOperationRunning);
				Assert.True(harness.Main.IsBuildRunning);
				Assert.Equal("Compiling a.ssl", harness.Main.OperationText);
				Assert.True(harness.Main.IsOperationIndeterminate);
				Assert.True(harness.Main.CancelOperationCommand.CanExecute(null));
				Assert.False(harness.Main.CompileCommand.CanExecute(null));
				Assert.False(harness.Main.CompileAllOpenCommand.CanExecute(null));
				Assert.False(harness.Main.MassCompileCommand.CanExecute(null));
				Assert.False(harness.Main.PreprocessCommand.CanExecute(null));
				Assert.False(harness.Main.RoundTripCommand.CanExecute(null));

				harness.Main.CancelOperationCommand.Execute(null);
				Assert.Equal("Cancelling: Compiling a.ssl", harness.Main.OperationText);
				Assert.False(harness.Main.CancelOperationCommand.CanExecute(null));
				await run;

				Assert.False(harness.Main.IsOperationRunning);
				Assert.Equal(String.Empty, harness.Main.OperationText);
				Assert.True(harness.Main.CompileCommand.CanExecute(null));
				Assert.False(harness.Main.CancelOperationCommand.CanExecute(null));
			});
		}

		[Fact]
		public void A_second_build_command_is_refused_while_one_runs()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = CreateHarness();
				DocumentViewModel a = await harness.OpenFileAsync("a.ssl", Script);
				DocumentViewModel b = await harness.OpenFileAsync("b.ssl", Script);
				harness.Compiler.Hang = true;
				Task<Boolean> first = harness.Build.CompileAsync(a);
				Assert.True(await Eventually.TrueAsync(() => harness.Compiler.HangingRuns == 1));

				Assert.False(await harness.Build.CompileAsync(b));
				Assert.False(await harness.Build.CompileAllOpenAsync());
				Assert.False(await harness.Build.PreprocessAsync(b));
				Assert.False(await harness.Build.RoundTripAsync(b));
				await harness.Build.MassCompileAsync();

				Assert.Equal(new[] { a.FilePath }, harness.Compiler.Compiled);
				Assert.Empty(harness.ToolWindows.BatchRuns);
				Assert.StartsWith("Another build operation is still running", harness.Output.StatusMessage, StringComparison.Ordinal);
				harness.Build.Cancel();
				await first;
			});
		}

		[Fact]
		public void Cancelling_compile_all_keeps_what_was_compiled_and_starts_no_other_script()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = CreateHarness();
				DocumentViewModel a = await harness.OpenFileAsync("a.ssl", Script);
				DocumentViewModel b = await harness.OpenFileAsync("b.ssl", Script);
				DocumentViewModel c = await harness.OpenFileAsync("c.ssl", Script);
				harness.Compiler.HangWhen = run => run == 2;
				harness.Compiler.Errors = new[] { new BuildError(BuildErrorType.Warning, "careful", BuildError.NoFile, 1) };

				Task<Boolean> run = harness.Build.CompileAllOpenAsync();
				Assert.True(await Eventually.TrueAsync(() => harness.Compiler.HangingRuns == 1));
				OperationInfo? info = harness.Operations.Current;
				Assert.Equal(OperationKind.CompileAll, info!.Kind);
				Assert.Equal("b.ssl", info.Subject);
				Assert.Equal(2, info.Index);
				Assert.Equal(3, info.Total);
				Assert.Equal("Compiling b.ssl (2/3)", harness.Main.OperationText);
				harness.Build.Cancel();
				Boolean result = await run;

				Assert.False(result);
				Assert.Equal(new[] { a.FilePath, b.FilePath }, harness.Compiler.Compiled);
				Assert.Equal("Compilation cancelled: 1 of 3 open script(s) compiled.", harness.Output.StatusMessage);
				Assert.Equal("careful", Assert.Single(harness.Output.Errors).Message);
				Assert.False(harness.Operations.IsRunning);
				Assert.NotNull(c);
			});
		}

		[Fact]
		public void Cancelling_preprocessing_stops_it_and_opens_nothing()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = CreateHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				harness.Compiler.PreprocessedFile = harness.Folder.Write("a_pp.ssl", "// preprocessed\r\n");
				harness.Compiler.Hang = true;

				Task<Boolean> run = harness.Build.PreprocessAsync(document);
				Assert.True(await Eventually.TrueAsync(() => harness.Compiler.HangingRuns == 1));
				harness.Build.Cancel();
				Boolean result = await run;

				Assert.False(result);
				Assert.Single(harness.Workspace.Documents);
				Assert.Equal("Preprocessing of a.ssl cancelled.", harness.Output.StatusMessage);
				Assert.Empty(harness.Dialogs.Messages);
				Assert.False(harness.Build.IsBusy);
			});
		}

		[Fact]
		public void Cancelling_a_round_trip_during_the_compilation_stops_it()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = CreateHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				harness.Compiler.Hang = true;

				Task<Boolean> run = harness.Build.RoundTripAsync(document);
				Assert.True(await Eventually.TrueAsync(() => harness.Compiler.HangingRuns == 1));
				harness.Build.Cancel();
				Boolean result = await run;

				Assert.False(result);
				Assert.Single(harness.Workspace.Documents);
				Assert.Equal("Round trip of a.ssl cancelled.", harness.Output.StatusMessage);
				Assert.False(harness.Build.IsBusy);
			});
		}

		[Fact]
		public void Cancelling_a_round_trip_during_the_decompilation_stops_it_and_opens_no_tab()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = CreateHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				File.WriteAllText(Path.ChangeExtension(document.FilePath!, ".int"), "binary");
				harness.Compiler.DecompiledText = "// round trip\r\n";
				Int32 calls = 0;

				// The compilation is instant, the decompiler (the second call of the fake) hangs.
				harness.Compiler.HangWhen = _ => ++calls > 1;
				Task<Boolean> run = harness.Build.RoundTripAsync(document);
				Assert.True(await Eventually.TrueAsync(() => harness.Compiler.HangingRuns == 1));
				harness.Build.Cancel();
				Boolean result = await run;

				Assert.False(result);
				Assert.Single(harness.Workspace.Documents);
				Assert.Equal(1, harness.Compiler.CancelledRuns);
				Assert.Equal("Round trip of a.ssl cancelled.", harness.Output.StatusMessage);
				Assert.False(harness.Build.IsBusy);
			});
		}

		[Fact]
		public void Cancelling_a_mass_compilation_cancels_the_batch_and_reports_it()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = CreateHarness();
				harness.Folder.Write("project/a.ssl", Script);
				harness.Folder.Write("project/b.ssl", Script);
				harness.Folder.Write("project/c.ssl", Script);
				harness.Settings.Update(settings =>
				{
					settings.Paths.OutputDirectory = harness.Folder.Combine("out");
					settings.Paths.SolutionFolder = harness.Folder.Combine("project");
				});
				harness.ToolWindows.BatchHangs = true;
				harness.ToolWindows.BatchProgress.Add(new BatchCompileProgress(harness.Folder.Combine("project/a.ssl"), true, 1, 0, 3));

				Task run = harness.Build.MassCompileAsync();
				Assert.True(await Eventually.TrueAsync(() => harness.ToolWindows.BatchRuns.Count == 1));
				Assert.True(await Eventually.TrueAsync(() => harness.Operations.Current is { Index: 1 }));
				Assert.Equal("Mass compile (1/3): a.ssl", harness.Main.OperationText);
				harness.Build.Cancel();
				await run;

				Assert.True(harness.ToolWindows.BatchToken.IsCancellationRequested);
				Assert.Equal("Mass compile cancelled: 1 of 3 compiled, 0 failed.", harness.Output.StatusMessage);
				Assert.False(harness.Build.IsBusy);
			});
		}

		[Fact]
		public void The_operation_ends_even_when_the_compiler_throws()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = CreateHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				ThrowingCompiler compiler = new ThrowingCompiler();
				BuildCoordinator build = new BuildCoordinator(harness.Settings, compiler, new FakeBatchCompileService(), harness.Workspace, harness.Coordinator, harness.Dialogs, harness.Output, harness.Notifications, harness.Sounds, harness.ToolWindows, harness.Operations, harness.Problems);

				await Assert.ThrowsAsync<InvalidOperationException>(() => build.CompileAsync(document));

				Assert.False(build.IsBusy);
				Assert.False(harness.Operations.IsRunning);
			});
		}

		[Fact]
		public void A_tool_that_is_missing_becomes_a_problem_with_a_hint_and_an_action_instead_of_a_raw_log_line()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = CreateHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				ToolFailure failure = new ToolFailure(ToolFailureKind.NotFound, harness.Paths.GetResourcePath("compile.exe"));
				harness.Compiler.CompileSucceeds = false;
				harness.Compiler.ToolFailures = new[] { failure };
				harness.Compiler.Errors = new[] { new BuildError(BuildErrorType.Error, failure.LogMessage, BuildError.NoFile, 0) };

				Boolean result = await harness.Build.CompileAsync(document);

				Assert.False(result);
				Assert.Empty(document.BuildErrors);
				ErrorItemViewModel row = harness.Output.Errors.Single();
				Assert.Equal(ProblemSource.Tool, row.Source);
				Assert.Contains("compile.exe was not found", row.Message, StringComparison.Ordinal);
				Assert.True(row.HasHint);
				Assert.Equal(SettingsPage.Compiler, row.Action!.Page);
				Assert.Equal((Int32)OutputTab.Errors, harness.Output.SelectedTab);

				// The problem goes with the next build that works.
				harness.Compiler.CompileSucceeds = true;
				harness.Compiler.ToolFailures = Array.Empty<ToolFailure>();
				harness.Compiler.Errors = Array.Empty<BuildError>();
				Assert.True(await harness.Build.CompileAsync(document));
				Assert.Empty(harness.Output.Errors);
			});
		}

		[Fact]
		public void A_failure_without_any_error_position_still_says_that_something_failed()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = CreateHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				harness.Compiler.CompileSucceeds = false;

				await harness.Build.CompileAsync(document);

				Problem problem = Assert.Single(harness.Problems.Problems);
				Assert.Equal(ProblemSource.Build, problem.Source);
				Assert.Equal("a.ssl failed to compile, but the tool reported no error position.", problem.Message);
				Assert.Same(document, problem.Owner);

				// Compiling again replaces the problem of the document instead of adding a second one.
				await harness.Build.CompileAsync(document);
				Assert.Single(harness.Problems.Problems);

				// A closed document takes its problems with it.
				await harness.Workspace.CloseAsync(document);
				Assert.Empty(harness.Problems.Problems);
			});
		}

		[Fact]
		public void A_missing_output_folder_is_a_settings_problem_with_the_paths_page_as_action()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = CreateHarness();
				harness.Settings.Update(settings =>
				{
					settings.Compiler.CompileToSourceDirectory = false;
					settings.Paths.OutputDirectory = null;
				});
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);

				await harness.Build.CompileAsync(document);

				Problem problem = Assert.Single(harness.Problems.Problems);
				Assert.Equal(ProblemSource.Settings, problem.Source);
				Assert.Equal(SettingsPage.Paths, problem.Action!.Page);
				Assert.Contains("compiling next to the source", problem.Hint, StringComparison.Ordinal);
			});
		}

		[Fact]
		public void Compiling_a_header_is_a_problem_of_the_header()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = CreateHarness();
				DocumentViewModel header = await harness.OpenFileAsync("defs.h", "#define A 1\r\n");

				await harness.Build.CompileAsync(header);

				Problem problem = Assert.Single(harness.Problems.Problems);
				Assert.Equal("defs.h is not an SSL script and cannot be compiled.", problem.Message);
			});
		}

		[Fact]
		public void The_failures_of_a_mass_compilation_are_listed_as_build_problems_and_replaced_by_the_next_run()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = CreateHarness();
				String project = harness.Folder.Combine("project");
				String failed = harness.Folder.Write("project/a.ssl", Script);
				harness.Settings.Update(settings =>
				{
					settings.Paths.OutputDirectory = harness.Folder.Combine("out");
					settings.Paths.SolutionFolder = project;
				});
				harness.ToolWindows.BatchFailures.Add(new BatchCompileFailure(failed, "[Error] <" + failed + ">:3:5: undeclared variable\r\n[Warning] <" + failed + ">:4:1: unused\r\n"));
				harness.ToolWindows.BatchFailures.Add(new BatchCompileFailure(harness.Folder.Combine("project/b.ssl"), "the tool wrote nothing useful"));

				await harness.Build.MassCompileAsync();

				Assert.Equal(2, harness.Problems.Problems.Count);
				Problem first = harness.Problems.Problems[0];
				Assert.Equal("undeclared variable", first.Message);
				Assert.Equal(3, first.Line);
				Assert.Equal(failed, first.File);
				Assert.Equal(ProblemOrigins.MassCompile, first.Origin);
				Assert.Contains("failed to compile", harness.Problems.Problems[1].Message, StringComparison.Ordinal);

				harness.ToolWindows.BatchFailures.Clear();
				await harness.Build.MassCompileAsync();
				Assert.Empty(harness.Problems.Problems);
			});
		}

		private sealed class ThrowingCompiler : IScriptCompiler
		{
			public Task<CompilationResult> CompileAsync(CompilationRequest request, System.Threading.CancellationToken cancellationToken = default)
			{
				throw new InvalidOperationException("the compiler broke");
			}

			public Task<CompilationResult> PreprocessAsync(CompilationRequest request, System.Threading.CancellationToken cancellationToken = default)
			{
				throw new InvalidOperationException("the compiler broke");
			}

			public Task<DecompilationResult> DecompileAsync(DecompilationRequest request, IProgress<String>? outputProgress = null, System.Threading.CancellationToken cancellationToken = default)
			{
				throw new InvalidOperationException("the compiler broke");
			}

			public String GetOutputPath(String scriptPath, CompilerOptions options, Boolean temporaryOutput = false)
			{
				return scriptPath;
			}
		}
	}
}