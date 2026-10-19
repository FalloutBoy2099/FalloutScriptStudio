namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Problems;
	using FalloutScriptStudio.Tests.Shell.Support;
	using FalloutScriptStudio.ViewModels.Main;
	using FalloutScriptStudio.Workspace;
	using Xunit;

	/// <summary>The project commands of the main window, the status bar and the title, the hint about the project of an opened script, and the options that follow the project.</summary>
	public sealed class MainViewModelProjectTests
	{
		private const String Script = "procedure start begin\r\nend\r\n";

		private static String MakeProject(ShellHarness harness, String name, String? projectFile = "{ }")
		{
			String folder = harness.Folder.Combine(name);
			Directory.CreateDirectory(folder);
			if (projectFile is not null)
			{
				File.WriteAllText(Path.Combine(folder, ProjectSettings.FileName), projectFile);
			}

			return folder;
		}

		[Fact]
		public void With_no_project_the_status_bar_and_the_title_are_unchanged()
		{
			ShellStaRunner.Run(() =>
			{
				using ShellHarness harness = new ShellHarness();

				Assert.False(harness.Main.HasProject);
				Assert.Equal("Project: <unset>", harness.Main.ProjectText);
				Assert.Equal("Fallout Script Studio", harness.Main.Title);
				Assert.Same(harness.Settings.Settings, harness.Settings.Effective.Settings);
				Assert.False(harness.Main.CloseProjectCommand.CanExecute(null));
				return Task.CompletedTask;
			});
		}

		[Fact]
		public void Open_project_asks_for_a_folder_and_shows_the_project_in_the_status_bar_and_the_title()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				String folder = MakeProject(harness, "mymod", null);
				harness.Dialogs.FolderAnswer = folder;

				await harness.Main.OpenProjectAsync();

				Assert.True(harness.Main.HasProject);
				Assert.Equal(folder, harness.Settings.Settings.Paths.SolutionFolder);
				Assert.Equal(folder, harness.Main.Project.ProjectFolder);
				Assert.Equal("Project: mymod", harness.Main.ProjectText);
				Assert.Equal("Fallout Script Studio (mymod)", harness.Main.Title);
				Assert.Contains("mymod", harness.Main.ProjectTip, StringComparison.Ordinal);
				Assert.True(harness.Main.CloseProjectCommand.CanExecute(null));
				Assert.Equal(new[] { folder }, harness.Main.RecentProjects.ToArray());
				DocumentViewModel document = await harness.OpenFileAsync("mymod/a.ssl", Script);
				Assert.Equal("Fallout Script Studio (mymod) - " + document.FilePath, harness.Main.Title);
			});
		}

		[Fact]
		public void The_name_of_the_project_file_is_shown_instead_of_the_folder_name()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Dialogs.FolderAnswer = MakeProject(harness, "mymod", "{ \"name\": \"Great Mod\" }");

				await harness.Main.OpenProjectAsync();

				Assert.Equal("Project: Great Mod", harness.Main.ProjectText);
				Assert.StartsWith("Fallout Script Studio (Great Mod)", harness.Main.Title, StringComparison.Ordinal);
			});
		}

		[Fact]
		public void A_cancelled_folder_picker_opens_nothing()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Dialogs.FolderAnswer = null;

				await harness.Main.OpenProjectAsync();

				Assert.False(harness.Main.HasProject);
				Assert.Null(harness.Settings.Settings.Paths.SolutionFolder);
				Assert.Empty(harness.Main.RecentProjects);
			});
		}

		[Fact]
		public void Open_recent_project_opens_it_and_a_missing_one_is_reported_and_forgotten()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				String first = MakeProject(harness, "first");
				String second = MakeProject(harness, "second");
				await harness.Main.OpenRecentProjectAsync(first);
				await harness.Main.OpenRecentProjectAsync(second);
				Assert.Equal(new[] { second, first }, harness.Main.RecentProjects.ToArray());

				await harness.Main.OpenRecentProjectAsync(first);
				Assert.Equal(first, harness.Projects.Folder);
				Assert.Equal(new[] { first, second }, harness.Main.RecentProjects.ToArray());

				Directory.Delete(second, true);
				await harness.Main.OpenRecentProjectAsync(second);

				Assert.Equal(first, harness.Projects.Folder);
				Assert.Single(harness.Dialogs.Messages);
				Assert.Contains(second, harness.Dialogs.Messages[0], StringComparison.Ordinal);
				Assert.Equal(new[] { first }, harness.Main.RecentProjects.ToArray());
			});
		}

		[Fact]
		public void Clear_recent_projects_asks_first()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				await harness.Main.OpenRecentProjectAsync(MakeProject(harness, "p"));
				harness.Dialogs.ConfirmAnswer = false;

				await harness.Main.ClearRecentProjectsAsync();
				Assert.Single(harness.Main.RecentProjects);
				Assert.Single(harness.Dialogs.Confirmations);

				harness.Dialogs.ConfirmAnswer = true;
				await harness.Main.ClearRecentProjectsAsync();
				Assert.Empty(harness.Main.RecentProjects);
				Assert.Empty(harness.Projects.RecentProjects);
			});
		}

		[Fact]
		public void Close_project_gives_the_global_settings_back()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				await harness.Main.OpenRecentProjectAsync(MakeProject(harness, "mod", "{ \"shortCircuit\": true }"));
				Assert.True(harness.Settings.Effective.Settings.Compiler.ShortCircuit);

				harness.Main.CloseProjectCommand.Execute(null);

				Assert.False(harness.Main.HasProject);
				Assert.Equal("Project: <unset>", harness.Main.ProjectText);
				Assert.Equal("Fallout Script Studio", harness.Main.Title);
				Assert.False(harness.Settings.Effective.Settings.Compiler.ShortCircuit);
				Assert.Null(harness.Settings.Settings.Paths.SolutionFolder);
				Assert.False(harness.Main.CloseProjectCommand.CanExecute(null));
				Assert.Single(harness.Main.RecentProjects);
			});
		}

		[Fact]
		public void Create_project_settings_writes_the_file_with_the_values_in_effect_and_shows_the_project_settings()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				String folder = MakeProject(harness, "mod", null);
				harness.Settings.Update(settings => settings.Compiler.Optimization = OptimizationLevel.Full);
				await harness.Main.OpenRecentProjectAsync(folder);
				Assert.True(harness.Main.CreateProjectSettingsCommand.CanExecute(null));
				Assert.False(harness.Main.HasProjectSettings);

				await harness.Main.CreateProjectSettingsAsync();

				String json = File.ReadAllText(Path.Combine(folder, ProjectSettings.FileName));
				Assert.Contains("\"optimization\": \"full\"", json, StringComparison.Ordinal);
				Assert.True(harness.Main.HasProjectSettings);
				Assert.Contains("project-settings", harness.ToolWindows.Calls);
				Assert.Contains(ProjectSettings.FileName, harness.Output.StatusMessage, StringComparison.Ordinal);
				Assert.False(harness.Main.CreateProjectSettingsCommand.CanExecute(null));
			});
		}

		[Fact]
		public void Project_settings_without_a_project_asks_for_one_first_and_with_a_project_opens_the_window()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Dialogs.FolderAnswer = null;
				await harness.Main.ProjectSettingsAsync();
				Assert.DoesNotContain("project-settings", harness.ToolWindows.Calls);

				harness.Dialogs.FolderAnswer = MakeProject(harness, "mod");
				await harness.Main.ProjectSettingsAsync();
				Assert.True(harness.Main.HasProject);
				Assert.DoesNotContain("project-settings", harness.ToolWindows.Calls);

				await harness.Main.ProjectSettingsAsync();
				Assert.Contains("project-settings", harness.ToolWindows.Calls);
			});
		}

		[Fact]
		public void Opening_or_changing_a_project_parses_the_open_scripts_again()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				await harness.OpenFileAsync("a.ssl", Script);
				Assert.True(await Eventually.TrueAsync(() => harness.Parser.Started > 0));
				Int32 started = harness.Parser.Started;

				await harness.Main.OpenRecentProjectAsync(MakeProject(harness, "mod"));
				Assert.True(await Eventually.TrueAsync(() => harness.Parser.Started > started));
				started = harness.Parser.Started;

				ProjectSettings changed = harness.Projects.Settings!.Clone();
				changed.HeadersDirectory = harness.Folder.Combine("headers");
				Assert.True(harness.Projects.SaveSettings(changed));

				Assert.True(await Eventually.TrueAsync(() => harness.Parser.Started > started));
			});
		}

		[Fact]
		public void The_parser_options_follow_the_project()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Settings.Update(settings =>
				{
					settings.Compiler.PreprocessorDefine = "GLOBAL";
					settings.Compiler.BackwardCompatibility = false;
				});
				Assert.Equal("GLOBAL", ParserOptionsFactory.Create(harness.Settings).PreprocessorDefinitions);

				await harness.Main.OpenRecentProjectAsync(MakeProject(harness, "mod", "{ \"preprocessorDefine\": \"MOD\", \"backwardCompatibility\": true, \"headersDirectory\": \"hdr\", \"scriptsHeaderFile\": \"hdr/scripts.h\" }"));

				ParserOptions options = ParserOptionsFactory.Create(harness.Settings);
				Assert.Equal("MOD", options.PreprocessorDefinitions);
				Assert.Equal(1, options.BackwardCompatibilityMode);
				Assert.Equal(harness.Folder.Combine("mod", "hdr"), options.HeadersDirectory);
				Assert.Equal(harness.Folder.Combine("mod", "hdr"), options.GlobalMacroDirectory);

				harness.Main.CloseProjectCommand.Execute(null);
				Assert.Equal("GLOBAL", ParserOptionsFactory.Create(harness.Settings).PreprocessorDefinitions);
				Assert.Equal(0, ParserOptionsFactory.Create(harness.Settings).BackwardCompatibilityMode);
			});
		}

		[Fact]
		public void The_compiler_uses_the_output_folder_of_the_project()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Settings.Update(settings =>
				{
					settings.Compiler.CompileToSourceDirectory = false;
					settings.Paths.OutputDirectory = null;
				});
				DocumentViewModel before = await harness.OpenFileAsync("a.ssl", Script);
				Assert.False(await harness.Build.CompileAsync(before));
				Assert.Empty(harness.Compiler.Compiled);

				await harness.Main.OpenRecentProjectAsync(MakeProject(harness, "mod", "{ \"outputDirectory\": \"out\" }"));
				Assert.True(await harness.Build.CompileAsync(before));

				Assert.Single(harness.Compiler.Compiled);
				Assert.Equal(harness.Folder.Combine("mod", "out"), harness.Compiler.LastRequest!.Options.OutputDirectory);
				Assert.Null(harness.Settings.Settings.Paths.OutputDirectory);
			});
		}

		[Fact]
		public void The_toolbar_list_offers_the_project_definitions()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Settings.PreprocessorDefines.Items.Add("GLOBALDEF");

				await harness.Main.OpenRecentProjectAsync(MakeProject(harness, "mod", "{ \"defines\": [ \"MODDEF\" ] }"));

				Assert.Equal(new[] { String.Empty, "DEBUG", "RELEASE", "GLOBALDEF", "MODDEF" }, harness.Main.PreprocessorDefineItems);
				harness.Main.CloseProjectCommand.Execute(null);
				Assert.Equal(new[] { String.Empty, "DEBUG", "RELEASE", "GLOBALDEF" }, harness.Main.PreprocessorDefineItems);
			});
		}

		[Fact]
		public void Selecting_a_definition_changes_the_project_when_it_overrides_the_definition_and_the_global_setting_otherwise()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				String folder = MakeProject(harness, "mod", "{ \"preprocessorDefine\": \"\", \"defines\": [ \"MODDEF\" ] }");
				await harness.Main.OpenRecentProjectAsync(folder);
				await harness.OpenFileAsync("mod/a.ssl", Script);

				harness.Main.SelectedPreprocessorDefine = "MODDEF";

				Assert.Equal("MODDEF", harness.Settings.Effective.Settings.Compiler.PreprocessorDefine);
				Assert.Null(harness.Settings.Settings.Compiler.PreprocessorDefine);
				Assert.Contains("\"preprocessorDefine\": \"MODDEF\"", File.ReadAllText(Path.Combine(folder, ProjectSettings.FileName)), StringComparison.Ordinal);
				Assert.EndsWith("[MODDEF]", harness.Main.Title, StringComparison.Ordinal);
				Assert.Equal("MODDEF", harness.Main.SelectedPreprocessorDefine);

				harness.Main.SelectedPreprocessorDefine = String.Empty;
				Assert.Null(harness.Settings.Effective.Settings.Compiler.PreprocessorDefine);
				Assert.Contains("\"preprocessorDefine\": \"\"", File.ReadAllText(Path.Combine(folder, ProjectSettings.FileName)), StringComparison.Ordinal);

				// A project that does not override the definition leaves the choice to the global setting.
				String plain = MakeProject(harness, "plain", "{ }");
				await harness.Main.OpenRecentProjectAsync(plain);
				harness.Main.SelectedPreprocessorDefine = "DEBUG";
				Assert.Equal("DEBUG", harness.Settings.Settings.Compiler.PreprocessorDefine);
				Assert.Equal("{ }", File.ReadAllText(Path.Combine(plain, ProjectSettings.FileName)));
			});
		}

		[Fact]
		public void A_script_of_another_project_gets_a_hint_with_a_switch_link_and_never_a_dialog()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				String project = MakeProject(harness, "mod");

				await harness.OpenFileAsync("mod/scripts/a.ssl", Script);

				Problem hint = Assert.Single(harness.Problems.Problems, problem => problem.Origin == ProblemOrigins.ProjectDetection);
				Assert.Equal(ProblemSeverity.Info, hint.Severity);
				Assert.Equal(ProblemActionKind.SwitchProject, hint.Action!.Kind);
				Assert.Equal(project, hint.Action.FolderPath);
				Assert.Contains(project, hint.Message, StringComparison.Ordinal);
				Assert.Contains(project, harness.Output.StatusMessage, StringComparison.Ordinal);
				Assert.Empty(harness.Dialogs.Messages);
				Assert.Empty(harness.Dialogs.Confirmations);
				Assert.False(harness.Main.HasProject);
			});
		}

		[Fact]
		public void The_hint_is_offered_once_per_project_and_session()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				MakeProject(harness, "mod");
				MakeProject(harness, "other");
				DocumentViewModel first = await harness.OpenFileAsync("mod/a.ssl", Script);
				Assert.Single(harness.Problems.Problems, problem => problem.Origin == ProblemOrigins.ProjectDetection);

				harness.Problems.Clear();
				await harness.OpenFileAsync("mod/b.ssl", Script);
				await harness.Workspace.CloseAsync(first);
				await harness.OpenFileAsync("mod/a.ssl", Script);
				Assert.DoesNotContain(harness.Problems.Problems, problem => problem.Origin == ProblemOrigins.ProjectDetection);

				await harness.OpenFileAsync("other/c.ssl", Script);
				Assert.Single(harness.Problems.Problems, problem => problem.Origin == ProblemOrigins.ProjectDetection);
			});
		}

		[Fact]
		public void No_hint_for_scripts_of_the_active_project_or_without_a_project()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				await harness.OpenFileAsync("free/a.ssl", Script);
				String project = MakeProject(harness, "mod");
				await harness.Main.OpenRecentProjectAsync(project);

				await harness.OpenFileAsync("mod/sub/b.ssl", Script);

				Assert.DoesNotContain(harness.Problems.Problems, problem => problem.Origin == ProblemOrigins.ProjectDetection);
			});
		}

		[Fact]
		public void The_switch_link_opens_the_project_and_the_hint_goes_away()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				String project = MakeProject(harness, "mod", "{ \"shortCircuit\": true }");
				await harness.OpenFileAsync("mod/a.ssl", Script);
				ErrorItemViewModel row = Assert.Single(harness.Output.Errors, item => item.HasAction);
				Assert.Equal("Switch to this project", row.ActionLabel);

				await harness.Output.RunProblemActionCommand.ExecuteAsync(row);

				Assert.Equal(project, harness.Projects.Folder);
				Assert.True(harness.Settings.Effective.Settings.Compiler.ShortCircuit);
				Assert.DoesNotContain(harness.Problems.Problems, problem => problem.Origin == ProblemOrigins.ProjectDetection);
			});
		}

		[Fact]
		public void A_damaged_project_file_is_reported_as_a_problem_and_the_global_settings_stay_in_effect()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				String folder = MakeProject(harness, "mod", "{ not json");

				await harness.Main.OpenRecentProjectAsync(folder);

				Problem problem = Assert.Single(harness.Problems.Problems, item => item.Source == ProblemSource.Settings);
				Assert.Equal(ProblemSeverity.Warning, problem.Severity);
				Assert.Equal(Path.Combine(folder, ProjectSettings.FileName), problem.File);
				Assert.True(harness.Main.HasProject);
				Assert.False(harness.Main.HasProjectSettings);
				Assert.Same(harness.Settings.Settings, harness.Settings.Effective.Settings);
			});
		}

		[Fact]
		public void The_project_explorer_follows_the_project()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				String folder = MakeProject(harness, "mod");
				File.WriteAllText(Path.Combine(folder, "x.ssl"), Script);

				await harness.Main.OpenRecentProjectAsync(folder);

				Assert.True(harness.Main.Project.HasProject);
				Assert.Equal(folder, harness.Main.Project.ProjectFolder);
				Assert.NotEmpty(harness.Main.Project.Nodes);

				harness.Main.CloseProjectCommand.Execute(null);
				Assert.False(harness.Main.Project.HasProject);
			});
		}

		[Fact]
		public void Choosing_the_folder_in_the_explorer_is_a_project_switch_too()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				String folder = MakeProject(harness, "mod", "{ \"name\": \"Explorer Mod\" }");
				harness.Dialogs.FolderAnswer = folder;

				harness.Main.Project.ChooseFolder();

				Assert.Equal("Project: Explorer Mod", harness.Main.ProjectText);
				Assert.Equal(folder, harness.Projects.Folder);
				await Task.CompletedTask;
			});
		}
	}
}