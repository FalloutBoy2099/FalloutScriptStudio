namespace FalloutScriptStudio.Tests.Flowchart
{
	using System;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Controls.Diagram.Model;
	using FalloutScriptStudio.Core.Dialogue;
	using FalloutScriptStudio.Tests.ScriptTools;
	using Xunit;

	[Collection("Test collection for FalloutScriptStudio.Tests.Flowchart.FlowchartViewModelTests")]
	public class FlowchartViewModelNodeTests
	{
		[Fact]
		public void PrepareContext_DescribesTheNodeUnderThePointer()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				await harness.LoadAsync();
				DiagramNode node = harness.Canvas.FindNode("Node001")!;
				harness.Canvas.Select(node);

				harness.ViewModel.PrepareContext(node);

				Assert.Equal("Edit Node: Node001", harness.ViewModel.EditNodeHeader);
				Assert.Equal("Rename Node: Node001", harness.ViewModel.RenameNodeHeader);
				Assert.True(harness.ViewModel.CanEditNode);
				Assert.True(harness.ViewModel.CanRenameNode);
				Assert.True(harness.ViewModel.CanHideSelection);
				Assert.True(harness.ViewModel.CanDeleteSelection);
				Assert.Equal("Jump from: Node001 to", harness.ViewModel.JumpHeader);
				Assert.Equal(new[] { "Node002", "Node999" }, harness.ViewModel.JumpToTargets.Select(t => t.Name).ToArray());
				Assert.Equal(new[] { "talk_p_proc" }, harness.ViewModel.JumpFromTargets.Select(t => t.Name).ToArray());

				harness.ViewModel.PrepareContext(null);
				Assert.False(harness.ViewModel.CanEditNode);
				Assert.Equal("Edit Node", harness.ViewModel.EditNodeHeader);
			});
		}

		[Fact]
		public void PrepareContext_StartNodeAndEditedNodesCannotBeRenamed()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				await harness.LoadAsync();

				harness.ViewModel.PrepareContext(harness.Canvas.FindNode("talk_p_proc"));
				Assert.False(harness.ViewModel.CanRenameNode);

				harness.Editors.Open(harness.Script, "Node001", true);
				harness.ViewModel.PrepareContext(harness.Canvas.FindNode("Node001"));
				Assert.False(harness.ViewModel.CanRenameNode);
				Assert.True(harness.Canvas.FindNode("Node001")!.IsEditing);
			});
		}

		[Fact]
		public void Delete_RemovesTheProceduresFromTheScriptAsOneUndoableChange()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				await harness.LoadAsync();
				String original = harness.Script.TextDocument.Text;
				harness.Canvas.Select(harness.Canvas.FindNode("Node777")!);
				harness.Canvas.Select(harness.Canvas.FindNode("Node002")!, true);

				await harness.ViewModel.DeleteSelectionAsync();

				String text = harness.Script.TextDocument.Text;
				Assert.DoesNotContain("Node777", text, StringComparison.Ordinal);
				Assert.DoesNotContain("procedure Node002", text, StringComparison.Ordinal);
				Assert.Contains("procedure Node001 begin", text, StringComparison.Ordinal);
				Assert.Null(harness.Canvas.FindNode("Node777"));
				Assert.Null(harness.Canvas.FindNode("Node002"));
				Assert.Single(harness.Dialogs.Questions);

				harness.Script.TextDocument.UndoStack.Undo();
				Assert.Equal(original, harness.Script.TextDocument.Text);
			});
		}

		[Fact]
		public void Delete_WhenTheUserDeclines_ChangesNothing()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				await harness.LoadAsync();
				String original = harness.Script.TextDocument.Text;
				harness.Canvas.Select(harness.Canvas.FindNode("Node777")!);
				harness.Dialogs.ConfirmAnswer = false;

				await harness.ViewModel.DeleteSelectionAsync();

				Assert.Equal(original, harness.Script.TextDocument.Text);
				Assert.NotNull(harness.Canvas.FindNode("Node777"));
			});
		}

		[Fact]
		public void Delete_NeverDeletesTheStartNodeOrANodeThatIsBeingEdited()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				await harness.LoadAsync();
				String original = harness.Script.TextDocument.Text;
				harness.Editors.Open(harness.Script, "Node001", true);
				harness.Canvas.Select(harness.Canvas.FindNode("talk_p_proc")!);
				harness.Canvas.Select(harness.Canvas.FindNode("Node001")!, true);

				await harness.ViewModel.DeleteSelectionAsync();

				Assert.Equal(original, harness.Script.TextDocument.Text);
				Assert.Equal(2, harness.Dialogs.Warnings.Count);
				Assert.NotNull(harness.Canvas.FindNode("talk_p_proc"));
				Assert.NotNull(harness.Canvas.FindNode("Node001"));
			});
		}

		[Fact]
		public void Delete_Notes_LeavesTheScriptAlone()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				await harness.LoadAsync();
				String original = harness.Script.TextDocument.Text;
				harness.ViewModel.AddNote();
				Assert.Single(harness.Canvas.Items.OfType<DiagramNote>());

				await harness.ViewModel.DeleteSelectionAsync();

				Assert.Empty(harness.Canvas.Items.OfType<DiagramNote>());
				Assert.Equal(original, harness.Script.TextDocument.Text);
			});
		}

		[Fact]
		public void Rename_ChangesTheScriptAndKeepsThePositionOfTheNode()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				await harness.LoadAsync();
				DiagramNode node = harness.Canvas.FindNode("Node001")!;
				node.X = 321;
				harness.ViewModel.PrepareContext(node);
				harness.WorkspaceRecorder.RenameTo = "Node050";

				await harness.ViewModel.RenameNodeAsync();

				Assert.DoesNotContain("Node001", harness.Script.TextDocument.Text, StringComparison.Ordinal);
				Assert.Null(harness.Canvas.FindNode("Node001"));
				Assert.Equal(321, harness.Canvas.FindNode("Node050")!.X);
				Assert.Equal("Node050", harness.Canvas.FindNode("talk_p_proc")!.Rows[1].LinkTargetName);
			});
		}

		[Fact]
		public void Rename_ToANameWithoutNode_IsRefusedAndUndone()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				await harness.LoadAsync();
				String original = harness.Script.TextDocument.Text;
				harness.ViewModel.PrepareContext(harness.Canvas.FindNode("Node001"));
				harness.WorkspaceRecorder.RenameTo = "Dialog001";

				await harness.ViewModel.RenameNodeAsync();

				Assert.Equal(original, harness.Script.TextDocument.Text);
				Assert.Single(harness.Dialogs.Warnings);
				Assert.NotNull(harness.Canvas.FindNode("Node001"));
			});
		}

		[Fact]
		public void Rename_CancelledByTheUser_ChangesNothing()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				await harness.LoadAsync();
				String original = harness.Script.TextDocument.Text;
				harness.ViewModel.PrepareContext(harness.Canvas.FindNode("Node001"));
				harness.WorkspaceRecorder.RenameTo = null;

				await harness.ViewModel.RenameNodeAsync();

				Assert.Equal(original, harness.Script.TextDocument.Text);
				Assert.Empty(harness.Dialogs.Warnings);
			});
		}

		[Fact]
		public void Rename_TheStartNode_IsNotPossible()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				await harness.LoadAsync();
				String original = harness.Script.TextDocument.Text;
				harness.ViewModel.PrepareContext(harness.Canvas.FindNode("talk_p_proc"));
				harness.WorkspaceRecorder.RenameTo = "Node000";

				await harness.ViewModel.RenameNodeAsync();

				Assert.Equal(original, harness.Script.TextDocument.Text);
			});
		}

		[Fact]
		public void AddNode_PutsTheNewNodeInTheMiddleOfTheViewAndSelectsIt()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				await harness.LoadAsync();
				harness.FlowDialogs.NewNode = async script =>
				{
					NodeCreationResult result = await harness.ScriptEditor.CreateNodeAsync(script, "300", "\r\n   Reply(100);\r\n");
					return result.NodeName;
				};

				await harness.ViewModel.AddNodeAsync();

				Assert.Contains("procedure Node300 begin", harness.Script.TextDocument.Text, StringComparison.Ordinal);
				DiagramNode node = harness.Canvas.FindNode("Node300")!;
				Assert.Same(node, harness.Canvas.SelectedItem);
				Assert.Equal(6, harness.Canvas.Nodes.Count());
			});
		}

		[Fact]
		public void AddNode_Cancelled_AddsNothing()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				await harness.LoadAsync();

				await harness.ViewModel.AddNodeAsync();

				Assert.Equal(5, harness.Canvas.Nodes.Count());
			});
		}

		[Fact]
		public void EditNode_OpensTheNodeEditor()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				await harness.LoadAsync();
				harness.ViewModel.PrepareContext(harness.Canvas.FindNode("Node002"));

				harness.ViewModel.EditNode();

				Assert.True(harness.Editors.IsEditing(harness.Script, "Node002"));
			});
		}

		[Fact]
		public void AppliedNodeCode_UpdatesTheChart()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				await harness.LoadAsync();
				await harness.ScriptEditor.ReplaceNodeCodeAsync(harness.Script, "Node002", "\r\n   Reply(120);\r\n   Reply(121);\r\n   Reply(122);\r\n");

				harness.Editors.RaiseApplied(harness.Script, "Node002");
				for (Int32 i = 0; i < 100 && harness.Canvas.FindNode("Node002")!.Rows.Count != 3; i++)
				{
					await Task.Delay(20);
				}

				Assert.Equal(3, harness.Canvas.FindNode("Node002")!.Rows.Count);
			});
		}
	}
}
