namespace FalloutScriptStudio.Core.Tests.Dialogue
{
	using System;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Dialogue;
	using FalloutScriptStudio.Core.Messages;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Core.TextTools;
	using Xunit;

	public class FlowchartBuilderTests
	{
		private readonly FlowchartBuilder _builder = new FlowchartBuilder(new DialogueNodeParser(new DialogueFunctionRules()), new ProcedureTextService());

		private async Task<FlowchartModel> BuildAsync(FlowchartBuildOptions? options = null, String? script = null, FakeMessageSource? messages = null)
		{
			String text = script ?? DialogueSamples.Script();
			return await this._builder.BuildAsync(text, DialogueSamples.CreateInfo(text), messages ?? DialogueSamples.Messages(), options ?? new FlowchartBuildOptions { TabSize = 3 });
		}

		[Fact]
		public async Task Build_ListsTheNodesInScriptOrder()
		{
			FlowchartModel model = await this.BuildAsync();

			Assert.Equal(new[] { "talk_p_proc", "Node001", "Node002", "Node999", "Node777" }, model.NodeNames.ToArray());
			Assert.Same(model.Nodes[1], model.FindNode("NODE001"));
			Assert.Null(model.FindNode("start"));
		}

		[Fact]
		public async Task Build_WorksOutNodeKinds()
		{
			FlowchartModel model = await this.BuildAsync();

			Assert.Equal(DialogueNodeKind.DialogStart, model.FindNode("talk_p_proc")!.Kind);
			Assert.Equal(DialogueNodeKind.Default, model.FindNode("Node001")!.Kind);
			Assert.Equal(DialogueNodeKind.Default, model.FindNode("Node002")!.Kind);
			Assert.Equal(DialogueNodeKind.DialogEnd, model.FindNode("Node999")!.Kind);
			Assert.Equal(DialogueNodeKind.Unused, model.FindNode("Node777")!.Kind);
		}

		[Fact]
		public async Task Build_NodeWithoutIncomingLinks_IsMarked()
		{
			String script = DialogueSamples.Script().Replace("NOption(101, Node001, 4)", "NOption(101, Node999, 4)");

			FlowchartModel model = await this.BuildAsync(script: script);

			Assert.Equal(DialogueNodeKind.NoFromLink, model.FindNode("Node001")!.Kind);
		}

		[Fact]
		public async Task Build_CollectsLinksWithTheirContentLines()
		{
			FlowchartModel model = await this.BuildAsync();

			FlowchartNodeData talk = model.FindNode("talk_p_proc")!;
			Assert.Equal(
				new[] { ("Node001", 2), ("Node999", 3), ("Node002", 5), ("Node999", 7) },
				talk.LinkedTo.Select(l => (l.TargetNode, l.ContentLine)).ToArray());
			Assert.Equal(8, talk.Content.Count);

			FlowchartNodeData node1 = model.FindNode("Node001")!;
			Assert.Equal(new[] { ("Node002", 5), ("Node999", 6), ("Node002", 7) }, node1.LinkedTo.Select(l => (l.TargetNode, l.ContentLine)).ToArray());
		}

		[Fact]
		public async Task Build_CollectsIncomingLinks()
		{
			FlowchartModel model = await this.BuildAsync();

			FlowchartNodeData exit = model.FindNode("Node999")!;
			Assert.Equal(new[] { "talk_p_proc", "Node001", "Node002" }, exit.LinkedFrom.ToArray());
			Assert.Equal(new[] { "talk_p_proc : Node002", "Node001" }, exit.LinkedFromColumns.ToArray());
			Assert.Empty(model.FindNode("Node777")!.LinkedFrom);
		}

		[Fact]
		public async Task Build_ContentLinesCarryCodeAndMessageTexts()
		{
			FlowchartModel model = await this.BuildAsync();

			FlowchartContentLine[] talk = model.FindNode("talk_p_proc")!.Content.ToArray();
			Assert.Equal("Reply(MSG_HELLO);", talk[0].Code);
			Assert.Equal("Hello there, stranger.", talk[0].MessageText);
			Assert.Equal((DialogueOpcode.Reply, 100, -1, 0), (talk[0].Opcode, talk[0].MessageNumber, talk[0].MessageFileNumber, talk[0].Index));
			Assert.Equal("if (global_var(3) == 1) then begin", talk[3].Code);
			Assert.Equal(DialogueOpcode.None, talk[3].Opcode);
			Assert.Null(talk[3].MessageText);
			Assert.Equal("I need your help.", talk[1].MessageText);
		}

