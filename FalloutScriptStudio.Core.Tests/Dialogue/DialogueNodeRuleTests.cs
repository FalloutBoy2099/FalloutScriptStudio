namespace FalloutScriptStudio.Core.Tests.Dialogue
{
	using System;
	using FalloutScriptStudio.Core.Dialogue;
	using Xunit;

	public class DialogueNodeRuleTests
	{
		[Theory]
		[InlineData("Node001", true)]
		[InlineData("node_exit", true)]
		[InlineData("SubNodeHelper", true)]
		[InlineData("talk_p_proc", true)]
		[InlineData("TALK_P_PROC", true)]
		[InlineData("start", false)]
		[InlineData("helper", false)]
		[InlineData("talk_p_proc2", false)]
		[InlineData("", false)]
		public void A_node_is_a_procedure_whose_name_contains_node_or_talk_p_proc(String name, Boolean expected)
		{
			Assert.Equal(expected, DialogueNodeRule.IsNode(name));
		}

		[Fact]
		public void The_parser_and_the_rule_agree()
		{
			DialogueNodeParser parser = new DialogueNodeParser(new DialogueFunctionRules());

			foreach (String name in new[] { "Node001", "talk_p_proc", "helper", "NODE9", "map_enter_p_proc" })
			{
				Assert.Equal(DialogueNodeRule.IsNode(name), parser.IsNodeProcedureName(name));
			}
		}
	}
}