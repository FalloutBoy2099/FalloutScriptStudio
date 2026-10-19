namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.Linq;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Tests.Shell.Support;
	using FalloutScriptStudio.ViewModels.Panels;
	using Xunit;

	public sealed class DialoguePreviewFromNodeTests
	{
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

		private static ProcedureNodeViewModel Node(ShellHarness harness, String name)
		{
			return harness.Procedures.Groups.SelectMany(g => g.Children).OfType<ProcedureNodeViewModel>().Single(n => n.Procedure.Name == name);
		}

		[Fact]
		public void The_preview_is_offered_for_the_procedures_whose_node_code_can_be_edited()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("npc.ssl", NodeScript);
				await harness.Workspace.RequestParseAsync(document, true);

				Assert.True(Node(harness, "Node001").CanPreviewDialogue);
				Assert.True(Node(harness, "talk_p_proc").CanPreviewDialogue);
				Assert.False(Node(harness, "helper").CanPreviewDialogue);
				Assert.All(harness.Procedures.Groups.SelectMany(g => g.Children).OfType<ProcedureNodeViewModel>(), node => Assert.Equal(node.CanEditNodeCode, node.CanPreviewDialogue));
			});
		}

		[Fact]
		public void The_procedures_panel_asks_the_tool_service_for_the_preview_at_the_node()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("npc.ssl", NodeScript);
				harness.Folder.Write("npc.msg", "{100}{}{Hello}\r\n");
				await harness.Workspace.RequestParseAsync(document, true);

				await harness.Procedures.PreviewDialogueAsync(Node(harness, "Node001"));

				(DocumentViewModel script, FalloutScriptStudio.Services.ScriptTools.DialoguePreviewOpenOptions options) = Assert.Single(harness.ScriptTools.PreviewRequests);
				Assert.Same(document, script);
				Assert.Equal("Node001", options.StartAtNode);
				Assert.Equal(new[] { "preview:npc.ssl@Node001" }, harness.ScriptTools.Calls);
			});
		}

		[Fact]
		public void A_second_request_for_another_node_is_a_second_request_to_the_same_window()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("npc.ssl", NodeScript);
				harness.Folder.Write("npc.msg", "{100}{}{Hello}\r\n");
				await harness.Workspace.RequestParseAsync(document, true);

				await harness.Procedures.PreviewDialogueAsync(Node(harness, "Node001"));
				await harness.Procedures.PreviewDialogueAsync(Node(harness, "talk_p_proc"));

				Assert.Equal(new[] { "Node001", "talk_p_proc" }, harness.ScriptTools.PreviewRequests.Select(request => request.Options.StartAtNode));
				Assert.All(harness.ScriptTools.PreviewRequests, request => Assert.Same(document, request.Script));
			});
		}

		[Fact]
		public void The_preview_ignores_a_procedure_that_is_not_a_node()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("npc.ssl", NodeScript);
				harness.Folder.Write("npc.msg", "{100}{}{Hello}\r\n");
				await harness.Workspace.RequestParseAsync(document, true);

				await harness.Procedures.PreviewDialogueAsync(Node(harness, "helper"));
				await harness.Procedures.PreviewDialogueAsync(null);

				Assert.Empty(harness.ScriptTools.PreviewRequests);
			});
		}

		[Fact]
		public void The_preview_needs_a_message_file_like_the_menu_command_does()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("npc.ssl", NodeScript);
				await harness.Workspace.RequestParseAsync(document, true);

				await harness.Procedures.PreviewDialogueAsync(Node(harness, "Node001"));

				Assert.Empty(harness.ScriptTools.PreviewRequests);
				Assert.Contains("message file", Assert.Single(harness.Dialogs.Messages), StringComparison.OrdinalIgnoreCase);
			});
		}

		[Fact]
		public void The_preview_of_an_unsaved_script_is_refused()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel unsaved = harness.Workspace.CreateNew(NodeScript);

				await harness.Workspace.PreviewDialogueFromNodeAsync(unsaved, "Node001");

				Assert.Empty(harness.ScriptTools.PreviewRequests);
				Assert.Single(harness.Dialogs.Messages);
			});
		}
	}
}