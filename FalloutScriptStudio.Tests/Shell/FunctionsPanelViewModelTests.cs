namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Tests.Shell.Support;
	using FalloutScriptStudio.Core.Functions;
	using FalloutScriptStudio.ViewModels.Panels;
	using FalloutScriptStudio.Services.ScriptTools.Dialogs;
	using Xunit;

	public sealed class FunctionsPanelViewModelTests
	{
		private static FunctionNodeViewModel Find(ShellHarness harness, String name)
		{
			Func<FunctionNodeViewModel, System.Collections.Generic.IEnumerable<FunctionNodeViewModel>> flatten = null!;
			flatten = node => node.Children.SelectMany(flatten).Prepend(node);
			return harness.Functions.Roots.SelectMany(flatten).First(n => n.Name == name);
		}

		[Fact]
		public void The_tree_combines_the_built_in_and_user_files()
		{
			using ShellHarness harness = new ShellHarness();

			Assert.Equal(new[] { "Group", "User" }, harness.Functions.Roots.Select(r => r.Name));
			Assert.False(Find(harness, "wait").IsUser);
			Assert.True(Find(harness, "mine").IsUser);
		}

		[Fact]
		public void Inserting_a_function_puts_the_code_at_the_caret_and_places_the_caret_on_the_first_placeholder()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", "   x := ;\r\n");
				FakeEditorSurface editor = harness.AttachEditor(document);
				editor.CaretOffset = 3;

				harness.Functions.Insert(Find(harness, "wait"));

				Assert.Equal("   wait({int time}); x := ;\r\n", document.TextDocument.Text);
				Assert.Equal(3 + "wait(".Length, editor.SelectionStart);
				Assert.Equal("{int time}".Length, editor.SelectionLength);
				Assert.Contains("focus", editor.Calls);
			});
		}

		[Fact]
		public void Inserting_over_a_selection_replaces_it_without_padding()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", "abc def");
				FakeEditorSurface editor = harness.AttachEditor(document);
				editor.Select(0, 3);

				harness.Functions.Insert(Find(harness, "mine"));

				Assert.Equal("mine; def", document.TextDocument.Text);
			});
		}

		[Fact]
		public void Groups_cannot_be_inserted()
		{
			using ShellHarness harness = new ShellHarness();

			harness.Functions.Insert(Find(harness, "Group"));

			Assert.Empty(harness.Workspace.Documents);
		}

		[Fact]
		public void The_available_actions_depend_on_the_selected_node()
		{
			using ShellHarness harness = new ShellHarness();

			harness.Functions.SelectedNode = Find(harness, "wait");
			Assert.True(harness.Functions.CanEditFunction);
			Assert.False(harness.Functions.CanAddFunction);
			Assert.False(harness.Functions.CanDeleteNode);

			harness.Functions.SelectedNode = Find(harness, "User");
			Assert.False(harness.Functions.CanEditFunction);
			Assert.True(harness.Functions.CanAddFunction);
			Assert.True(harness.Functions.CanAddGroup);
			Assert.True(harness.Functions.CanRenameGroup);
			Assert.False(harness.Functions.CanDeleteNode);

			harness.Functions.SelectedNode = Find(harness, "mine");
			Assert.True(harness.Functions.CanDeleteNode);
			Assert.True(harness.Functions.CanEditFunction);
		}

		[Fact]
		public void Adding_a_user_function_updates_the_file_and_the_tree()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Functions.SelectedNode = Find(harness, "mine");
				harness.DialogueDialogs.Answer = new FunctionEditResult(new FunctionDefinition("extra", "Extra function", "extra();"), false);

				await harness.Functions.AddFunctionAsync();

				FunctionNodeViewModel user = Find(harness, "User");
				Assert.Contains(user.Children, c => c.Name == "extra");
				Assert.Contains(user.Children, c => c.Name == "mine");
				Assert.True(harness.DialogueDialogs.LastIsNew);
				String file = File.ReadAllText(Path.Combine(harness.Paths.DescriptionsDirectory, "userfunctions.ini"));
				Assert.Contains("extra<d>Extra function<s>extra();", file, StringComparison.Ordinal);
			});
		}

		[Fact]
		public void The_add_to_highlight_choice_of_the_dialog_reaches_the_keyword_store()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				using UserKeywordStore keywords = new UserKeywordStore(Path.Combine(harness.Paths.SettingsDirectory, "user-keywords.json"));
				using FunctionCatalogService catalog = new FunctionCatalogService(
					new FunctionCatalogPaths(Path.Combine(harness.Paths.DescriptionsDirectory, "functions.ini"), Path.Combine(harness.Paths.DescriptionsDirectory, "userfunctions.ini")),
					keywords);
				await catalog.LoadAsync();
				FunctionsPanelViewModel panel = new FunctionsPanelViewModel(catalog, harness.Tracker, harness.Dialogs, harness.DialogueDialogs);
				panel.SelectedNode = panel.Roots.First(r => r.Name == "User");
				harness.DialogueDialogs.Answer = new FunctionEditResult(new FunctionDefinition("extra", "Extra function", "EXTRA_MACRO({x})"), true);

				await panel.AddFunctionAsync();

				Assert.Equal(new[] { "EXTRA_MACRO" }, keywords.Keywords.ToArray());
			});
		}

		[Fact]
		public void A_failed_change_is_explained_in_the_language_of_the_interface()
		{
			Assert.Equal("This operation is not allowed for the selected item.", FunctionsPanelViewModel.DescribeFailure(FunctionOperationResult.Fail(FunctionOperationError.NotAllowed, "english core text")));
			Assert.Equal("The name, description or code is not valid.", FunctionsPanelViewModel.DescribeFailure(FunctionOperationResult.Fail(FunctionOperationError.InvalidDefinition, "english core text")));
			Assert.Contains("Reload the tree", FunctionsPanelViewModel.DescribeFailure(FunctionOperationResult.Fail(FunctionOperationError.NodeChanged, "english core text")), StringComparison.Ordinal);
			Assert.EndsWith("denied", FunctionsPanelViewModel.DescribeFailure(FunctionOperationResult.Fail(FunctionOperationError.IoError, "denied")), StringComparison.Ordinal);
		}

		[Fact]
		public void Cancelling_the_function_dialog_changes_nothing()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Functions.SelectedNode = Find(harness, "User");
				harness.DialogueDialogs.Answer = null;

				await harness.Functions.AddFunctionAsync();

				Assert.Single(Find(harness, "User").Children);
			});
		}

		[Fact]
		public void Editing_a_user_function_updates_file_and_node()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				FunctionNodeViewModel mine = Find(harness, "mine");
				harness.Functions.SelectedNode = mine;
				harness.DialogueDialogs.Answer = new FunctionEditResult(new FunctionDefinition("mine2", "Changed", "mine2;"), false);

				await harness.Functions.EditFunctionAsync();

				FunctionNodeViewModel edited = Find(harness, "mine2");
				Assert.Equal("Changed", edited.Description);
				Assert.Equal("mine2;", edited.Node.Code);
				Assert.Equal("mine", harness.DialogueDialogs.LastInitial!.Name);
				Assert.False(harness.DialogueDialogs.LastIsNew);
				Assert.Contains("mine2<d>Changed<s>mine2;", File.ReadAllText(Path.Combine(harness.Paths.DescriptionsDirectory, "userfunctions.ini")), StringComparison.Ordinal);
			});
		}

		[Fact]
		public void Editing_a_built_in_function_asks_first()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				FunctionNodeViewModel wait = Find(harness, "wait");
				harness.Functions.SelectedNode = wait;
				harness.DialogueDialogs.Answer = new FunctionEditResult(new FunctionDefinition("wait", "Changed", "wait();"), false);
				harness.Dialogs.ConfirmAnswer = false;

				await harness.Functions.EditFunctionAsync();

				Assert.Equal("Waits", Find(harness, "wait").Description);

				harness.Dialogs.ConfirmAnswer = true;
				await harness.Functions.EditFunctionAsync();
				Assert.Equal("Changed", Find(harness, "wait").Description);
				Assert.Contains("wait<d>Changed<s>wait();", File.ReadAllText(Path.Combine(harness.Paths.DescriptionsDirectory, "functions.ini")), StringComparison.Ordinal);
			});
		}

		[Fact]
		public void Groups_can_be_added_renamed_and_deleted()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				FunctionNodeViewModel user = Find(harness, "User");
				harness.Functions.SelectedNode = user;
				harness.Dialogs.PromptAnswer = "Fresh";

				await harness.Functions.AddGroupAsync();
				FunctionNodeViewModel fresh = Find(harness, "Fresh");
				Assert.Equal("User", fresh.Parent!.Name);

				harness.Functions.SelectedNode = fresh;
				harness.Dialogs.PromptAnswer = "Renamed";
				await harness.Functions.RenameGroupAsync();
				Assert.Equal("Renamed", Find(harness, "Renamed").Name);
				harness.Functions.SelectedNode = Find(harness, "Renamed");

				harness.Dialogs.ConfirmAnswer = true;
				await harness.Functions.DeleteNodeAsync();
				Assert.DoesNotContain(Find(harness, "User").Children, c => c.Name == "Renamed");
				Assert.DoesNotContain("Renamed", File.ReadAllText(Path.Combine(harness.Paths.DescriptionsDirectory, "userfunctions.ini")), StringComparison.Ordinal);
			});
		}

		[Fact]
		public void Deleting_a_function_needs_confirmation()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				FunctionNodeViewModel mine = Find(harness, "mine");
				harness.Functions.SelectedNode = mine;
				harness.Dialogs.ConfirmAnswer = false;

				await harness.Functions.DeleteNodeAsync();
				Assert.Contains(Find(harness, "User").Children, c => c.Name == "mine");

				harness.Dialogs.ConfirmAnswer = true;
				await harness.Functions.DeleteNodeAsync();
				Assert.Empty(Find(harness, "User").Children);
			});
		}
	}
}
