namespace FalloutScriptStudio.Tests.Flowchart
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Dialogue;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Tests.ScriptTools;
	using FalloutScriptStudio.ViewModels.Flowchart;
	using Xunit;

	public class NodeCodeEditorViewModelTests
	{
		private static async Task<NodeCodeEditorViewModel> OpenAsync(FlowchartHarness harness, String node = "Node001", INodeCodeSnippetStore? snippets = null)
		{
			NodeCodeEditorViewModel editor = harness.CreateNodeEditor(snippets);
			await editor.InitializeAsync(harness.Script, node);
			return editor;
		}

		[Fact]
		public void Initialize_ShowsTheCodeAndTheMessagesOfTheNode()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();

				NodeCodeEditorViewModel editor = await OpenAsync(harness);

				Assert.Equal("Code: Node001", editor.Title);
				Assert.Contains("Reply(110);", editor.Code.Text, StringComparison.Ordinal);
				Assert.False(editor.HasChanges);
				Assert.Equal(2, editor.Rows.Count);
				Assert.Equal(110, editor.Rows[0].MessageNumber);
				Assert.Equal("I am John.", editor.Rows[0].Text);
				Assert.False(editor.Rows[0].IsOption);
				Assert.True(editor.Rows[1].IsOption);
				Assert.Equal("dcjohn.msg", editor.Rows[1].FileName);
				Assert.True(editor.Rows[1].CanEditText);
			});
		}

		[Fact]
		public void Initialize_ListsTheNodesWithTheEmptyChoiceFirst()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();

				NodeCodeEditorViewModel editor = await OpenAsync(harness);

				Assert.Equal(new[] { NodeCodeEditorViewModel.NoNodeChoice, "Node001", "Node002", "Node777", "Node999", "talk_p_proc" }, editor.NodeChoices.ToArray());
				Assert.Equal(NodeCodeEditorViewModel.NoNodeChoice, editor.SelectedNodeChoice);
			});
		}

		[Fact]
		public void EditingTheCode_MarksItChangedUntilUndone()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				NodeCodeEditorViewModel editor = await OpenAsync(harness);

				editor.Code.Insert(0, "// note\r\n");
				Assert.True(editor.HasChanges);

				editor.Undo();
				Assert.False(editor.HasChanges);

				editor.Redo();
				Assert.True(editor.HasChanges);
			});
		}

		[Fact]
		public void Apply_ReplacesTheCodeInTheScriptAsOneUndoableChange()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				NodeCodeEditorViewModel editor = await OpenAsync(harness, "Node002");
				String original = harness.Script.TextDocument.Text;
				Boolean applied = false;
				Boolean closed = false;
				editor.CodeApplied += (_, _) => applied = true;
				editor.CloseRequested += (_, accepted) => closed = accepted;
				editor.Code.Text = "\r\n   Reply(120);\r\n   Reply(122);\r\n";

				Assert.True(await editor.ApplyAsync(true));

				Assert.Contains("Reply(122);", harness.Script.TextDocument.Text, StringComparison.Ordinal);
				Assert.DoesNotContain("NOption(121, Node999, 4);", harness.Script.TextDocument.Text, StringComparison.Ordinal);
				Assert.True(applied);
				Assert.True(closed);
				Assert.False(editor.HasChanges);

				harness.Script.TextDocument.UndoStack.Undo();
				Assert.Equal(original, harness.Script.TextDocument.Text);
			});
		}

		[Fact]
		public void Apply_WithoutChanges_JustCloses()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				NodeCodeEditorViewModel editor = await OpenAsync(harness);
				Boolean applied = false;
				Boolean closed = false;
				editor.CodeApplied += (_, _) => applied = true;
				editor.CloseRequested += (_, accepted) => closed = accepted;

				Assert.True(await editor.ApplyAsync(true));

				Assert.False(applied);
				Assert.True(closed);
			});
		}

		[Fact]
		public void Apply_WhenTheNodeLeftTheScript_ReportsTheError()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				NodeCodeEditorViewModel editor = await OpenAsync(harness, "Node777");
				editor.Code.Insert(0, "// x\r\n");
				harness.Script.TextDocument.Text = harness.Script.TextDocument.Text.Replace("procedure Node777", "procedure Other777", StringComparison.Ordinal);

				Assert.False(await editor.ApplyAsync(true));

				Assert.Single(harness.Dialogs.Errors);
				Assert.True(editor.HasChanges);
			});
		}

		[Fact]
		public void ConfirmClose_AsksOnlyWhenTheCodeChanged()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				NodeCodeEditorViewModel editor = await OpenAsync(harness, "Node777");

				Assert.True(await editor.ConfirmCloseAsync(false));
				Assert.Empty(harness.Dialogs.Questions);

				editor.Code.Insert(0, "// x\r\n");
				harness.Dialogs.YesNoCancelAnswer = YesNoCancel.Cancel;
				Assert.False(await editor.ConfirmCloseAsync(false));

				harness.Dialogs.YesNoCancelAnswer = YesNoCancel.No;
				Assert.True(await editor.ConfirmCloseAsync(false));
				Assert.DoesNotContain("// x", harness.Script.TextDocument.Text, StringComparison.Ordinal);

				harness.Dialogs.YesNoCancelAnswer = YesNoCancel.Yes;
				Assert.True(await editor.ConfirmCloseAsync(false));
				Assert.Contains("// x", harness.Script.TextDocument.Text, StringComparison.Ordinal);
			});
		}

		[Fact]
		public void ConfirmClose_WhenForced_HasNoCancelChoice()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				NodeCodeEditorViewModel editor = await OpenAsync(harness, "Node777");
				editor.Code.Insert(0, "// x\r\n");
				harness.Dialogs.ConfirmAnswer = false;

				Assert.True(await editor.ConfirmCloseAsync(true));

				Assert.DoesNotContain("// x", harness.Script.TextDocument.Text, StringComparison.Ordinal);
			});
		}

		[Fact]
		public void GoToNode_ShowsAnotherNodeAfterAskingAboutChanges()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				NodeCodeEditorViewModel editor = await OpenAsync(harness);
				editor.Code.Insert(0, "// x\r\n");
				editor.SelectedNodeChoice = "Node002";
				harness.Dialogs.YesNoCancelAnswer = YesNoCancel.Cancel;

				await editor.GoToNodeAsync();
				Assert.Equal("Node001", editor.NodeName);

				harness.Dialogs.YesNoCancelAnswer = YesNoCancel.No;
				await editor.GoToNodeAsync();
				Assert.Equal("Node002", editor.NodeName);
				Assert.Equal("Code: Node002", editor.Title);
				Assert.Contains("Reply(120);", editor.Code.Text, StringComparison.Ordinal);
				Assert.False(editor.HasChanges);
				Assert.Equal(NodeCodeEditorViewModel.NoNodeChoice, editor.SelectedNodeChoice);
			});
		}

		[Fact]
		public void GoToNode_WithoutAChoice_DoesNothing()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				NodeCodeEditorViewModel editor = await OpenAsync(harness);

				await editor.GoToNodeAsync();

				Assert.Equal("Node001", editor.NodeName);
			});
		}

		[Fact]
		public void OpeningTheMessageOfARow_ReplacesTheNumberWithTheLineTheUserSends()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				NodeCodeEditorViewModel editor = await OpenAsync(harness);
				harness.MessageEditor.SendMessageId = 120;

				editor.OpenMessage(editor.Rows[0]);
				await Task.Delay(50);

				Assert.Contains("Reply(120);", editor.Code.Text, StringComparison.Ordinal);
				Assert.DoesNotContain("Reply(110);", editor.Code.Text, StringComparison.Ordinal);
				Assert.True(editor.HasChanges);
				Assert.Equal("More.", editor.Rows[0].Text);
				Services.MessageEditor.MessageEditorOpenOptions request = Assert.Single(harness.MessageEditor.Requests);
				Assert.Equal(FlowchartSamples.MessagePath, request.FilePath);
				Assert.Equal(110, request.SelectMessageId);
				Assert.True(request.CloseOnSend);
				Assert.True(request.Modal);
				Assert.Same(editor, request.Target);
			});
		}

		[Fact]
		public void InsertMessage_PastesTheNumberTheUserSends()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				NodeCodeEditorViewModel editor = await OpenAsync(harness);
				List<String> inserted = new List<String>();
				editor.InsertTextRequested += (_, text) => inserted.Add(text);
				harness.MessageEditor.SendMessageId = 105;

				await editor.InsertMessageAsync();

				Assert.Equal(new[] { "105" }, inserted);
				Assert.Equal(FlowchartSamples.MessagePath, harness.MessageEditor.Requests.Single().FilePath);
				Assert.Null(harness.MessageEditor.Requests.Single().SelectMessageId);
			});
		}

		[Fact]
		public void SelectingARow_SelectsItsStatementInTheCode()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				NodeCodeEditorViewModel editor = await OpenAsync(harness);
				(Int32 Offset, Int32 Length)? range = null;
				editor.SelectRangeRequested += (_, r) => range = r;

				editor.SelectedRow = editor.Rows[1];

				Assert.NotNull(range);
				Assert.Equal("NOption(111, Node002, 4)", editor.Code.GetText(range.Value.Offset, range.Value.Length));
			});
		}

		[Fact]
		public void EditingTheTextOfARow_WritesTheMessageFile()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				NodeCodeEditorViewModel editor = await OpenAsync(harness);

				editor.Rows[0].Text = "Edited";
				await Task.Delay(50);

				Assert.Equal((FlowchartSamples.MessagePath, 110, "Edited"), Assert.Single(harness.Messages.Updates));
			});
		}

		[Theory]
		[InlineData("a{b")]
		[InlineData("a}b")]
		public void EditingTheTextOfARow_WithBraces_IsRejectedAndRestored(String text)
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				NodeCodeEditorViewModel editor = await OpenAsync(harness);
				String original = editor.Rows[0].Text;

				editor.Rows[0].Text = text;
				await Task.Delay(50);

				Assert.Empty(harness.Messages.Updates);
				Assert.Equal(original, editor.Rows[0].Text);
				Assert.Contains("Curly braces", Assert.Single(harness.Dialogs.Errors), StringComparison.Ordinal);
			});
		}

		[Fact]
		public void MissingMessages_AreErrorRowsThatCannotBeEdited()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				harness.Script.TextDocument.Text = harness.Script.TextDocument.Text.Replace("Reply(110)", "Reply(999)", StringComparison.Ordinal);
				NodeCodeEditorViewModel editor = await OpenAsync(harness);

				Assert.True(editor.Rows[0].IsError);
				Assert.False(editor.Rows[0].CanEditText);
				Assert.True(editor.Rows[0].IsReadOnlyText);
			});
		}

		[Fact]
		public void RefreshRows_FollowsTheCode()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				NodeCodeEditorViewModel editor = await OpenAsync(harness);

				editor.Code.Text += "   NOption(101, Node002, 4);\r\n";
				await editor.RefreshRowsAsync();

				Assert.Equal(3, editor.Rows.Count);
			});
		}

		[Fact]
		public void Snippets_AreStoredAndOffered()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				NodeCodeSnippetStore store = new NodeCodeSnippetStore(harness.Folder.Combine("CustomCode.ini"));
				NodeCodeEditorViewModel editor = await OpenAsync(harness, "Node001", store);
				harness.Dialogs.PromptAnswer = "greeting";

				await editor.StoreSnippetAsync("Reply(100);");

				Assert.True(editor.CanStoreSnippets);
				NodeCodeSnippet snippet = Assert.Single(editor.Snippets);
				Assert.Equal("greeting", snippet.Name);
				Assert.Equal("Reply(100);", snippet.Code);

				harness.Dialogs.PromptAnswer = null;
				await editor.StoreSnippetAsync("ignored");
				Assert.Single(editor.Snippets);
			});
		}

		[Fact]
		public void Snippets_ChangedOnAnotherThread_AreRefreshedOnTheUiThread()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				FakeNodeCodeSnippetStore store = new FakeNodeCodeSnippetStore();
				NodeCodeEditorViewModel editor = await OpenAsync(harness, "Node001", store);
				Int32 uiThread = Environment.CurrentManagedThreadId;
				List<Int32> notifiedOn = new List<Int32>();
				editor.Snippets.CollectionChanged += (_, _) => notifiedOn.Add(Environment.CurrentManagedThreadId);
				store.Snippets = new[] { new NodeCodeSnippet("greeting", "Reply(100);") };

				await Task.Run(store.RaiseChanged);
				await Task.Delay(100);

				Assert.Equal("greeting", Assert.Single(editor.Snippets).Name);
				Assert.NotEmpty(notifiedOn);
				Assert.All(notifiedOn, thread => Assert.Equal(uiThread, thread));
			});
		}

		[Fact]
		public void Dispose_StopsListeningToTheSnippetStore()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				FakeNodeCodeSnippetStore store = new FakeNodeCodeSnippetStore();
				NodeCodeEditorViewModel editor = await OpenAsync(harness, "Node001", store);
				Assert.Equal(1, store.SubscriberCount);

				editor.Dispose();

				Assert.Equal(0, store.SubscriberCount);
			});
		}

		[Fact]
		public void WithoutASnippetStore_SnippetsCannotBeStored()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				NodeCodeEditorViewModel editor = await OpenAsync(harness);
				harness.Dialogs.PromptAnswer = "name";

				await editor.StoreSnippetAsync("code");

				Assert.False(editor.CanStoreSnippets);
				Assert.Empty(editor.Snippets);
			});
		}

		[Fact]
		public void InsertText_AndPasteNodeName_AskTheEditorToInsert()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				NodeCodeEditorViewModel editor = await OpenAsync(harness);
				List<String> inserted = new List<String>();
				editor.InsertTextRequested += (_, text) => inserted.Add(text);

				editor.InsertText("call {node};");
				editor.InsertText(String.Empty);
				editor.PasteNodeName();
				editor.SelectedNodeChoice = "Node002";
				editor.PasteNodeName();

				Assert.Equal(new[] { "call {node};", "Node002" }, inserted);
			});
		}

		[Fact]
		public void QuickMacros_AreTheLegacyStatements()
		{
			Assert.Equal(
				new[] { "Reply", "Messages", "Option", "LowOption", "mstr", "g_mstr", "Call node" },
				NodeEditorMacro.All.Select(m => m.Header).ToArray());
			Assert.Equal("NOption({msg_num}, {node}, {iq});", NodeEditorMacro.All.Single(m => m.Header == "Option").Template);
		}

		[Fact]
		public void Done_AppliesTheCodeAndClosesTheWindowOnlyWhenOpenedFromTheFlowchart()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				NodeCodeEditorViewModel fromFlowchart = await OpenAsync(harness, "Node002");
				fromFlowchart.CloseOnDone = true;
				Boolean flowchartClosed = false;
				fromFlowchart.CloseRequested += (_, accepted) => flowchartClosed = accepted;
				fromFlowchart.Code.Text = "\r\n   Reply(120);\r\n";

				await fromFlowchart.DoneAsync();

				Assert.True(flowchartClosed);
				Assert.False(fromFlowchart.HasChanges);
			});
		}

		[Fact]
		public void Done_ByDefault_AppliesTheCodeButKeepsTheWindowOpen()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				NodeCodeEditorViewModel fromTree = await OpenAsync(harness, "Node002");
				Boolean closed = false;
				Boolean applied = false;
				fromTree.CloseRequested += (_, _) => closed = true;
				fromTree.CodeApplied += (_, _) => applied = true;
				fromTree.Code.Text = "\r\n   Reply(120);\r\n";

				await fromTree.DoneAsync();

				Assert.False(closed);
				Assert.True(applied);
				Assert.False(fromTree.HasChanges);
				Assert.Contains("Reply(120);", harness.Script.TextDocument.Text, StringComparison.Ordinal);
			});
		}

		[Fact]
		public void ChoosingANodeWhileTextIsSelected_ReplacesTheSelectionWithTheNodeName()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				NodeCodeEditorViewModel editor = await OpenAsync(harness);
				List<String> inserted = new List<String>();
				editor.InsertTextRequested += (_, text) => inserted.Add(text);

				editor.NodeChoiceCommitted(true);
				editor.SelectedNodeChoice = "Node002";
				editor.NodeChoiceCommitted(false);
				editor.NodeChoiceCommitted(true);

				Assert.Equal(new[] { "Node002" }, inserted);
			});
		}
	}
}
