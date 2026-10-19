namespace FalloutScriptStudio.Core.Tests.Dialogue
{
	using System;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Dialogue;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Core.Tests.TextTools;
	using FalloutScriptStudio.Core.TextTools;
	using Xunit;

	public class DialogueNodeToolsTests
	{
		private readonly DialogueNodeFactory _factory = new DialogueNodeFactory(new ProcedureTextService());
		private readonly DialogueNodeEditor _editor = new DialogueNodeEditor(new DialogueNodeParser(new DialogueFunctionRules()));

		[Fact]
		public void Templates_AreTheLegacyOnes()
		{
			Assert.Equal(new[] { "<Empty>", "Template Code 1", "Template Code 2", "Template Code 3" }, DialogueNodeTemplate.All.Select(t => t.Name).ToArray());
			Assert.Equal(String.Empty, DialogueNodeTemplate.All[0].Code);
			Assert.Equal("\r\n   NMessage(100);\r\n", DialogueNodeTemplate.All[3].Code);
			Assert.Contains("NOption(101, Node999, 4);", DialogueNodeTemplate.All[1].Code);
			Assert.Equal("Node###", DialogueNodeTemplate.DefaultNodeName);
		}

		[Theory]
		[InlineData("Node003", "Node003", NodeCreationError.None)]
		[InlineData("003", "Node003", NodeCreationError.None)]
		[InlineData("  nodeX ", "nodeX", NodeCreationError.None)]
		[InlineData("MyNode_1", "MyNode_1", NodeCreationError.None)]
		[InlineData("", "", NodeCreationError.EmptyName)]
		[InlineData("Node###", "Node###", NodeCreationError.PlaceholderInName)]
		[InlineData("Node001", "Node001", NodeCreationError.NameExists)]
		[InlineData("node001", "node001", NodeCreationError.NameExists)]
		[InlineData("a b", "Nodea b", NodeCreationError.InvalidName)]
		[InlineData("start", "Nodestart", NodeCreationError.None)]
		public void CheckName_NormalizesAndValidates(String requested, String expectedName, NodeCreationError expectedError)
		{
			(String name, NodeCreationError error) = this._factory.CheckName(requested, DialogueSamples.CreateInfo());

			Assert.Equal(expectedName, name);
			Assert.Equal(expectedError, error);
		}

		[Fact]
		public void IsValidNodeName_RequiresTheWordNode()
		{
			Assert.True(DialogueNodeFactory.IsValidNodeName("MyNODE"));
			Assert.False(DialogueNodeFactory.IsValidNodeName("Branch1"));
		}

		[Fact]
		public void Create_AppendsTheNodeAndItsDeclaration()
		{
			String script = DialogueSamples.Script();

			NodeCreationResult result = this._factory.Create(script, DialogueSamples.CreateInfo(), "002a", DialogueNodeTemplate.All[3].Code);

			Assert.True(result.Succeeded);
			Assert.Equal("Node002a", result.NodeName);
			String text = result.Edit!.NewText;
			Assert.Equal(text, TextEditApplier.Apply(script, result.Edit.Edits));
			Assert.Contains("procedure Node002a;", text);
			Assert.EndsWith("procedure Node002a begin\r\n\r\n   NMessage(100);\r\n\r\nend", text);
			ProgramInfo reparsed = new InternalParser().Parse(text, null);
			Assert.NotNull(reparsed.GetProcedure("Node002a"));
			Assert.True(result.Edit.FocusLine > 0);
		}

		[Fact]
		public void Create_WithABadName_ReturnsTheError()
		{
			NodeCreationResult result = this._factory.Create(DialogueSamples.Script(), DialogueSamples.CreateInfo(), "Node001", String.Empty);

			Assert.False(result.Succeeded);
			Assert.Equal(NodeCreationError.NameExists, result.Error);
			Assert.Null(result.Edit);
		}

		[Fact]
		public async Task GetMessageRowsAsync_ListsTheMessagesOfTheNodeCode()
		{
			FakeMessageSource messages = DialogueSamples.Messages();
			String code = TestScripts.Crlf("   Reply(mstr(110));", "", "   NOption(101, Node002, 4);", "   NOption(999, Node002, 4);", "   x := 1;", "   call Node002;");

			var rows = await this._editor.GetMessageRowsAsync(code, DialogueSamples.CreateInfo(), messages);

			Assert.Equal(new[] { 0, 2, 3 }, rows.Select(r => r.CodeLineIndex).ToArray());
			Assert.Equal("Reply from message_str.", rows[0].Text);
			Assert.EndsWith("other.msg", rows[0].MessageFilePath);
			Assert.False(rows[0].IsError);
			Assert.True(rows[1].IsOption);
			Assert.Equal("I need your help.", rows[1].Text);
			Assert.True(rows[2].IsError);
			Assert.Equal(999, rows[2].MessageNumber);
			Assert.Contains(123, messages.Requested);
		}

		[Fact]
		public void CreateMessageNumberEdit_ReplacesTheNumberInTheStatement()
		{
			String code = TestScripts.Crlf("   NOption(101, Node002, 4);", "", "   NOption(MSG_HELLO, Node001, 4);   NOption(101, Node999, 101);");
			ProgramInfo info = DialogueSamples.CreateInfo();
			var parser = new DialogueNodeParser(new DialogueFunctionRules());
			var lines = parser.ParseNode(code, info, new DialogueNodeParseOptions());

			TextEdit? first = this._editor.CreateMessageNumberEdit(code, lines[0], "205");
			TextEdit? viaMacro = this._editor.CreateMessageNumberEdit(code, lines[1], "205");
			TextEdit? second = this._editor.CreateMessageNumberEdit(code, lines[2], "300");

			Assert.Equal("   NOption(205, Node002, 4);", TextEditApplier.Apply(code, new[] { first! }).Split("\r\n")[0]);
			Assert.Null(viaMacro);
			Assert.Equal("   NOption(MSG_HELLO, Node001, 4);   NOption(300, Node999, 101);", TextEditApplier.Apply(code, new[] { second! }).Split("\r\n")[2]);
		}

		[Fact]
		public void CreateMessageNumberEdit_LineOutsideTheText_ReturnsNull()
		{
			var line = new DialogueLine { Opcode = DialogueOpcode.Reply, MessageNumber = 5, CodeLineIndex = 10, ShortCode = "Reply(5)" };

			Assert.Null(this._editor.CreateMessageNumberEdit("Reply(5);", line, "6"));
		}
	}
}