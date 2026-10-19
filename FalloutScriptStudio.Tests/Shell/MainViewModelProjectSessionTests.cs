namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Problems;
	using FalloutScriptStudio.Tests.Shell.Support;
	using FalloutScriptStudio.ViewModels.Main;
	using Xunit;

	/// <summary>
	/// The documents that were open in a project are remembered when the project is left or the program ends and opened again when the
	/// project is opened: at once when nothing is open, with a hint otherwise, never when the option is off, never without a project.
	/// </summary>
	public sealed class MainViewModelProjectSessionTests
	{
		private const String Script = "procedure start begin\r\nend\r\n";

		private static String ProjectFolder(ShellHarness harness)
		{
			String folder = harness.Folder.Combine("mod");
			Directory.CreateDirectory(folder);
			return folder;
		}

		private static void Remember(ShellHarness harness, String projectFolder, String[] documents, String? active)
		{
			String[] full = documents.Select(path => harness.Folder.Write(path, Script)).ToArray();
			harness.Settings.Recent.SetProjectSession(ProjectSession.Create(projectFolder, full, active is null ? null : Path.Combine(harness.Folder.Path, active)));
		}

		private static async Task WaitForAsync(Func<Boolean> condition, String what)
		{
			for (Int32 attempt = 0; attempt < 2400 && !condition(); attempt++)
			{
				await Task.Delay(25);
			}

			Assert.True(condition(), "Timed out waiting for: " + what);
		}

		[Fact]
		public void Closing_the_project_remembers_the_open_documents_of_the_project_with_relative_paths_and_the_active_one()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				String folder = ProjectFolder(harness);
				await harness.Main.OpenRecentProjectAsync(folder);
				await harness.OpenFileAsync("mod/scripts/a.ssl", Script);
				DocumentViewModel second = await harness.OpenFileAsync("mod/b.h", "#define X 1\r\n");
				await harness.OpenFileAsync("elsewhere/c.ssl", Script);
				harness.Workspace.CreateNew();
				harness.Workspace.ActiveDocument = second;

				harness.Main.CloseProjectCommand.Execute(null);

				ProjectSession session = Assert.Single(harness.Settings.Recent.ProjectSessions);
				Assert.Equal(folder, session.Folder);
				Assert.Equal(new[] { "scripts/a.ssl", "b.h" }, session.Documents);
				Assert.Equal("b.h", session.ActiveDocument);
			});
		}

		[Fact]
		public void Leaving_a_project_for_another_remembers_the_documents_of_the_first_one()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				String first = ProjectFolder(harness);
				String second = harness.Folder.Combine("other");
				Directory.CreateDirectory(second);
				await harness.Main.OpenRecentProjectAsync(first);
				await harness.OpenFileAsync("mod/a.ssl", Script);

				await harness.Main.OpenRecentProjectAsync(second);

				ProjectSession session = Assert.Single(harness.Settings.Recent.ProjectSessions);
				Assert.Equal(first, session.Folder);
				Assert.Equal(new[] { "a.ssl" }, session.Documents);
			});
		}

		[Fact]
		public void Ending_the_program_remembers_the_documents_before_they_are_closed()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				String folder = ProjectFolder(harness);
				await harness.Main.OpenRecentProjectAsync(folder);
				await harness.OpenFileAsync("mod/a.ssl", Script);
				await harness.OpenFileAsync("mod/b.ssl", Script);

				Assert.True(await harness.Main.CanCloseAsync());

				Assert.Empty(harness.Workspace.Documents);
				ProjectSession session = Assert.Single(harness.Settings.Recent.ProjectSessions);
				Assert.Equal(new[] { "a.ssl", "b.ssl" }, session.Documents);
				Assert.Equal("b.ssl", session.ActiveDocument);
			});
		}

		[Fact]
		public void Leaving_a_project_where_no_document_was_opened_keeps_the_session_it_had()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Settings.Update(settings => settings.Editor.RestoreProjectDocuments = false);
				String first = ProjectFolder(harness);
				String second = harness.Folder.Combine("other");
				Directory.CreateDirectory(second);
				Remember(harness, first, new[] { @"mod\a.ssl", @"mod\b.ssl" }, @"mod\b.ssl");
				await harness.Main.OpenRecentProjectAsync(first);

				await harness.Main.OpenRecentProjectAsync(second);

				ProjectSession session = Assert.Single(harness.Settings.Recent.ProjectSessions);
				Assert.Equal(first, session.Folder);
				Assert.Equal(new[] { "a.ssl", "b.ssl" }, session.Documents);
				Assert.Equal("b.ssl", session.ActiveDocument);
			});
		}

		[Fact]
		public void Closing_a_project_where_no_document_was_opened_keeps_the_session_it_had()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Settings.Update(settings => settings.Editor.RestoreProjectDocuments = false);
				String folder = ProjectFolder(harness);
				Remember(harness, folder, new[] { @"mod\a.ssl" }, null);
				await harness.Main.OpenRecentProjectAsync(folder);

				harness.Main.CloseProjectCommand.Execute(null);

				Assert.Equal(new[] { "a.ssl" }, Assert.Single(harness.Settings.Recent.ProjectSessions).Documents);
			});
		}

		[Fact]
		public void Ending_the_program_without_having_opened_a_document_of_the_project_keeps_the_session_it_had()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Settings.Update(settings => settings.Editor.RestoreProjectDocuments = false);
				String folder = ProjectFolder(harness);
				Remember(harness, folder, new[] { @"mod\a.ssl", @"mod\b.ssl" }, null);
				await harness.Main.OpenRecentProjectAsync(folder);
				await harness.OpenFileAsync("work/w.ssl", Script);

				Assert.True(await harness.Main.CanCloseAsync());

				Assert.Equal(new[] { "a.ssl", "b.ssl" }, Assert.Single(harness.Settings.Recent.ProjectSessions).Documents);
			});
		}

		[Fact]
		public void Documents_of_another_project_that_stay_open_do_not_replace_the_session_of_the_project_that_is_left()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Settings.Update(settings => settings.Editor.RestoreProjectDocuments = false);
				String first = ProjectFolder(harness);
				String second = harness.Folder.Combine("other");
				Directory.CreateDirectory(second);
				Remember(harness, second, new[] { @"other\x.ssl" }, null);
				await harness.Main.OpenRecentProjectAsync(first);
				await harness.OpenFileAsync("mod/a.ssl", Script);
				await harness.Main.OpenRecentProjectAsync(second);
				await harness.OpenFileAsync("work/w.ssl", Script);

				harness.Main.CloseProjectCommand.Execute(null);

				ProjectSession session = Assert.Single(harness.Settings.Recent.ProjectSessions, item => String.Equals(item.Folder, second, StringComparison.OrdinalIgnoreCase));
				Assert.Equal(new[] { "x.ssl" }, session.Documents);
			});
		}

		[Fact]
		public void Closing_every_document_of_the_project_during_the_visit_does_replace_the_session()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Settings.Update(settings => settings.Editor.RestoreProjectDocuments = false);
				String folder = ProjectFolder(harness);
				Remember(harness, folder, new[] { @"mod\a.ssl" }, null);
				await harness.Main.OpenRecentProjectAsync(folder);
				await harness.OpenFileAsync("mod/a.ssl", Script);
				await harness.Workspace.CloseAllAsync();

				harness.Main.CloseProjectCommand.Execute(null);

				Assert.Empty(harness.Settings.Recent.ProjectSessions);
			});
		}

		[Fact]
		public void Documents_of_the_project_that_were_open_before_the_project_became_active_belong_to_the_visit()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Settings.Update(settings => settings.Editor.RestoreProjectDocuments = false);
				String folder = ProjectFolder(harness);
				Remember(harness, folder, new[] { @"mod\old.ssl" }, null);
				await harness.OpenFileAsync("mod/a.ssl", Script);
				await harness.Main.OpenRecentProjectAsync(folder);

				harness.Main.CloseProjectCommand.Execute(null);

				Assert.Equal(new[] { "a.ssl" }, Assert.Single(harness.Settings.Recent.ProjectSessions).Documents);
			});
		}

		[Fact]
		public void Without_a_project_nothing_is_remembered_and_the_documents_behave_as_before()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				await harness.OpenFileAsync("loose/a.ssl", Script);

				Assert.True(await harness.Main.CanCloseAsync());

				Assert.Empty(harness.Settings.Recent.ProjectSessions);
				Assert.Empty(harness.Problems.Problems);
			});
		}

		[Fact]
		public void Opening_a_project_with_nothing_open_opens_its_remembered_documents_in_the_background_and_activates_the_remembered_one()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				String folder = ProjectFolder(harness);
				Remember(harness, folder, new[] { @"mod\a.ssl", @"mod\sub\b.ssl", @"mod\c.h" }, @"mod\sub\b.ssl");

				await harness.Main.OpenRecentProjectAsync(folder);
				await WaitForAsync(() => harness.Workspace.Documents.Count == 3, "the documents");
				await WaitForAsync(() => harness.Output.StatusMessage == "3 file(s) of the project opened again.", "the notice");

				Assert.Equal(new[] { "a.ssl", "b.ssl", "c.h" }, harness.Workspace.Documents.Select(document => document.FileName));
				Assert.Equal("b.ssl", harness.Workspace.ActiveDocument!.FileName);
				Assert.Empty(harness.Settings.Recent.Scripts);
				Assert.DoesNotContain(harness.Problems.Problems, problem => problem.Origin == ProblemOrigins.ProjectSession);
			});
		}

		[Fact]
		public void Missing_files_are_skipped_silently()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				String folder = ProjectFolder(harness);
				Remember(harness, folder, new[] { @"mod\a.ssl", @"mod\gone.ssl", @"mod\b.ssl" }, @"mod\gone.ssl");
				File.Delete(harness.Folder.Combine("mod", "gone.ssl"));

				await harness.Main.OpenRecentProjectAsync(folder);
				await WaitForAsync(() => harness.Workspace.Documents.Count == 2, "the documents");
				await WaitForAsync(() => harness.Output.StatusMessage == "2 file(s) of the project opened again.", "the notice");

				Assert.Equal(new[] { "a.ssl", "b.ssl" }, harness.Workspace.Documents.Select(document => document.FileName));
				Assert.Equal("b.ssl", harness.Workspace.ActiveDocument!.FileName);
				Assert.Empty(harness.Problems.Problems);
				Assert.Empty(harness.Dialogs.Messages);
			});
		}

		[Fact]
		public void With_documents_already_open_a_hint_offers_the_restoration_instead_of_doing_it()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				String folder = ProjectFolder(harness);
				Remember(harness, folder, new[] { @"mod\a.ssl", @"mod\b.ssl" }, @"mod\a.ssl");
				DocumentViewModel working = await harness.OpenFileAsync("work/w.ssl", Script);

				await harness.Main.OpenRecentProjectAsync(folder);
				await WaitForAsync(() => harness.Problems.Problems.Any(problem => problem.Origin == ProblemOrigins.ProjectSession), "the hint");

				Assert.Same(working, Assert.Single(harness.Workspace.Documents));
				Problem hint = harness.Problems.Problems.Single(problem => problem.Origin == ProblemOrigins.ProjectSession);
				Assert.Equal(ProblemSeverity.Info, hint.Severity);
				Assert.Equal(ProblemActionKind.RestoreDocuments, hint.Action!.Kind);
				Assert.Equal("Restore open files", hint.Action.Label);
				Assert.Equal(folder, hint.Action.FolderPath);
				Assert.Contains("2 file(s)", hint.Message, StringComparison.Ordinal);
				Assert.Contains("mod", hint.Message, StringComparison.Ordinal);

				ErrorItemViewModel row = harness.Output.Errors.Single(item => item.Action?.Kind == ProblemActionKind.RestoreDocuments);
				await harness.Output.RunProblemActionAsync(row);

				Assert.Equal(new[] { "w.ssl", "a.ssl", "b.ssl" }, harness.Workspace.Documents.Select(document => document.FileName));
				Assert.Equal("a.ssl", harness.Workspace.ActiveDocument!.FileName);
				Assert.DoesNotContain(harness.Problems.Problems, problem => problem.Origin == ProblemOrigins.ProjectSession);
				Assert.Equal("2 file(s) of the project opened again.", harness.Output.StatusMessage);
			});
		}

		[Fact]
		public void The_hint_goes_away_when_the_project_is_left()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				String folder = ProjectFolder(harness);
				Remember(harness, folder, new[] { @"mod\a.ssl" }, null);
				await harness.OpenFileAsync("work/w.ssl", Script);
				await harness.Main.OpenRecentProjectAsync(folder);
				await WaitForAsync(() => harness.Problems.Problems.Any(problem => problem.Origin == ProblemOrigins.ProjectSession), "the hint");

				harness.Main.CloseProjectCommand.Execute(null);

				Assert.DoesNotContain(harness.Problems.Problems, problem => problem.Origin == ProblemOrigins.ProjectSession);
			});
		}

		[Fact]
		public void With_the_option_off_nothing_is_restored_and_nothing_is_offered()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Settings.Update(settings => settings.Editor.RestoreProjectDocuments = false);
				String folder = ProjectFolder(harness);
				Remember(harness, folder, new[] { @"mod\a.ssl" }, null);

				await harness.Main.OpenRecentProjectAsync(folder);
				await Task.Delay(200);
				Assert.Empty(harness.Workspace.Documents);

				String second = harness.Folder.Combine("other");
				Directory.CreateDirectory(second);
				Remember(harness, second, new[] { @"other\x.ssl" }, null);
				await harness.OpenFileAsync("work/w.ssl", Script);
				await harness.Main.OpenRecentProjectAsync(second);
				await Task.Delay(200);

				Assert.Equal("w.ssl", Assert.Single(harness.Workspace.Documents).FileName);
				Assert.Empty(harness.Problems.Problems);
			});
		}

		[Fact]
		public void A_project_without_remembered_documents_opens_as_before()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				String folder = ProjectFolder(harness);

				await harness.Main.OpenRecentProjectAsync(folder);
				await Task.Delay(200);

				Assert.Empty(harness.Workspace.Documents);
				Assert.Empty(harness.Problems.Problems);
				Assert.Equal(folder, harness.Projects.Folder);
			});
		}

		[Fact]
		public void Reopening_the_open_project_or_reloading_its_settings_does_not_restore_again()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				String folder = ProjectFolder(harness);
				File.WriteAllText(Path.Combine(folder, ProjectSettings.FileName), "{ }");
				Remember(harness, folder, new[] { @"mod\a.ssl" }, null);
				await harness.Main.OpenRecentProjectAsync(folder);
				await WaitForAsync(() => harness.Workspace.Documents.Count == 1, "the document");
				await harness.Workspace.CloseAllAsync();
				Remember(harness, folder, new[] { @"mod\a.ssl" }, null);

				await harness.Main.OpenRecentProjectAsync(folder);
				harness.Projects.Reload();
				await Task.Delay(200);

				Assert.Empty(harness.Workspace.Documents);
			});
		}

		[Fact]
		public void A_project_that_is_open_when_the_program_starts_gets_its_documents_back()
		{
			ShellStaRunner.Run(async () =>
			{
				String? folder = null;
				using ShellHarness harness = new ShellHarness(null, settings =>
				{
					String root = Directory.GetParent(settings.Paths.ProgramDirectory)!.FullName;
					folder = Path.Combine(root, "mod");
					Directory.CreateDirectory(folder);
					File.WriteAllText(Path.Combine(root, "mod", "a.ssl"), Script);
					File.WriteAllText(Path.Combine(root, "mod", "b.ssl"), Script);
					settings.Update(options => options.Paths.SolutionFolder = folder);
					settings.Recent.SetProjectSession(ProjectSession.Create(folder, new[] { Path.Combine(folder, "a.ssl"), Path.Combine(folder, "b.ssl") }, Path.Combine(folder, "a.ssl")));
				});
				Assert.Equal(folder, harness.Projects.Folder);
				Assert.Empty(harness.Workspace.Documents);

				await harness.Main.RestoreStartupProjectAsync();

				Assert.Equal(new[] { "a.ssl", "b.ssl" }, harness.Workspace.Documents.Select(document => document.FileName));
				Assert.Equal("a.ssl", harness.Workspace.ActiveDocument!.FileName);
			});
		}

		[Fact]
		public void Without_an_open_project_the_start_up_restoration_does_nothing()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();

				await harness.Main.RestoreStartupProjectAsync();

				Assert.Empty(harness.Workspace.Documents);
				Assert.Empty(harness.Problems.Problems);
			});
		}
	}
}