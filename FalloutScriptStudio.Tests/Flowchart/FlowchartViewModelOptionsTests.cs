namespace FalloutScriptStudio.Tests.Flowchart
{
	using System;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Controls.Diagram.Model;
	using FalloutScriptStudio.Tests.ScriptTools;
	using FalloutScriptStudio.ViewModels.Flowchart;
	using Xunit;

	[Collection("Test collection for FalloutScriptStudio.Tests.Flowchart.FlowchartViewModelTests")]
	public class FlowchartViewModelOptionsTests
	{
		[Fact]
		public void Options_AreReadFromAndWrittenToTheSettings()
		{
			StaTestRunner.Run(() =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				harness.Settings.Settings.Flowchart.AutoUpdate = true;
				harness.Settings.Settings.Flowchart.AutoArrange = true;
				harness.Settings.Settings.Flowchart.AutoSaveChart = false;
				harness.Settings.Settings.Flowchart.AutoHideNodes = true;

				FlowchartViewModel second = harness.CreateViewModel();
				Assert.True(second.AutoUpdate);
				Assert.True(second.AutoArrange);
				Assert.False(second.AutoSaveChart);
				Assert.True(second.AutoHideNodes);

				second.AutoUpdate = false;
				second.AutoSaveChart = true;
				second.WithoutExitNode = true;
				second.AutoArrange = false;
				second.AutoHideNodes = false;

				Assert.False(harness.Settings.Settings.Flowchart.AutoUpdate);
				Assert.True(harness.Settings.Settings.Flowchart.AutoSaveChart);
				Assert.True(harness.Settings.Settings.Flowchart.WithoutExitNode);
				Assert.False(harness.Settings.Settings.Flowchart.AutoArrange);
				Assert.False(harness.Settings.Settings.Flowchart.AutoHideNodes);
				second.Dispose();
				return Task.CompletedTask;
			});
		}

		[Fact]
		public void DisplayOptions_ReachTheCanvas()
		{
			StaTestRunner.Run(() =>
			{
				using FlowchartHarness harness = new FlowchartHarness();

				harness.ViewModel.HighQuality = false;
				harness.ViewModel.LowDetails = true;
				harness.ViewModel.LinksOnlyForSelection = true;

				Assert.False(harness.Canvas.HighQuality);
				Assert.True(harness.Canvas.LowDetail);
				Assert.True(harness.Canvas.LinksOnlyForSelection);
				return Task.CompletedTask;
			});
		}

		[Fact]
		public void Zoom_FollowsTheCanvasAndTheSlider()
		{
			StaTestRunner.Run(() =>
			{
				using FlowchartHarness harness = new FlowchartHarness();

				harness.ViewModel.ZoomPercent = 50;
				Assert.Equal(0.5, harness.Canvas.Zoom, 3);
				Assert.Equal("50%", harness.ViewModel.ZoomText);

				harness.Canvas.Zoom = 2;
				Assert.Equal(200, harness.ViewModel.ZoomPercent);
				return Task.CompletedTask;
			});
		}

		[Fact]
		public void ShowAllCode_ChangesOnlyTheNodesWithoutASettingOfTheirOwn()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				await harness.LoadAsync();
				harness.Canvas.FindNode("Node001")!.HasUserShowCodeSetting = true;

				harness.ViewModel.ShowAllCode = false;

				Assert.True(harness.Canvas.FindNode("Node001")!.ShowCodeRows);
				Assert.False(harness.Canvas.FindNode("Node002")!.ShowCodeRows);
			});
		}

		[Fact]
		public void Collapse_AndExpand_ReachTheNodesOrTheirRows()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				await harness.LoadAsync();

				harness.ViewModel.CollapseAll(false);
				Assert.All(harness.Canvas.Nodes, n => Assert.True(n.IsCollapsed));

				harness.ViewModel.ExpandAll(false);
				Assert.All(harness.Canvas.Nodes, n => Assert.False(n.IsCollapsed));

				harness.ViewModel.ExpandAll(true);
				Assert.True(harness.Canvas.FindNode("Node001")!.Rows[0].IsExpanded);

				harness.ViewModel.CollapseAll(true);
				Assert.False(harness.Canvas.FindNode("Node001")!.Rows[0].IsExpanded);
			});
		}

		[Fact]
		public void Create_AsksBeforeReplacingAnExistingChart()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				await harness.LoadAsync();
				harness.Canvas.FindNode("Node001")!.X = 777;
				harness.Dialogs.ConfirmAnswer = false;

				await harness.ViewModel.CreateAsync();
				Assert.Equal(777, harness.Canvas.FindNode("Node001")!.X);

				harness.Dialogs.ConfirmAnswer = true;
				await harness.ViewModel.CreateAsync();
				Assert.NotEqual(777, harness.Canvas.FindNode("Node001")!.X);
				Assert.True(harness.ViewModel.IsHintVisible);
			});
		}

		[Fact]
		public void Hide_Unhide_AndJump()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				await harness.LoadAsync();
				DiagramNode node1 = harness.Canvas.FindNode("Node001")!;
				DiagramNode node2 = harness.Canvas.FindNode("Node002")!;
				harness.Canvas.Select(node1);
				harness.Canvas.Select(node2, true);

				harness.ViewModel.HideSelection();

				Assert.True(node1.IsHidden);
				Assert.True(node2.IsHidden);
				Assert.Empty(harness.Canvas.SelectedItems);

				harness.ViewModel.PrepareContext(null);
				Assert.Equal(new[] { "Node001", "Node002" }, harness.ViewModel.HiddenNodeNames.ToArray());

				harness.ViewModel.UnhideNode("Node001");
				Assert.False(node1.IsHidden);

				harness.ViewModel.JumpToNode("Node002");
				Assert.False(node2.IsHidden);
				Assert.Same(node2, harness.Canvas.SelectedItem);

				node1.IsHidden = true;
				node2.IsHidden = true;
				node1.X = 0;
				node1.Y = 0;
				harness.ViewModel.UnhideAll();
				Assert.False(node1.IsHidden);
				Assert.False(node2.IsHidden);
				Assert.Equal(20, node1.Y);
			});
		}

		[Fact]
		public void FunctionRules_OpenTheConfigurationAndRefreshTheChart()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				await harness.LoadAsync();
				Int32 sources = harness.Messages.SourcesCreated;

				await harness.ViewModel.ShowFunctionRulesAsync();

				Assert.Contains("ShowFunctionRulesAsync", harness.ToolRecorder.Calls);
				Assert.True(harness.Messages.SourcesCreated > sources);
			});
		}

		[Fact]
		public void ShowCommentCode_RebuildsTheNodes()
		{
			StaTestRunner.Run(async () =>
			{
				using FlowchartHarness harness = new FlowchartHarness();
				harness.Script.TextDocument.Text = harness.Script.TextDocument.Text.Replace("   Reply(130);", "   Reply(130);\r\n   // NOption(131, Node001, 4);", StringComparison.Ordinal);
				await harness.LoadAsync();
				Int32 rows = harness.Canvas.FindNode("Node777")!.Rows.Count;

				harness.ViewModel.ShowCommentCode = true;
				await Task.Delay(50);

				Assert.True(harness.Canvas.FindNode("Node777")!.Rows.Count > rows);
			});
		}

		[Fact]
		public void Hint_ShowsAndDismisses()
		{
			StaTestRunner.Run(() =>
			{
				using FlowchartHarness harness = new FlowchartHarness();

				harness.ViewModel.ShowHint("Something", 60);
				Assert.True(harness.ViewModel.IsHintVisible);
				Assert.Equal("Something", harness.ViewModel.Hint);

				harness.ViewModel.DismissHint();
				Assert.False(harness.ViewModel.IsHintVisible);
				return Task.CompletedTask;
			});
		}
	}
}