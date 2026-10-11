namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Tests.Shell.Support;
	using FalloutScriptStudio.ViewModels.Panels;
	using FalloutScriptStudio.Workspace.Project;
	using Xunit;

	public sealed class ProjectExplorerTests
	{
		private static TempFolder CreateProject()
		{
			TempFolder folder = new TempFolder();
			folder.Write("scripts/a.ssl", "// a");
			folder.Write("scripts/b.int", "bin");
			folder.Write("scripts/notes.txt", "x");
			folder.Write("scripts/sub/deep.ssl", "// deep");
			folder.Write("scripts/sub/readme.md", "x");
			folder.Write("headers/define.h", "#define A 1");
			folder.Write(".git/config", "x");
			folder.Write("dialog/npc.msg", "{1}{}{x}");
			folder.Write("dialog/npc.fcd", "x");
			return folder;
		}

		[Fact]
		public void Only_script_related_files_are_listed()
		{
			using TempFolder folder = CreateProject();
			ProjectFileFilter filter = new ProjectFileFilter(false);

			String[] files = filter.GetFiles(folder.Combine("scripts")).Select(Path.GetFileName).ToArray()!;

			Assert.Equal(new[] { "a.ssl" }, files);
			Assert.Equal(new[] { "dialog", "headers", "scripts" }, filter.GetDirectories(folder.Path).Select(Path.GetFileName));
		}

		[Fact]
		public void Compiled_scripts_are_listed_on_request()
		{
			using TempFolder folder = CreateProject();

			String[] files = new ProjectFileFilter(true).GetFiles(folder.Combine("scripts")).Select(Path.GetFileName).ToArray()!;

			Assert.Equal(new[] { "a.ssl", "b.int" }, files);
		}

		[Fact]
		public void The_dialog_files_are_listed()
		{
			using TempFolder folder = CreateProject();

			Assert.Equal(new[] { "npc.fcd", "npc.msg" }, new ProjectFileFilter(false).GetFiles(folder.Combine("dialog")).Select(Path.GetFileName));
		}

		[Fact]
		public void Searching_finds_files_by_name_below_the_root()
		{
			using TempFolder folder = CreateProject();

			String[] found = new ProjectFileFilter(false).Search(folder.Path, "DE").Select(Path.GetFileName).OrderBy(n => n).ToArray()!;

			Assert.Equal(new[] { "deep.ssl", "define.h" }, found);
		}

		[Fact]
		public void Hidden_folders_are_skipped_by_the_search()
		{
			using TempFolder folder = CreateProject();

			Assert.Empty(new ProjectFileFilter(false).Search(folder.Path, "config"));
		}

		[Fact]
		public void A_missing_folder_gives_empty_lists()
		{
			ProjectFileFilter filter = new ProjectFileFilter(false);

			Assert.Empty(filter.GetFiles(@"C:\definitely\not\here"));
			Assert.Empty(filter.GetDirectories(@"C:\definitely\not\here"));
		}

		[Fact]
		public void Folder_nodes_load_their_children_when_expanded()
		{
			using TempFolder folder = CreateProject();
			ProjectNodeViewModel node = new ProjectNodeViewModel(folder.Combine("scripts"), true, new ProjectFileFilter(false));

			Assert.Single(node.Children);
			Assert.Equal("...", node.Children[0].Name);
			node.IsExpanded = true;

			Assert.Equal(new[] { "sub", "a.ssl" }, node.Children.Select(c => c.Name));
			Assert.True(node.Children[0].IsDirectory);
			Assert.Equal(DocumentKind.Script, node.Children[1].Kind);
		}

		[Fact]
		public void The_explorer_lists_the_project_folder_and_filters_by_name()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				using TempFolder project = CreateProject();
				harness.Settings.Update(settings => settings.Paths.SolutionFolder = project.Path);
				ProjectExplorerViewModel explorer = new ProjectExplorerViewModel(harness.Settings, harness.Workspace, harness.Dialogs, new FakeShellService());

				Assert.True(explorer.HasProject);
				ProjectNodeViewModel root = Assert.Single(explorer.Nodes);
				Assert.True(root.IsExpanded);
				Assert.Equal(new[] { "dialog", "headers", "scripts" }, root.Children.Select(c => c.Name));

				explorer.FilterText = "npc";
				Assert.Equal(new[] { "npc.fcd", "npc.msg" }, explorer.Nodes.Select(n => n.Name).OrderBy(n => n));

				explorer.FilterText = String.Empty;
				Assert.Single(explorer.Nodes);
				await Task.CompletedTask;
			});
		}

		[Fact]
		public void Opening_a_file_node_opens_it_in_a_tab()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				using TempFolder project = CreateProject();
				harness.Settings.Update(settings => settings.Paths.SolutionFolder = project.Path);
				ProjectExplorerViewModel explorer = new ProjectExplorerViewModel(harness.Settings, harness.Workspace, harness.Dialogs, new FakeShellService());
				ProjectNodeViewModel file = new ProjectNodeViewModel(project.Combine("scripts", "a.ssl"), false, new ProjectFileFilter(false));

				await explorer.OpenAsync(file);

				Assert.NotNull(harness.Workspace.FindDocument(file.FullPath));
			});
		}

		[Fact]
		public void The_project_folder_can_be_chosen_and_the_tree_follows_the_setting()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				using TempFolder project = CreateProject();
				ProjectExplorerViewModel explorer = new ProjectExplorerViewModel(harness.Settings, harness.Workspace, harness.Dialogs, new FakeShellService());
				Assert.False(explorer.HasProject);
				harness.Dialogs.FolderAnswer = project.Path;

				explorer.ChooseFolder();

				Assert.Equal(project.Path, harness.Settings.Settings.Paths.SolutionFolder);
				Assert.True(explorer.HasProject);
				await Task.CompletedTask;
			});
		}
	}
}
