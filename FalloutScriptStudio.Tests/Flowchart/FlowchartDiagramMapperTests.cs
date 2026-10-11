namespace FalloutScriptStudio.Tests.Flowchart
{
	using System;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Controls.Diagram.Model;
	using FalloutScriptStudio.Core.Dialogue;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Core.TextTools;
	using FalloutScriptStudio.Services.ScriptTools.Flowchart;
	using Xunit;

	public class FlowchartDiagramMapperTests
	{
		private static async Task<FlowchartModel> BuildAsync()
		{
			DialogueNodeParser parser = new DialogueNodeParser(new DialogueFunctionRules());
			FlowchartBuilder builder = new FlowchartBuilder(parser, new ProcedureTextService());
			String text = FlowchartSamples.Script();
			ProgramInfo info = new InternalParser().Parse(text, FlowchartSamples.ScriptPath);
			return await builder.BuildAsync(text, info, FlowchartSamples.CreateSource(), new FlowchartBuildOptions { TabSize = 3 });
		}

		[Fact]
		public async Task CreateNode_MakesOneRowPerLineWithTheKindOfTheStatement()
		{
			FlowchartModel model = await BuildAsync();

			DiagramNode node = FlowchartDiagramMapper.CreateNode(model.FindNode("Node001")!, true);

			Assert.Equal(DiagramNodeKind.Default, node.Kind);
			Assert.Equal(3, node.Rows.Count);
			Assert.Equal(DiagramRowKind.Reply, node.Rows[0].Kind);
			Assert.Equal("\"I am John.\"", node.Rows[0].Text);
			Assert.Equal("Reply(110);", node.Rows[0].Detail);
			Assert.Equal(DiagramRowKind.Option, node.Rows[1].Kind);
			Assert.Equal("Tell me more.", node.Rows[1].Text);
			Assert.Equal(DiagramRowKind.Call, node.Rows[2].Kind);
			Assert.Equal("call Node999;", node.Rows[2].Text);
		}

		[Fact]
		public async Task CreateNode_LinksTheRowsThatJump()
		{
			FlowchartModel model = await BuildAsync();

			DiagramNode node = FlowchartDiagramMapper.CreateNode(model.FindNode("Node001")!, true);

			Assert.Null(node.Rows[0].LinkTargetName);
			Assert.Equal("Node002", node.Rows[1].LinkTargetName);
			Assert.Equal("Node999", node.Rows[2].LinkTargetName);
		}

		[Fact]
		public async Task CreateNode_UsesTheKindAndHeaderOfTheRoleOfTheNode()
		{
			FlowchartModel model = await BuildAsync();

			DiagramNode start = FlowchartDiagramMapper.CreateNode(model.FindNode("talk_p_proc")!, true);
			DiagramNode unused = FlowchartDiagramMapper.CreateNode(model.FindNode("Node777")!, true);
			DiagramNode end = FlowchartDiagramMapper.CreateNode(model.FindNode("Node999")!, true);
			DiagramNode inner = FlowchartDiagramMapper.CreateNode(model.FindNode("Node002")!, true);

			Assert.Equal(DiagramNodeKind.Start, start.Kind);
			Assert.Equal(new[] { FlowchartDiagramMapper.StartNodeCaption }, start.HeaderLines.ToArray());
			Assert.Equal(DiagramNodeKind.Unused, unused.Kind);
			Assert.True(unused.IsCollapsed);
			Assert.Equal(new[] { FlowchartDiagramMapper.UnusedCaption }, unused.HeaderLines.ToArray());
			Assert.Equal(DiagramNodeKind.End, end.Kind);
			Assert.True(end.IsCollapsed);
			Assert.Equal(new[] { "talk_p_proc : Node002", "Node001" }, end.HeaderLines.ToArray());
			Assert.Equal(new[] { "Node001" }, inner.HeaderLines.ToArray());
		}

		[Fact]
		public async Task CreateNode_AppliesTheCodeDefault()
		{
			FlowchartModel model = await BuildAsync();

			Assert.True(FlowchartDiagramMapper.CreateNode(model.FindNode("Node001")!, true).ShowCodeRows);
			Assert.False(FlowchartDiagramMapper.CreateNode(model.FindNode("Node001")!, false).ShowCodeRows);
		}

		[Fact]
		public async Task CaptureAndApplyState_KeepPositionSizeAndExpandedRows()
		{
			FlowchartModel model = await BuildAsync();
			DiagramNode node = FlowchartDiagramMapper.CreateNode(model.FindNode("Node001")!, true);
			node.X = 120;
			node.Y = 340;
			node.Width = 260;
			node.IsHidden = true;
			node.Rows[1].IsExpanded = true;
			node.ShowCodeRows = false;
			node.HasUserShowCodeSetting = true;

			FlowchartNodeState state = FlowchartDiagramMapper.CaptureState(node);
			DiagramNode copy = FlowchartDiagramMapper.CreateNode(model.FindNode("Node001")!, true);
			FlowchartDiagramMapper.ApplyState(copy, state);

			Assert.Equal(120, copy.X);
			Assert.Equal(340, copy.Y);
			Assert.Equal(260, copy.Width);
			Assert.True(copy.IsHidden);
			Assert.False(copy.ShowCodeRows);
			Assert.True(copy.HasUserShowCodeSetting);
			Assert.True(copy.Rows[1].IsExpanded);
			Assert.False(copy.Rows[0].IsExpanded);
			Assert.Equal(new[] { true, false }, state.Contents.Select(c => c.Collapsed).ToArray());
			Assert.Equal(new[] { "0", "1" }, state.Contents.Select(c => c.Line).ToArray());
		}

		[Fact]
		public async Task CaptureState_WithoutAUserCodeSetting_FollowsTheGlobalDefault()
		{
			FlowchartModel model = await BuildAsync();
			DiagramNode node = FlowchartDiagramMapper.CreateNode(model.FindNode("Node001")!, false);

			Assert.Null(FlowchartDiagramMapper.CaptureState(node).ShowCode);
		}

		[Fact]
		public async Task CreateDocument_WritesHiddenNodesFirstAndThenTheNotes()
		{
			FlowchartModel model = await BuildAsync();
			DiagramNode a = FlowchartDiagramMapper.CreateNode(model.FindNode("Node001")!, true);
			DiagramNode b = FlowchartDiagramMapper.CreateNode(model.FindNode("Node002")!, true);
			DiagramNode c = FlowchartDiagramMapper.CreateNode(model.FindNode("Node777")!, true);
			b.IsHidden = true;
			DiagramNote note = new DiagramNote { Text = "remember", X = 10, Y = 20 };

			FlowchartDocument document = FlowchartDiagramMapper.CreateDocument(new DiagramItem[] { a, b, note, c }, "dcjohn.ssl", 1.5, 7, 9);

			Assert.Equal(new[] { "Node002", "Node001", "Node777" }, document.Nodes.Select(n => n.Name).ToArray());
			Assert.Equal("remember", Assert.Single(document.Notes).Text);
			Assert.Equal(1.5f, document.Zoom);
			Assert.Equal(7, document.PositionX);
			Assert.Equal(9, document.PositionY);
		}

		[Fact]
		public void CreateNote_RestoresTheSavedNote()
		{
			DiagramNote note = FlowchartDiagramMapper.CreateNote(new FlowchartNoteState("text") { X = 5, Y = 6, Width = 150, Height = 70 });

			Assert.Equal("text", note.Text);
			Assert.Equal(5, note.X);
			Assert.Equal(150, note.Width);
			Assert.Equal(70, note.Height);
		}

		[Theory]
		[InlineData("Node001", "Node001")]
		[InlineData("\"Node001\"", "Node001")]
		[InlineData(" Node001 ", "Node001")]
		public void NormalizeTarget_RemovesQuotesAndSpaces(String target, String expected)
		{
			Assert.Equal(expected, FlowchartDiagramMapper.NormalizeTarget(target));
		}
	}
}
