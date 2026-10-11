namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Tests.Shell.Support;
	using FalloutScriptStudio.Workspace;
	using Xunit;

	public sealed class BuildCoordinatorTests
	{
		private const String Script = "procedure start begin\r\nend\r\n";

		private static ShellHarness CreateHarness()
		{
			ShellHarness harness = new ShellHarness();
			harness.Settings.Update(settings => settings.Compiler.CompileToSourceDirectory = true);
			return harness;
		}

		[Fact]
		public void A_successful_compile_stores_the_log_announces_it_and_clears_parser_errors()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = CreateHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				document.ParserErrors = new[] { new BuildError(BuildErrorType.Error, "old", document.FilePath!, 1) };
				ShellNotification? toast = null;
				harness.Notifications.NotificationRaised += (_, notification) => toast = notification;

				Boolean result = await harness.Build.CompileAsync(document);

				Assert.True(result);
				Assert.Equal(new[] { document.FilePath }, harness.Compiler.Compiled);
				Assert.Contains("Compilation Successfully!", document.BuildLog, StringComparison.Ordinal);
				Assert.Empty(document.ParserErrors);
				Assert.NotNull(toast);
				Assert.Equal(NotificationKind.Success, toast!.Kind);
				Assert.Contains("Compiled:", harness.Output.StatusMessage, StringComparison.Ordinal);
				Assert.Equal(0, harness.Sounds.CompileFailedCount);
			});
		}

		[Fact]
		public void A_failed_compile_plays_the_sound_and_shows_the_errors_tab()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = CreateHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				harness.Compiler.CompileSucceeds = false;
				harness.Compiler.Errors = new[] { new BuildError(BuildErrorType.Error, "bad", document.FilePath!, 1) };

				Boolean result = await harness.Build.CompileAsync(document);

				Assert.False(result);
				Assert.Equal(1, harness.Sounds.CompileFailedCount);
				Assert.Contains("Compilation Failed!", document.BuildLog, StringComparison.Ordinal);
				Assert.Equal("bad", Assert.Single(document.BuildErrors).Message);
				Assert.Equal((Int32)OutputTab.Errors, harness.Output.SelectedTab);
				Assert.Equal("bad", Assert.Single(harness.Output.Errors).Message);
			});
		}

		[Fact]
		public void A_modified_script_is_saved_before_it_is_compiled()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = CreateHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				document.TextDocument.Insert(0, "// edit\r\n");

				await harness.Build.CompileAsync(document);

				Assert.False(document.IsModified);
				Assert.StartsWith("// edit", File.ReadAllText(document.FilePath!), StringComparison.Ordinal);
				Assert.Single(harness.Compiler.Compiled);
			});
		}

		[Fact]
		public void An_unsaved_script_whose_save_is_cancelled_is_not_compiled()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = CreateHarness();
				DocumentViewModel document = harness.Workspace.CreateNew(Script);
				harness.Dialogs.SaveFileAnswer = null;

				Boolean result = await harness.Build.CompileAsync(document);

				Assert.False(result);
				Assert.Empty(harness.Compiler.Compiled);
			});
		}

		[Fact]
		public void Only_ssl_scripts_can_be_compiled()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = CreateHarness();
				DocumentViewModel header = await harness.OpenFileAsync("defs.h", "#define A 1\r\n");

				Boolean result = await harness.Build.CompileAsync(header);

				Assert.False(result);
				Assert.Empty(harness.Compiler.Compiled);
				Assert.Single(harness.Dialogs.Messages);
			});
		}

		[Fact]
		public void Compiling_needs_an_output_folder_unless_the_script_folder_is_used()
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

				Boolean result = await harness.Build.CompileAsync(document);

				Assert.False(result);
				Assert.Empty(harness.Compiler.Compiled);
				Assert.Single(harness.Dialogs.Messages);
			});
		}

		[Fact]
		public void Compile_all_open_builds_every_script_and_summarizes_the_logs()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = CreateHarness();
				DocumentViewModel a = await harness.OpenFileAsync("a.ssl", Script);
				DocumentViewModel b = await harness.OpenFileAsync("b.ssl", Script);
				await harness.OpenFileAsync("defs.h", "#define A 1\r\n");

				Boolean result = await harness.Build.CompileAllOpenAsync();

				Assert.True(result);
				Assert.Equal(2, harness.Compiler.Compiled.Count);
				Assert.Contains("compile output", a.BuildLog, StringComparison.Ordinal);
				Assert.Contains("compile output", b.BuildLog, StringComparison.Ordinal);
				Assert.Contains("Compiled 2 of 2", harness.Output.StatusMessage, StringComparison.Ordinal);
			});
		}

		[Fact]
		public void Preprocessing_opens_the_preprocessed_copy()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = CreateHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				harness.Compiler.PreprocessedFile = harness.Folder.Write("a_pp.ssl", "// preprocessed\r\n");

				Boolean result = await harness.Build.PreprocessAsync(document);

				Assert.True(result);
				Assert.Equal(new[] { document.FilePath }, harness.Compiler.Preprocessed);
				Assert.NotNull(harness.Workspace.FindDocument(harness.Compiler.PreprocessedFile));
				Assert.Equal((Int32)OutputTab.Build, harness.Output.SelectedTab);
			});
		}

		[Fact]
		public void A_failed_preprocessing_is_reported_and_nothing_is_opened()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = CreateHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				harness.Compiler.CompileSucceeds = false;

				Boolean result = await harness.Build.PreprocessAsync(document);

				Assert.False(result);
				Assert.Single(harness.Workspace.Documents);
				Assert.Single(harness.Dialogs.Messages);
			});
		}

		[Fact]
		public void Round_trip_compiles_to_a_temporary_file_and_opens_the_decompiled_result()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = CreateHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				String compiled = Path.ChangeExtension(document.FilePath!, ".int");
				File.WriteAllText(compiled, "binary");
				harness.Compiler.DecompiledText = "// round trip\r\nprocedure start begin\r\nend\r\n";

				Boolean result = await harness.Build.RoundTripAsync(document);

				Assert.True(result);
				Assert.Equal(2, harness.Workspace.Documents.Count);
				DocumentViewModel decompiled = harness.Workspace.ActiveDocument!;
				Assert.StartsWith("// round trip", decompiled.TextDocument.Text, StringComparison.Ordinal);
				Assert.StartsWith(harness.Paths.ScratchDirectory, decompiled.FilePath, StringComparison.OrdinalIgnoreCase);
			});
		}

		[Fact]
		public void Round_trip_is_refused_when_the_user_batch_file_compiles()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = CreateHarness();
				harness.Settings.Update(settings => settings.Compiler.UseUserCommand = true);
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);

				Boolean result = await harness.Build.RoundTripAsync(document);

				Assert.False(result);
				Assert.Empty(harness.Compiler.Compiled);
			});
		}

		[Fact]
		public void Mass_compile_hands_the_project_scripts_to_the_batch_window()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = CreateHarness();
				harness.Folder.Write("project/a.ssl", Script);
				harness.Folder.Write("project/sub/b.ssl", Script);
				harness.Settings.Update(settings =>
				{
					settings.Paths.OutputDirectory = harness.Folder.Combine("out");
					settings.Paths.SolutionFolder = harness.Folder.Combine("project");
				});

				await harness.Build.MassCompileAsync();

				IReadOnlyList<String> run = Assert.Single(harness.ToolWindows.BatchRuns);
				Assert.Equal(2, run.Count);
			});
		}

		[Fact]
		public void Mass_compile_asks_for_a_folder_when_no_project_is_set_and_remembers_it()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = CreateHarness();
				harness.Folder.Write("scripts/a.ssl", Script);
				harness.Settings.Update(settings => settings.Paths.OutputDirectory = harness.Folder.Combine("out"));
				harness.Dialogs.FolderAnswer = harness.Folder.Combine("scripts");

				await harness.Build.MassCompileAsync();

				Assert.Single(harness.ToolWindows.BatchRuns);
				Assert.Equal(harness.Folder.Combine("scripts"), harness.Settings.Settings.Paths.LastMassCompileDirectory);
			});
		}

		[Fact]
		public void Mass_compile_needs_the_output_folder()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = CreateHarness();
				harness.Settings.Update(settings => settings.Paths.OutputDirectory = null);

				await harness.Build.MassCompileAsync();

				Assert.Empty(harness.ToolWindows.BatchRuns);
				Assert.Single(harness.Dialogs.Messages);
			});
		}

		[Fact]
		public void Compile_all_open_lists_the_errors_of_every_compiled_script_in_the_errors_tab()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = CreateHarness();
				DocumentViewModel a = await harness.OpenFileAsync("a.ssl", Script);
				DocumentViewModel b = await harness.OpenFileAsync("b.ssl", Script);
				harness.Compiler.Errors = new[]
				{
					new BuildError(BuildErrorType.Warning, "careful", BuildError.NoFile, 2),
					new BuildError(BuildErrorType.Error, "broken", BuildError.NoFile, 1),
				};

				await harness.Build.CompileAllOpenAsync();

				Assert.Equal(new[] { "broken", "careful", "broken", "careful" }, harness.Output.Errors.Select(e => e.Message));
				Assert.Equal(4, harness.Output.ErrorCount);
				Assert.Equal(2, new[] { a, b }.Count(d => d.BuildErrors.Count == 2));
				Assert.Contains("compile output", harness.Output.BuildText, StringComparison.Ordinal);
			});
		}

		[Fact]
		public void Compile_all_open_without_errors_empties_the_errors_tab()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = CreateHarness();
				DocumentViewModel a = await harness.OpenFileAsync("a.ssl", Script);
				await harness.OpenFileAsync("b.ssl", Script);
				a.ParserErrors = new[] { new BuildError(BuildErrorType.Warning, "old parser warning", BuildError.NoFile, 1) };

				await harness.Build.CompileAllOpenAsync();

				Assert.Empty(harness.Output.Errors);
			});
		}
	}
}
