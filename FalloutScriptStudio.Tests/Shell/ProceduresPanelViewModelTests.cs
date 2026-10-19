namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.TextTools;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Services.ScriptTools;
	using FalloutScriptStudio.Services.ScriptTools.Procedures;
	using FalloutScriptStudio.Tests.Shell.Support;
	using FalloutScriptStudio.ViewModels.Panels;
	using Xunit;

	public sealed class ProceduresPanelViewModelTests
	{
		private const String Script =
			"procedure start;\r\n" +
			"procedure helper;\r\n" +
			"procedure talk_p_proc;\r\n" +
			"\r\n" +
			"procedure start begin\r\n" +
			"   call helper;\r\n" +
			"end\r\n" +
			"\r\n" +
			"procedure helper begin\r\n" +
			"end\r\n" +
			"\r\n" +
			"procedure talk_p_proc begin\r\n" +
			"end\r\n";

		private static async Task<(ShellHarness Harness, DocumentViewModel Document)> CreateAsync()
		{
			ShellHarness harness = new ShellHarness();
			DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
			await harness.Workspace.RequestParseAsync(document, true);
			return (harness, document);
		}

		private static ProcedureNodeViewModel Node(ShellHarness harness, String name)
		{
			return harness.Procedures.Groups.SelectMany(g => g.Children).OfType<ProcedureNodeViewModel>().Single(n => n.Procedure.Name == name);
		}

		[Fact]
		public void The_tree_is_built_when_the_document_is_parsed_and_cleared_without_a_document()
		{
			ShellStaRunner.Run(async () =>
			{
				(ShellHarness harness, DocumentViewModel document) = await CreateAsync();
				using ShellHarness _h = harness;

				Assert.True(harness.Procedures.HasData);
				Assert.Equal(new[] { "start", "helper", "talk_p_proc" }, harness.Procedures.Groups.Single(g => g.IsLocal).Children.Select(c => c.Title));

				await harness.Workspace.CloseAsync(document);
				Assert.False(harness.Procedures.HasData);
				Assert.Empty(harness.Procedures.Groups);
			});
		}

		[Fact]
		public void Header_documents_show_the_procedures_too_but_messages_do_not()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				await harness.OpenFileAsync("a.msg", "{100}{}{x}\r\n");

				Assert.False(harness.Procedures.HasData);
			});
		}

		[Fact]
		public void Navigating_to_a_procedure_goes_to_its_body()
		{
			ShellStaRunner.Run(async () =>
			{
				(ShellHarness harness, DocumentViewModel document) = await CreateAsync();
				using ShellHarness _h = harness;
				FakeEditorSurface editor = harness.AttachEditor(document);

				await harness.Procedures.NavigateAsync(Node(harness, "helper"));

				Assert.Equal(9, editor.LastGoTo!.Value.Line);
			});
		}

		[Fact]
		public void Inserting_a_name_puts_it_into_the_script_at_the_caret()
		{
			ShellStaRunner.Run(async () =>
			{
				(ShellHarness harness, DocumentViewModel document) = await CreateAsync();
				using ShellHarness _h = harness;
				harness.AttachEditor(document);

				harness.Procedures.InsertName(Node(harness, "helper"));

				Assert.StartsWith("helper", document.TextDocument.Text, StringComparison.Ordinal);
			});
		}

		[Fact]
		public void The_procedure_at_the_caret_is_highlighted()
		{
			ShellStaRunner.Run(async () =>
			{
				(ShellHarness harness, DocumentViewModel document) = await CreateAsync();
				using ShellHarness _h = harness;
				FakeEditorSurface editor = harness.AttachEditor(document);

				editor.GoTo(10);
				Assert.True(Node(harness, "helper").IsCurrent);
				Assert.False(Node(harness, "start").IsCurrent);

				editor.GoTo(6);
				Assert.True(Node(harness, "start").IsCurrent);
				Assert.False(Node(harness, "helper").IsCurrent);

				editor.CurrentProcedure = document.ProgramInfo!.GetProcedure("talk_p_proc");
				editor.RaiseProcedureChanged();
				Assert.True(Node(harness, "talk_p_proc").IsCurrent);
			});
		}

		[Fact]
		public void Expansion_and_selection_survive_a_rebuild()
		{
			ShellStaRunner.Run(async () =>
			{
				(ShellHarness harness, DocumentViewModel document) = await CreateAsync();
				using ShellHarness _h = harness;
				ProcedureNodeViewModel start = Node(harness, "start");
				start.IsExpanded = true;
				Assert.True(harness.Procedures.Groups.Single(g => g.IsLocal).IsExpanded);
				harness.Procedures.Groups.Single(g => g.IsLocal).IsExpanded = false;
				harness.Procedures.SelectedNode = Node(harness, "helper");

				await harness.Workspace.RequestParseAsync(document, true);

				Assert.False(harness.Procedures.Groups.Single(g => g.IsLocal).IsExpanded);
				Assert.Equal("proc:helper", harness.Procedures.SelectedNode!.Key);
			});
		}

		[Fact]
		public void Each_document_remembers_its_own_expansion()
		{
			ShellStaRunner.Run(async () =>
			{
				(ShellHarness harness, DocumentViewModel first) = await CreateAsync();
				using ShellHarness _h = harness;
				harness.Procedures.Groups.Single(g => g.IsLocal).IsExpanded = false;
				DocumentViewModel second = await harness.OpenFileAsync("b.ssl", Script);
				await harness.Workspace.RequestParseAsync(second, true);
				Assert.True(harness.Procedures.Groups.Single(g => g.IsLocal).IsExpanded);

				harness.Workspace.ActiveDocument = first;

				Assert.False(harness.Procedures.Groups.Single(g => g.IsLocal).IsExpanded);
			});
		}

		[Fact]
		public void Deleting_asks_for_confirmation()
		{
			ShellStaRunner.Run(async () =>
			{
				(ShellHarness harness, DocumentViewModel document) = await CreateAsync();
				using ShellHarness _h = harness;
				harness.Dialogs.ConfirmAnswer = false;
				await harness.Procedures.DeleteAsync(Node(harness, "helper"));
				Assert.Contains("procedure helper begin", document.TextDocument.Text, StringComparison.Ordinal);

				harness.Dialogs.ConfirmAnswer = true;
				await harness.Procedures.DeleteAsync(Node(harness, "helper"));

				Assert.DoesNotContain("procedure helper begin", document.TextDocument.Text, StringComparison.Ordinal);
				Assert.DoesNotContain(harness.Procedures.Groups.SelectMany(g => g.Children), n => n.Title == "helper");
			});
		}

		[Fact]
		public void Moving_up_and_down_swaps_neighbours()
		{
			ShellStaRunner.Run(async () =>
			{
				(ShellHarness harness, DocumentViewModel document) = await CreateAsync();
				using ShellHarness _h = harness;

				await harness.Procedures.MoveUpAsync(Node(harness, "helper"));
				String text = document.TextDocument.Text;
				Assert.True(text.IndexOf("procedure helper begin", StringComparison.Ordinal) < text.IndexOf("procedure start begin", StringComparison.Ordinal));
				Assert.Equal(new[] { "helper", "start", "talk_p_proc" }, harness.Procedures.Groups.Single(g => g.IsLocal).Children.Select(c => c.Title));

				await harness.Procedures.MoveDownAsync(Node(harness, "helper"));
				Assert.Equal(new[] { "start", "helper", "talk_p_proc" }, harness.Procedures.Groups.Single(g => g.IsLocal).Children.Select(c => c.Title));
			});
		}

		[Fact]
		public void Moving_the_first_procedure_up_does_nothing()
		{
			ShellStaRunner.Run(async () =>
			{
				(ShellHarness harness, DocumentViewModel document) = await CreateAsync();
				using ShellHarness _h = harness;

				await harness.Procedures.MoveUpAsync(Node(harness, "start"));

				Assert.Equal(Script, document.TextDocument.Text);
			});
		}

		[Fact]
		public void Dragging_a_procedure_onto_another_moves_it()
		{
			ShellStaRunner.Run(async () =>
			{
				(ShellHarness harness, DocumentViewModel document) = await CreateAsync();
				using ShellHarness _h = harness;

				await harness.Procedures.MoveToAsync(Node(harness, "talk_p_proc"), Node(harness, "start"));

				String text = document.TextDocument.Text;
				Assert.True(text.IndexOf("procedure talk_p_proc begin", StringComparison.Ordinal) < text.IndexOf("procedure start begin", StringComparison.Ordinal));
			});
		}

		[Fact]
		public void A_standard_handler_is_created_through_the_prompt()
		{
			ShellStaRunner.Run(async () =>
			{
				(ShellHarness harness, DocumentViewModel document) = await CreateAsync();
				using ShellHarness _h = harness;
				harness.ScriptTools.ProcedureAnswer = new ProcedureNameResult("look_at_p_proc", "look_at_p_proc", ProcedurePlacement.AtEnd, false);

				await harness.Procedures.CreateHandlerAsync(new HandlerMenuItemViewModel(ProcedureHandlerEntry.CreateHandler("look_at_p_proc", "Look")));

				Assert.Contains("procedure look_at_p_proc begin", document.TextDocument.Text, StringComparison.Ordinal);
				Assert.True(harness.ScriptTools.LastRequest!.NameReadOnly);
			});
		}

		[Fact]
		public void Menu_groups_are_not_created_as_handlers()
		{
			ShellStaRunner.Run(async () =>
			{
				(ShellHarness harness, DocumentViewModel document) = await CreateAsync();
				using ShellHarness _h = harness;
				HandlerMenuItemViewModel group = new HandlerMenuItemViewModel(new ProcedureHandlerEntry(ProcedureHandlerKind.Group, "Group", String.Empty, new[] { ProcedureHandlerEntry.CreateHandler("x_proc", String.Empty) }));

				await harness.Procedures.CreateHandlerAsync(group);

				Assert.Equal(Script, document.TextDocument.Text);
				Assert.Null(harness.ScriptTools.LastRequest);
			});
		}

		[Fact]
		public void The_menu_of_standard_handlers_is_read_from_the_descriptions_folder()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				System.IO.File.WriteAllText(
					System.IO.Path.Combine(harness.Paths.DescriptionsDirectory, "prochandlers.ini"),
					"<m>Group\r\ntalk_p_proc<d>Talk\r\n");
				ProceduresPanelViewModel panel = new ProceduresPanelViewModel(harness.Workspace, harness.Tracker, harness.Dialogs, harness.Settings, harness.NodeEditors);

				Assert.Equal("talk_p_proc", Assert.Single(Assert.Single(panel.Handlers).Children).Name);
				await Task.CompletedTask;
			});
		}

		[Fact]
		public void The_toolbar_commands_reach_the_editor()
		{
			ShellStaRunner.Run(async () =>
			{
				(ShellHarness harness, DocumentViewModel document) = await CreateAsync();
				using ShellHarness _h = harness;
				FakeEditorSurface editor = harness.AttachEditor(document);

				harness.Procedures.GoToBeginCommand.Execute(null);
				harness.Procedures.ToggleOutlineCommand.Execute(null);

				Assert.Equal(new[] { "execute:GoToProcedureBegin", "execute:ToggleOutline" }, editor.Calls);
			});
		}

		[Fact]
		public void Showing_arguments_rebuilds_the_titles()
		{
			ShellStaRunner.Run(async () =>
			{
				(ShellHarness harness, DocumentViewModel _) = await CreateAsync();
				using ShellHarness _h = harness;

				harness.Procedures.ShowArguments = true;

				Assert.Contains(harness.Procedures.Groups.SelectMany(g => g.Children), n => n.Title.Contains('(', StringComparison.Ordinal));
			});
		}

		private const String NodeScript =
			"procedure talk_p_proc;\r\n" +
			"procedure Node001;\r\n" +
			"procedure helper;\r\n" +
			"\r\n" +
			"procedure talk_p_proc begin\r\n" +
			"end\r\n" +
			"\r\n" +
			"procedure Node001 begin\r\n" +
			"end\r\n" +
			"\r\n" +
			"procedure helper begin\r\n" +
			"end\r\n";

		[Fact]
		public void Edit_node_code_is_offered_for_dialogue_nodes_and_talk_p_proc_only()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("npc.ssl", NodeScript);
				await harness.Workspace.RequestParseAsync(document, true);

				Assert.True(Node(harness, "Node001").CanEditNodeCode);
				Assert.True(Node(harness, "talk_p_proc").CanEditNodeCode);
				Assert.False(Node(harness, "helper").CanEditNodeCode);
			});
		}

		[Fact]
		public void Edit_node_code_opens_the_node_editor_without_closing_it_on_done()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("npc.ssl", NodeScript);
				harness.Folder.Write("npc.msg", "{100}{}{Hello}\r\n");
				await harness.Workspace.RequestParseAsync(document, true);

				await harness.Procedures.EditNodeCodeAsync(Node(harness, "Node001"));

				(String node, Boolean fromFlowchart) = Assert.Single(harness.NodeEditors.Opened);
				Assert.Equal("Node001", node);
				Assert.False(fromFlowchart);
			});
		}

		[Fact]
		public void Edit_node_code_without_a_message_file_shows_an_error()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("npc.ssl", NodeScript);
				await harness.Workspace.RequestParseAsync(document, true);

				await harness.Procedures.EditNodeCodeAsync(Node(harness, "Node001"));

				Assert.Empty(harness.NodeEditors.Opened);
				Assert.Single(harness.Dialogs.Messages);
			});
		}

		[Fact]
		public void Edit_node_code_ignores_a_procedure_that_is_not_a_node()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("npc.ssl", NodeScript);
				harness.Folder.Write("npc.msg", "{100}{}{Hello}\r\n");
				await harness.Workspace.RequestParseAsync(document, true);

				await harness.Procedures.EditNodeCodeAsync(Node(harness, "helper"));

				Assert.Empty(harness.NodeEditors.Opened);
			});
		}

		[Fact]
		public void The_only_procedure_mode_folds_the_other_procedures_after_going_to_one()
		{
			ShellStaRunner.Run(async () =>
			{
				(ShellHarness harness, DocumentViewModel document) = await CreateAsync();
				using ShellHarness _h = harness;
				FakeEditorSurface editor = harness.AttachEditor(document);

				await harness.Procedures.NavigateAsync(Node(harness, "helper"));
				Assert.DoesNotContain("execute:ShowOnlyCurrentProcedure", editor.Calls);

				harness.Procedures.OnlyCurrentProcedure = true;
				await harness.Procedures.NavigateAsync(Node(harness, "helper"));

				Assert.Equal(new[] { "execute:ShowOnlyCurrentProcedure" }, editor.Calls.Where(c => c.StartsWith("execute", StringComparison.Ordinal)));
			});
		}

		[Fact]
		public void Create_procedure_starts_from_the_procedure_selected_in_the_tree()
		{
			ShellStaRunner.Run(async () =>
			{
				(ShellHarness harness, DocumentViewModel document) = await CreateAsync();
				using ShellHarness _h = harness;
				FakeEditorSurface editor = harness.AttachEditor(document);
				editor.CurrentProcedure = document.ProgramInfo!.GetProcedure("start");
				harness.Procedures.SelectedNode = Node(harness, "helper");
				harness.ScriptTools.ProcedureAnswer = new ProcedureNameResult("added", "added", ProcedurePlacement.AfterCurrent, false);

				await harness.Procedures.CreateProcedureAsync();

				Assert.Equal("helper", harness.ScriptTools.LastRequest!.InitialName);
				Assert.Equal(ProcedureNameDialogMode.Create, harness.ScriptTools.LastRequest.Mode);
				Assert.True(harness.ScriptTools.LastRequest.CanCopyBody);
				String text = document.TextDocument.Text;
				Assert.True(text.IndexOf("procedure added begin", StringComparison.Ordinal) > text.IndexOf("procedure helper begin", StringComparison.Ordinal));
				Assert.True(text.IndexOf("procedure added begin", StringComparison.Ordinal) < text.IndexOf("procedure talk_p_proc begin", StringComparison.Ordinal));
			});
		}

		[Fact]
		public void Create_procedure_without_a_tree_selection_starts_from_the_selected_text()
		{
			ShellStaRunner.Run(async () =>
			{
				(ShellHarness harness, DocumentViewModel document) = await CreateAsync();
				using ShellHarness _h = harness;
				FakeEditorSurface editor = harness.AttachEditor(document);
				editor.Select(document.TextDocument.Text.IndexOf("call helper", StringComparison.Ordinal) + 5, 6);
				harness.ScriptTools.ProcedureAnswer = null;

				await harness.Procedures.CreateProcedureAsync();

				Assert.Equal("helper", harness.ScriptTools.LastRequest!.InitialName);
			});
		}

		[Fact]
		public void Create_procedure_without_any_selection_starts_empty()
		{
			ShellStaRunner.Run(async () =>
			{
				(ShellHarness harness, DocumentViewModel _) = await CreateAsync();
				using ShellHarness _h = harness;
				harness.ScriptTools.ProcedureAnswer = null;

				await harness.Procedures.CreateProcedureAsync();

				Assert.Equal(String.Empty, harness.ScriptTools.LastRequest!.InitialName);
			});
		}
	}
}