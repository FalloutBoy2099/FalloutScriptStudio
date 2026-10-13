namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Problems;
	using FalloutScriptStudio.Tests.Shell.Support;
	using FalloutScriptStudio.ViewModels.Main;
	using Xunit;

	/// <summary>
	/// The project file of the open project is watched: a hand edit is reloaded (the open scripts are parsed again, the toolbar list of
	/// definitions follows) with a notice in the status bar, an invalid edit is a warning with a link to the file and keeps the last good
	/// settings, a deletion makes the project a folder without settings, and the editor's own save is not announced.
	/// </summary>
	public sealed class MainViewModelProjectWatchTests
	{
		private const String Script = "procedure start begin\r\nend\r\n";

		private static readonly ProjectFileWatchOptions Fast = new ProjectFileWatchOptions
		{
			Debounce = TimeSpan.FromMilliseconds(40),
			PollInterval = TimeSpan.FromMilliseconds(150),
			RetryDelay = TimeSpan.FromMilliseconds(20),
			ReadAttempts = 8,
			SettleAttempts = 3,
		};

		private static String MakeProject(ShellHarness harness, String json)
		{
			String folder = harness.Folder.Combine("mod");
			Directory.CreateDirectory(folder);
			File.WriteAllText(Path.Combine(folder, ProjectSettings.FileName), json);
			return folder;
		}

		private static async Task WaitForAsync(Func<Boolean> condition, String what)
		{
			for (Int32 attempt = 0; attempt < 400 && !condition(); attempt++)
			{
				await Task.Delay(25);
			}

			Assert.True(condition(), "Timed out waiting for: " + what);
		}

		[Fact]
		public void A_hand_edit_reloads_the_project_parses_the_open_scripts_again_and_says_so()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness(Fast);
				String folder = MakeProject(harness, "{ \"name\": \"Before\", \"optimization\": \"none\", \"defines\": [ \"ONE\" ] }");
				await harness.Main.OpenRecentProjectAsync(folder);
				await harness.OpenFileAsync("mod/a.ssl", Script);
				await WaitForAsync(() => harness.Parser.Completed > 0, "the first parse");
				Assert.Contains("ONE", harness.Main.PreprocessorDefineItems);
				Int32 started = harness.Parser.Started;
				harness.Output.ReportStatus("sentinel");

				File.WriteAllText(Path.Combine(folder, ProjectSettings.FileName), "{ \"name\": \"After\", \"optimization\": \"full\", \"defines\": [ \"TWO\" ] }");
				await WaitForAsync(() => harness.Output.StatusMessage == "Project settings reloaded.", "the notice");

				Assert.Equal("Project: After", harness.Main.ProjectText);
				Assert.Equal("Fallout Script Studio (After) - " + harness.Workspace.ActiveDocument!.FilePath, harness.Main.Title);
				Assert.Equal(OptimizationLevel.Full, harness.Settings.Effective.Settings.Compiler.Optimization);
				Assert.Contains("TWO", harness.Main.PreprocessorDefineItems);
				Assert.DoesNotContain("ONE", harness.Main.PreprocessorDefineItems);
				await WaitForAsync(() => harness.Parser.Started > started, "the open script parsed again");
				Assert.True(harness.Parser.MacroReloads > 0);
				Assert.DoesNotContain(harness.Problems.Problems, problem => problem.Origin == ProblemOrigins.ProjectFile);
			});
		}

		[Fact]
		public void An_invalid_edit_keeps_the_last_good_settings_and_is_a_warning_with_a_link_that_opens_the_file_at_the_error()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness(Fast);
				String folder = MakeProject(harness, "{ \"name\": \"Good\", \"optimization\": \"full\" }");
				String file = Path.Combine(folder, ProjectSettings.FileName);
				await harness.Main.OpenRecentProjectAsync(folder);

				File.WriteAllText(file, "{\r\n  \"name\": \"Broken\",\r\n  \"optimization\" \"none\"\r\n}");
				await WaitForAsync(() => harness.Problems.Problems.Any(problem => problem.Origin == ProblemOrigins.ProjectFile), "the warning");

				Problem problem = harness.Problems.Problems.Single(item => item.Origin == ProblemOrigins.ProjectFile);
				Assert.Equal(ProblemSeverity.Warning, problem.Severity);
				Assert.Equal(ProblemSource.Settings, problem.Source);
				Assert.Equal(file, problem.File);
				Assert.Equal(3, problem.Line);
				Assert.Contains("fss-project.json is not valid", problem.Message, StringComparison.Ordinal);
				Assert.Contains("line 3, column", problem.Message, StringComparison.Ordinal);
				Assert.Contains("last valid", problem.Message, StringComparison.Ordinal);
				Assert.NotNull(problem.Hint);
				Assert.Equal(ProblemActionKind.OpenFile, problem.Action!.Kind);
				Assert.Equal("Open the file", problem.Action.Label);
				Assert.Equal("Project: Good", harness.Main.ProjectText);
				Assert.Equal(OptimizationLevel.Full, harness.Settings.Effective.Settings.Compiler.Optimization);
				Assert.True(harness.Main.HasProjectSettings);
				Assert.Contains("not valid", harness.Output.StatusMessage, StringComparison.Ordinal);

				ErrorItemViewModel row = harness.Output.Errors.Single(item => item.Action?.Kind == ProblemActionKind.OpenFile);
				await harness.Output.RunProblemActionAsync(row);

				DocumentViewModel opened = Assert.Single(harness.Workspace.Documents);
				Assert.Equal(file, opened.FilePath);
				Assert.Same(opened, harness.Workspace.ActiveDocument);
			});
		}

		[Fact]
		public void Correcting_the_file_applies_it_and_removes_the_warning()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness(Fast);
				String folder = MakeProject(harness, "{ \"name\": \"Good\" }");
				String file = Path.Combine(folder, ProjectSettings.FileName);
				await harness.Main.OpenRecentProjectAsync(folder);
				File.WriteAllText(file, "{ nope");
				await WaitForAsync(() => harness.Problems.Problems.Any(problem => problem.Origin == ProblemOrigins.ProjectFile), "the warning");

				File.WriteAllText(file, "{ \"name\": \"Fixed\" }");
				await WaitForAsync(() => harness.Output.StatusMessage == "Project settings reloaded.", "the notice");

				Assert.Equal("Project: Fixed", harness.Main.ProjectText);
				Assert.DoesNotContain(harness.Problems.Problems, problem => problem.Origin == ProblemOrigins.ProjectFile);
			});
		}

		[Fact]
		public void Deleting_the_file_reverts_to_a_folder_without_settings()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness(Fast);
				String folder = MakeProject(harness, "{ \"name\": \"Doomed\", \"shortCircuit\": true }");
				await harness.Main.OpenRecentProjectAsync(folder);
				Assert.True(harness.Main.HasProjectSettings);
				Assert.True(harness.Settings.Effective.Settings.Compiler.ShortCircuit);

				File.Delete(Path.Combine(folder, ProjectSettings.FileName));
				await WaitForAsync(() => !harness.Main.HasProjectSettings, "the reversion");

				Assert.True(harness.Main.HasProject);
				Assert.Equal("Project: mod", harness.Main.ProjectText);
				Assert.False(harness.Settings.Effective.Settings.Compiler.ShortCircuit);
				Assert.Same(harness.Settings.Settings, harness.Settings.Effective.Settings);
				Assert.Contains("fss-project.json was deleted", harness.Output.StatusMessage, StringComparison.Ordinal);
				Assert.True(harness.Main.CreateProjectSettingsCommand.CanExecute(null));
			});
		}

		[Fact]
		public void The_editors_own_save_is_not_announced_as_a_reload()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness(Fast);
				String folder = MakeProject(harness, "{ \"name\": \"Mine\" }");
				await harness.Main.OpenRecentProjectAsync(folder);
				harness.Output.ReportStatus("sentinel");

				ProjectSettings edited = harness.Projects.Settings!.Clone();
				edited.Name = "Saved here";
				Assert.True(harness.Projects.SaveSettings(edited));
				await Task.Delay(700);

				Assert.Equal("sentinel", harness.Output.StatusMessage);
				Assert.Equal("Project: Saved here", harness.Main.ProjectText);
			});
		}

		[Fact]
		public void Leaving_the_project_removes_the_warning_about_its_file()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness(Fast);
				String folder = MakeProject(harness, "{ }");
				String other = harness.Folder.Combine("other");
				Directory.CreateDirectory(other);
				await harness.Main.OpenRecentProjectAsync(folder);
				File.WriteAllText(Path.Combine(folder, ProjectSettings.FileName), "{ nope");
				await WaitForAsync(() => harness.Problems.Problems.Any(problem => problem.Origin == ProblemOrigins.ProjectFile), "the warning");

				await harness.Main.OpenRecentProjectAsync(other);

				Assert.DoesNotContain(harness.Problems.Problems, problem => problem.Origin == ProblemOrigins.ProjectFile);
			});
		}

		[Fact]
		public void A_project_without_a_file_that_gets_one_by_hand_is_picked_up()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness(Fast);
				String folder = harness.Folder.Combine("mod");
				Directory.CreateDirectory(folder);
				await harness.Main.OpenRecentProjectAsync(folder);
				Assert.False(harness.Main.HasProjectSettings);

				File.WriteAllText(Path.Combine(folder, ProjectSettings.FileName), "{ \"name\": \"Hand made\" }");
				await WaitForAsync(() => harness.Main.HasProjectSettings, "the new file");

				Assert.Equal("Project: Hand made", harness.Main.ProjectText);
				Assert.Equal("Project settings reloaded.", harness.Output.StatusMessage);
			});
		}
	}
}