		[Fact]
		public async Task Build_ResolvesMessagesOfOtherFiles_AndRequestsThem()
		{
			FakeMessageSource messages = DialogueSamples.Messages();

			FlowchartModel model = await this.BuildAsync(messages: messages);

			FlowchartContentLine[] node1 = model.FindNode("Node001")!.Content.ToArray();
			Assert.Equal("Reply from message_str.", node1[0].MessageText);
			Assert.Equal("A message.", node1[1].MessageText);
			Assert.Equal("Reply from gsay_reply.", node1[2].MessageText);
			Assert.Equal("Message from gsay_message.", node1[3].MessageText);
			Assert.Equal("Option from gsay_option.", node1[4].MessageText);
			Assert.Equal("Option from giq_option.", node1[5].MessageText);
			Assert.Null(node1[6].MessageText);
			Assert.Contains(123, messages.Requested);
		}

		[Fact]
		public async Task Build_MissingMessages_ShowTheLegacyErrorTexts()
		{
			String script = DialogueSamples.Script().Replace("Reply(130);", "Reply(999);").Replace("Reply(120);", "Reply(some_unknown);");

			FlowchartModel model = await this.BuildAsync(script: script);

			Assert.Equal(MessageFileTexts.TextNotFound, model.FindNode("Node777")!.Content[0].MessageText);
			Assert.Equal(FlowchartBuilder.NoMessageNumberText, model.FindNode("Node002")!.Content[0].MessageText);
		}

		[Fact]
		public async Task Build_MissingMessageFile_ShowsTheLegacyErrorText()
		{
			var messages = new FakeMessageSource();
			messages.SetFile(-1, DialogueSamples.OwnMessages(), "own.msg");

			FlowchartModel model = await this.BuildAsync(messages: messages);

			Assert.Equal(String.Format(System.Globalization.CultureInfo.InvariantCulture, MessageFileTexts.FileNotFoundFormat, 123), model.FindNode("Node001")!.Content[0].MessageText);
		}

		[Fact]
		public async Task Build_CanLeaveOutTheExitNode()
		{
			FlowchartModel model = await this.BuildAsync(new FlowchartBuildOptions { ExcludeExitNode = true, TabSize = 3 });

			Assert.DoesNotContain("Node999", model.NodeNames);
			Assert.Equal(new[] { "Node999", "Node999" }, model.FindNode("talk_p_proc")!.LinkedTo.Where(l => l.TargetNode == "Node999").Select(l => l.TargetNode).ToArray());
		}

		[Fact]
		public async Task Build_IndentationOption_ControlsTheShownCode()
		{
			FlowchartModel plain = await this.BuildAsync(new FlowchartBuildOptions { TabSize = 0 });
			FlowchartModel wide = await this.BuildAsync(new FlowchartBuildOptions { TabSize = 4 });

			Assert.Equal("   Reply(MSG_HELLO);", plain.FindNode("talk_p_proc")!.Content[0].Code);
			Assert.Equal("   Reply(MSG_HELLO);", wide.FindNode("talk_p_proc")!.Content[0].Code);
		}

		[Fact]
		public async Task Build_CommentedCode_IsShownOnlyOnRequest()
		{
			String script = DialogueSamples.Script().Replace("   Reply(120);", "   // NOption(5, Node777, 4);\r\n   Reply(120);");

			FlowchartModel hidden = await this.BuildAsync(script: script);
			FlowchartModel shown = await this.BuildAsync(new FlowchartBuildOptions { ShowCommentedCode = true, TabSize = 3 }, script);

			Assert.Equal(DialogueNodeKind.Unused, hidden.FindNode("Node777")!.Kind);
			Assert.Equal(DialogueNodeKind.DialogEnd, shown.FindNode("Node777")!.Kind);
			Assert.Equal(2, hidden.FindNode("Node002")!.Content.Count);
			Assert.Equal(3, shown.FindNode("Node002")!.Content.Count);
		}

		[Fact]
		public async Task Build_ScriptWithoutNodes_IsEmpty()
		{
			FlowchartModel model = await this.BuildAsync(script: "procedure start begin\r\nend");

			Assert.Empty(model.Nodes);
		}

		[Fact]
		public async Task Build_DeclaredButNotDefinedNode_IsSkipped()
		{
			FlowchartModel model = await this.BuildAsync(script: "procedure Node001;\r\nprocedure talk_p_proc begin\r\n   call Node001;\r\nend");

			Assert.Equal(new[] { "talk_p_proc" }, model.NodeNames.ToArray());
		}
	}
}
