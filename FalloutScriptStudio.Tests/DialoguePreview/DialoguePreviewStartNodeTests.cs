namespace FalloutScriptStudio.Tests.DialoguePreview
{
	using System;
	using System.Linq;
	using FalloutScriptStudio.Controls.Diagram.Model;
	using FalloutScriptStudio.Services.ScriptTools;
	using FalloutScriptStudio.Tests.Flowchart;
	using FalloutScriptStudio.Tests.ScriptTools;
	using FalloutScriptStudio.ViewModels.DialoguePreview;
	using Xunit;

	public class DialoguePreviewStartNodeTests
	{
		[Fact]
		public void Initialize_AtANode_ShowsThatNodeInsteadOfTheTalkProcedure()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				DialoguePreviewViewModel preview = harness.CreatePreview();

				Boolean started = await preview.InitializeAsync(harness.Script, new DialoguePreviewOpenOptions { StartAtNode = "Node002" });

				Assert.True(started);
				Assert.Equal("Node002", preview.SelectedNode);
				Assert.NotEmpty(preview.Rows);
				Assert.False(preview.CanGoBack);
				Assert.Equal(new[] { "talk_p_proc", "Node001", "Node002", "Node999", "Node777" }, preview.NodeNames.ToArray());
			});
		}

		[Fact]
		public void Initialize_AtAnUnknownNode_FallsBackToTheTalkProcedure()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				DialoguePreviewViewModel preview = harness.CreatePreview();

				await preview.InitializeAsync(harness.Script, new DialoguePreviewOpenOptions { StartAtNode = "NoSuchNode" });

				Assert.Equal("talk_p_proc", preview.SelectedNode);
			});
		}

		[Fact]
		public void ShowNode_MovesAnOpenWindow_AndBackReturnsToWhereItWas()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				DialoguePreviewViewModel preview = harness.CreatePreview();
				await preview.InitializeAsync(harness.Script);
				Assert.Equal("talk_p_proc", preview.SelectedNode);

				await preview.ShowNodeAsync("Node777");

				Assert.Equal("Node777", preview.SelectedNode);
				Assert.True(preview.CanGoBack);
				Assert.False(preview.CanGoForward);

				await preview.GoBackAsync();
				Assert.Equal("talk_p_proc", preview.SelectedNode);
				Assert.True(preview.CanGoForward);
				await preview.GoForwardAsync();
				Assert.Equal("Node777", preview.SelectedNode);
			});
		}

		[Fact]
		public void ShowNode_OfTheNodeBeingShown_DoesNotAddAHistoryEntry()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				DialoguePreviewViewModel preview = harness.CreatePreview();
				await preview.InitializeAsync(harness.Script, new DialoguePreviewOpenOptions { StartAtNode = "Node001" });

				await preview.ShowNodeAsync("node001");

				Assert.Equal("Node001", preview.SelectedNode);
				Assert.False(preview.CanGoBack);
			});
		}

		[Fact]
		public void ShowNode_BeforeTheStart_DoesNothing()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				DialoguePreviewViewModel preview = harness.CreatePreview();

				await preview.ShowNodeAsync("Node001");

				Assert.Null(preview.SelectedNode);
				Assert.Empty(preview.Rows);
			});
		}

		[Fact]
		public void TheFlowchartMenuOffersThePreviewForTheNodeUnderThePointer()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				await harness.LoadAsync();
				DiagramNode node = harness.Canvas.FindNode("Node002")!;

				harness.ViewModel.PrepareContext(node);

				Assert.True(harness.ViewModel.CanPreviewNode);
				Assert.Equal("Preview Dialogue From Node: Node002", harness.ViewModel.PreviewNodeHeader);

				harness.ViewModel.PrepareContext(null);
				Assert.False(harness.ViewModel.CanPreviewNode);
				Assert.Equal("Preview Dialogue From This Node", harness.ViewModel.PreviewNodeHeader);
			});
		}

		[Fact]
		public void TheFlowchartPreviewCommandAsksTheWorkspaceForTheNode()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				await harness.LoadAsync();
				harness.ViewModel.PrepareContext(harness.Canvas.FindNode("Node002"));

				await harness.ViewModel.PreviewNodeAsync();

				Assert.Contains("PreviewDialogueFromNodeAsync", harness.WorkspaceRecorder.Calls);
			});
		}

		[Fact]
		public void TheFlowchartPreviewCommandDoesNothingWithoutANode()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				await harness.LoadAsync();
				harness.ViewModel.PrepareContext(null);

				await harness.ViewModel.PreviewNodeAsync();

				Assert.DoesNotContain("PreviewDialogueFromNodeAsync", harness.WorkspaceRecorder.Calls);
			});
		}
	}
}