namespace FalloutScriptStudio.Core.Tests.Dialogue
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using FalloutScriptStudio.Core.Dialogue;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Core.Tests.TextTools;
	using FalloutScriptStudio.Core.TextTools;
	using Xunit;

	public class DialogueNodeParserTests
	{
		private static IReadOnlyList<DialogueLine> Parse(String code, DialogueNodeParseOptions? options = null, DialogueFunctionRules? rules = null)
		{
			return DialogueSamples.Parse(code, options, rules);
		}

		private static DialogueLine Single(String code, DialogueFunctionRules? rules = null)
		{
			return Assert.Single(Parse(code, null, rules));
		}

		[Fact]
		public void Reply_WithMacroNumber_ResolvesTheMacro()
		{
			DialogueLine line = Single("   Reply(MSG_HELLO);");

			Assert.Equal(DialogueOpcode.Reply, line.Opcode);
			Assert.Equal(100, line.MessageNumber);
			Assert.Equal(-1, line.MessageFileNumber);
			Assert.Equal("[Reply]", line.ToNode);
			Assert.Equal("Reply(MSG_HELLO)", line.ShortCode);
			Assert.Equal("   Reply(MSG_HELLO);", line.Code);
		}

		[Theory]
		[InlineData("   Reply(100);", 100)]
		[InlineData("   Reply( 100 );", 100)]
		[InlineData("   Reply(100)", 100)]
		[InlineData("   Reply(MSG_BYE);", 101)]
		[InlineData("   Reply(MSG_HELLO + 5);", 105)]
		[InlineData("   Reply(100 + 2);", 102)]
		[InlineData("   Reply(random(110, 112));", 110)]
		public void Reply_NumberExpressions(String code, Int32 expected)
		{
			Assert.Equal(expected, Single(code).MessageNumber);
		}

		[Fact]
		public void Reply_WithMessageStrMacro_ResolvesMessageAndFile()
		{
			DialogueLine line = Single("   Reply(mstr(110));");

			Assert.Equal(110, line.MessageNumber);
			Assert.Equal(123, line.MessageFileNumber);
		}

		[Fact]
		public void Reply_WithInlineMessageStr_ResolvesMessageAndFile()
		{
			DialogueLine line = Single("   Reply(message_str(NAME, 110));");

			Assert.Equal(110, line.MessageNumber);
			Assert.Equal(123, line.MessageFileNumber);
		}

		[Fact]
		public void Reply_SelfReferencingMacro_DoesNotHang()
		{
			DialogueLine line = Single("   Reply(LOOP);");

			Assert.Equal(DialogueOpcode.Reply, line.Opcode);
			Assert.True(line.MessageNumber <= 0);
		}

		[Fact]
		public void Message_IsReportedAsMessage()
		{
			DialogueLine line = Single("   NMessage(111);");

			Assert.Equal(DialogueOpcode.Message, line.Opcode);
			Assert.Equal(111, line.MessageNumber);
			Assert.Equal("[Message]", line.ToNode);
			Assert.True(line.IsNpcText);
		}

		[Fact]
		public void Option_WithIq_ReadsNodeAndIq()
		{
			DialogueLine line = Single("   NOption(101, Node001, 4);");

			Assert.Equal(DialogueOpcode.Option, line.Opcode);
			Assert.Equal(101, line.MessageNumber);
			Assert.Equal("Node001", line.ToNode);
			Assert.Equal("4", line.Iq);
			Assert.Equal("NOption(101, Node001, 4)", line.ShortCode);
			Assert.True(line.IsOption);
			Assert.True(line.IsTransition);
		}

		[Fact]
		public void Option_WithoutIq_ReadsNode()
		{
			DialogueLine line = Single("   NLowOption(102, Node999);");

			Assert.Equal("Node999", line.ToNode);
			Assert.Equal(102, line.MessageNumber);
			Assert.Null(line.Iq);
		}

		[Fact]
		public void Option_WithNegativeIqAndMacroNumber()
		{
			Assert.Equal("-1", Single("      BOption(104, Node999, -1);").Iq);
			Assert.Equal(101, Single("      GOption(MSG_BYE, Node002, 5);").MessageNumber);
			Assert.Equal("Node002", Single("      GOption(MSG_BYE, Node002, 5);").ToNode);
		}

		[Fact]
		public void Option_NodeWithAtSign_IsCleaned()
		{
			Assert.Equal("Node001", Single("   NOption(101, @Node001, 4);").ToNode);
		}

		[Fact]
		public void GsayReply_And_GsayMessage_UseTheBuiltInRules()
		{
			DialogueLine reply = Single("   gsay_reply(NAME, 112);");
			DialogueLine message = Single("   gsay_message(NAME, 113, 50);");

			Assert.Equal(DialogueOpcode.Reply, reply.Opcode);
			Assert.Equal((112, 123), (reply.MessageNumber, reply.MessageFileNumber));
			Assert.Equal("gsay_reply(NAME, 112)", reply.ShortCode);
			Assert.Equal(DialogueOpcode.Message, message.Opcode);
			Assert.Equal((113, 123), (message.MessageNumber, message.MessageFileNumber));
		}

		[Fact]
		public void GsayOption_And_GiqOption_UseTheBuiltInRules()
		{
			DialogueLine gsay = Single("   gsay_option(NAME, 114, Node002, 50);");
			DialogueLine giq = Single("   giq_option(4, NAME, 115, Node999, 50);");

			Assert.Equal(DialogueOpcode.GsayOption, gsay.Opcode);
			Assert.Equal((114, 123, "Node002", "-"), (gsay.MessageNumber, gsay.MessageFileNumber, gsay.ToNode, gsay.Iq));
			Assert.Equal(DialogueOpcode.GiqOption, giq.Opcode);
			Assert.Equal((115, 123, "Node999", "4"), (giq.MessageNumber, giq.MessageFileNumber, giq.ToNode, giq.Iq));
		}

		[Fact]
		public void GsayFunctions_WithComplexArguments_FallBackToTheGenericParser()
		{
			DialogueLine reply = Single("   gsay_reply(NAME, mstr(112));");
			DialogueLine option = Single("   gsay_option(NAME, message_str(NAME, 114), Node002, 50);");

			Assert.Equal(112, reply.MessageNumber);
			Assert.Equal(123, reply.MessageFileNumber);
			Assert.Equal(DialogueOpcode.GsayOption, option.Opcode);
			Assert.Equal(("Node002", 114, 123), (option.ToNode, option.MessageNumber, option.MessageFileNumber));
		}

		[Fact]
		public void Call_ReadsTheNodeName()
		{
			DialogueLine line = Single("   call Node002;");

			Assert.Equal(DialogueOpcode.Call, line.Opcode);
			Assert.Equal("Node002", line.ToNode);
			Assert.Equal("call Node002", line.ShortCode);
			Assert.Equal(-1, line.MessageNumber);
			Assert.True(line.IsTransition);
		}

		[Theory]
		[InlineData("   call Node002 in 5;", "Node002")]
		[InlineData("   call Node002 when (x == 1);", "Node002")]
		[InlineData("   call Node002(1, 2);", "Node002")]
		[InlineData("   call Node002", "Node002")]
		public void Call_StopsAtTheEndOfTheName(String code, String expected)
		{
			Assert.Equal(expected, Single(code).ToNode);
		}

		[Fact]
		public void Call_InsideAWord_IsIgnored()
		{
			Assert.Empty(Parse("   x := recall_value;\r\n   call_other(1);"));
		}

		[Fact]
		public void UserRule_ForAMacroWithMessageNodeAndIq()
		{
			var rules = new DialogueFunctionRules(new[] { DialogueFunctionRule.FromOneBased(DialogueOpcode.Option, "MyOption", messageArg: 1, nodeArg: 2, iqArg: 3, totalArgs: 3) });

			DialogueLine line = Single("   MyOption(MSG_HELLO, Node001, 6);", rules);

			Assert.Equal(DialogueOpcode.Option, line.Opcode);
			Assert.Equal((100, "Node001", "6"), (line.MessageNumber, line.ToNode, line.Iq));
		}

		[Fact]
		public void UserRules_AreFoundInTheOrderOfTheLine_NotInTheOrderOfTheRules()
		{
			var rules = new DialogueFunctionRules(new[]
			{
				DialogueFunctionRule.FromOneBased(DialogueOpcode.Option, "OptA", messageArg: 1, nodeArg: 2, iqArg: 3, totalArgs: 3),
				DialogueFunctionRule.FromOneBased(DialogueOpcode.Option, "OptB", messageArg: 1, nodeArg: 2, iqArg: 3, totalArgs: 3),
			});

			IReadOnlyList<DialogueLine> lines = Parse("   OptB(101, Node001, 4); OptA(102, Node002, 5);", null, rules);

			Assert.Equal(new[] { (101, "Node001"), (102, "Node002") }, lines.Select(l => (l.MessageNumber, l.ToNode ?? String.Empty)).ToArray());
		}

		[Fact]
		public void UserRule_ForANodeJumpMacro()
		{
			var rules = new DialogueFunctionRules(new[] { DialogueFunctionRule.FromOneBased(DialogueOpcode.CallMacro, "GotoNode", messageArg: 0, nodeArg: 1, totalArgs: 1) });

			DialogueLine line = Single("   GotoNode(Node005);", rules);

			Assert.Equal(DialogueOpcode.Call, line.Opcode);
			Assert.Equal("Node005", line.ToNode);
			Assert.Equal("GotoNode(Node005)", line.ShortCode);
		}

		[Fact]
		public void UserRule_ForAJumpMacroWithSeveralArguments()
		{
			var rules = new DialogueFunctionRules(new[] { DialogueFunctionRule.FromOneBased(DialogueOpcode.CallMacro, "Jump", messageArg: 0, nodeArg: 2, totalArgs: 2) });

			Assert.Equal("Node005", Single("   Jump(1, Node005);", rules).ToNode);
		}

		[Fact]
		public void UserRule_ForAReplyMacro_IsReportedOnce()
		{
			var rules = new DialogueFunctionRules(new[] { DialogueFunctionRule.FromOneBased(DialogueOpcode.Reply, "Say", messageArg: 1, totalArgs: 1) });

			DialogueLine line = Single("   Say(MSG_HELLO);", rules);

			Assert.Equal(DialogueOpcode.Reply, line.Opcode);
			Assert.Equal(100, line.MessageNumber);
		}

		[Fact]
		public void UserRule_ForAMessageMacro_IsReportedAsMessage()
		{
			var rules = new DialogueFunctionRules(new[] { DialogueFunctionRule.FromOneBased(DialogueOpcode.Message, "Shout", messageArg: 1, totalArgs: 1) });

			DialogueLine line = Single("   Shout(111);", rules);

			Assert.Equal(DialogueOpcode.Message, line.Opcode);
		}

		[Fact]
		public void SeveralStatementsOnOneLine_AreAllFound()
		{
			IReadOnlyList<DialogueLine> lines = Parse("   NMessage(111); NOption(101, Node001, 4); call Node002;");

			Assert.Equal(new[] { DialogueOpcode.Message, DialogueOpcode.Option, DialogueOpcode.Call }, lines.Select(l => l.Opcode).ToArray());
		}

		[Fact]
		public void CommentedCode_IsSkippedByDefault()
		{
			String code = TestScripts.Crlf("   // NOption(1, Node001, 4);", "   /* Reply(5);", "   Reply(6); */", "   Reply(7); // Reply(8)", "   Reply(9);");

			IReadOnlyList<DialogueLine> lines = Parse(code);

			Assert.Equal(new[] { 7, 9 }, lines.Select(l => l.MessageNumber).ToArray());
		}

		[Fact]
		public void CommentedCode_IsParsedWhenCommentsAreNotExcluded()
		{
			IReadOnlyList<DialogueLine> lines = Parse("   // NOption(1, Node001, 4);\r\n   Reply(9);", new DialogueNodeParseOptions { ExcludeComments = false, IncludePlainCode = true });

			Assert.Equal(new[] { DialogueOpcode.Option, DialogueOpcode.Reply }, lines.Select(l => l.Opcode).ToArray());
		}

		[Theory]
		[InlineData("   NOption(")]
		[InlineData("   NOption(1")]
		[InlineData("   NOption(1);")]
		[InlineData("   NOption();")]
		[InlineData("   Reply(")]
		[InlineData("   Reply();")]
		[InlineData("   gsay_option(NAME);")]
		[InlineData("   giq_option(1, 2);")]
		[InlineData("   gsay_message(1);")]
		[InlineData("   call")]
		[InlineData("   call ;")]
		[InlineData("   Reply(\"unterminated")]
		[InlineData("   x := Option;")]
		[InlineData("   Message")]
		public void MalformedStatements_NeverThrow(String code)
		{
			Exception? error = Record.Exception(() => Parse(code));

			Assert.Null(error);
		}

		[Fact]
		public void MessageStr_IsNotAMessageStatement()
		{
			Assert.Empty(Parse("   display_msg(message_str(NAME, 5));"));
		}

		[Fact]
		public void DiagramParse_ReturnsEveryLineAndSplitsElse()
		{
			String code = TestScripts.Crlf(
				"\tReply(MSG_HELLO);",
				"\tif (x) then NOption(101, Node001, 4); else NOption(102, Node999, 4);",
				"\t// NOption(103, Node777, 4);",
				"\tset_global_var(1, 2);");

			IReadOnlyList<DialogueLine> lines = new DialogueNodeParser(new DialogueFunctionRules()).ParseNodeForDiagram(code, DialogueSamples.CreateInfo(), true);

			Assert.Equal(
				new[] { DialogueOpcode.Reply, DialogueOpcode.Option, DialogueOpcode.Option, DialogueOpcode.None },
				lines.Select(l => l.Opcode).ToArray());
			Assert.Equal("   Reply(MSG_HELLO);", lines[0].Code);
			Assert.Equal("   if (x) then NOption(101, Node001, 4);", lines[1].Code);
			Assert.Equal("   else NOption(102, Node999, 4);", lines[2].Code);
			Assert.Equal("   set_global_var(1, 2);", lines[3].Code);
		}

		[Fact]
		public void DiagramParse_CanShowCommentedCode()
		{
			IReadOnlyList<DialogueLine> lines = new DialogueNodeParser(new DialogueFunctionRules())
				.ParseNodeForDiagram("   // note\r\n   Reply(1);", DialogueSamples.CreateInfo(), false);

			Assert.Equal(new[] { DialogueOpcode.None, DialogueOpcode.Reply }, lines.Select(l => l.Opcode).ToArray());
			Assert.Equal("   // note", lines[0].Code);
		}

		[Fact]
		public void DiagramParse_ElseAfterCommentIsNotMoved()
		{
			IReadOnlyList<DialogueLine> lines = new DialogueNodeParser(new DialogueFunctionRules())
				.ParseNodeForDiagram("   Reply(1); // else Reply(2);", DialogueSamples.CreateInfo(), true);

			Assert.Single(lines);
			Assert.Equal(1, lines[0].MessageNumber);
		}

		[Fact]
		public void DiagramParse_SemicolonOnlyLine_DoesNotHang()
		{
			IReadOnlyList<DialogueLine> lines = new DialogueNodeParser(new DialogueFunctionRules())
				.ParseNodeForDiagram(";;;\r\n   else", DialogueSamples.CreateInfo(), true);

			Assert.Equal(2, lines.Count);
		}

		[Fact]
		public void GetNodeProcedureNames_FindsNodesAndTheStartProcedure()
		{
			ProgramInfo info = DialogueSamples.CreateInfo();
			var parser = new DialogueNodeParser(new DialogueFunctionRules());

			IReadOnlyList<String> names = parser.GetNodeProcedureNames(info.Procedures);

			Assert.Equal(new[] { "talk_p_proc", "Node001", "Node002", "Node999", "Node777" }, names.ToArray());
			Assert.True(parser.IsNodeProcedureName("SomeNodeX"));
			Assert.True(parser.IsNodeProcedureName("Talk_P_Proc"));
			Assert.False(parser.IsNodeProcedureName("start"));
		}

		[Fact]
		public void RulesSource_ChangesTakeEffectWithoutRecreatingTheParser()
		{
			var source = new MutableRulesSource();
			var parser = new DialogueNodeParser(source);
			ProgramInfo info = DialogueSamples.CreateInfo();

			Assert.Empty(parser.ParseNode("   Say(100);", info, new DialogueNodeParseOptions()));
			source.Current = new DialogueFunctionRules(new[] { DialogueFunctionRule.FromOneBased(DialogueOpcode.Reply, "Say", messageArg: 1, totalArgs: 1) });

			Assert.Single(parser.ParseNode("   Say(100);", info, new DialogueNodeParseOptions()));
		}

		[Fact]
		public void WholeSampleNodes_ParseToTheExpectedStatements()
		{
			ProgramInfo info = DialogueSamples.CreateInfo();
			var service = new ProcedureTextService();
			var parser = new DialogueNodeParser(new DialogueFunctionRules());

			IReadOnlyList<DialogueLine> talk = parser.ParseNode(service.GetProcedureCode(DialogueSamples.Script(), "talk_p_proc")!, info, new DialogueNodeParseOptions());
			IReadOnlyList<DialogueLine> node1 = parser.ParseNode(service.GetProcedureCode(DialogueSamples.Script(), "Node001")!, info, new DialogueNodeParseOptions());

			Assert.Equal(
				new[] { "[Reply]", "Node001", "Node999", "Node002", "Node999" },
				talk.Select(l => l.ToNode!).ToArray());
			Assert.Equal(
				new[] { DialogueOpcode.Message, DialogueOpcode.Reply, DialogueOpcode.Reply, DialogueOpcode.Message, DialogueOpcode.GsayOption, DialogueOpcode.GiqOption, DialogueOpcode.Call }.OrderBy(o => o).ToArray(),
				node1.Select(l => l.Opcode).OrderBy(o => o).ToArray());
		}

		private sealed class MutableRulesSource : IDialogueRulesSource
		{
			public DialogueFunctionRules Current { get; set; } = new DialogueFunctionRules();
		}
	}
}