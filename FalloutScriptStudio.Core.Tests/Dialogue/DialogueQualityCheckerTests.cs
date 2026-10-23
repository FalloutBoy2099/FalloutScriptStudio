namespace FalloutScriptStudio.Core.Tests.Dialogue
{
	using System;
	using System.Linq;
	using FalloutScriptStudio.Core.Dialogue;
	using FalloutScriptStudio.Core.Messages;
	using Xunit;
	using static DialogueQualitySamples;

	public class DialogueQualityCheckerTests
	{
		[Fact]
		public void A_well_formed_dialogue_has_no_finding()
		{
			String script = Script(WellFormed());

			DialogueQualityReport report = Check(script);

			Assert.True(report.HasDialogue);
			Assert.Equal(4, report.NodeCount);
			Assert.Equal("talk_p_proc", report.EntryNode);
			Assert.Empty(report.Findings);
		}

		[Fact]
		public void A_script_without_dialogue_statements_has_no_finding()
		{
			String script = Script("procedure get_node_count begin", "   display_msg(\"nodes\");", "end", "procedure talk_p_proc begin", "end");

			DialogueQualityReport report = Check(script);

			Assert.False(report.HasDialogue);
			Assert.Empty(report.Findings);
		}

		[Fact]
		public void A_node_that_nothing_leads_to_is_unreachable_at_the_line_of_its_header()
		{
			String script = Script(WithNodes("procedure Node777 begin", "   Reply(130);", "   NOption(131, Node999, 4);", "end"));

			DialogueQualityFinding finding = Assert.Single(Check(script).Findings);

			Assert.Equal(DialogueQualityCode.UnreachableNode, finding.Code);
			Assert.Equal(DialogueQualitySeverity.Warning, finding.Severity);
			Assert.Equal("Node777", finding.Node);
			Assert.Equal(LineOf(script, "procedure Node777"), finding.Line);
			Assert.Equal("DLG001", finding.Id);
		}

		[Fact]
		public void An_unused_empty_exit_node_is_not_reported_and_the_entry_node_is_never_unreachable()
		{
			String script = Script(WellFormed().Where(line => !line.Contains("Node999", StringComparison.Ordinal) || line.StartsWith("procedure", StringComparison.Ordinal)).ToArray());

			DialogueQualityReport report = Check(script);

			Assert.DoesNotContain(report.Findings, finding => finding.Code == DialogueQualityCode.UnreachableNode);
		}

		[Fact]
		public void A_node_reached_through_a_node_that_is_reachable_is_reachable_and_a_chain_of_unreachable_nodes_is_all_reported()
		{
			String script = Script(WithNodes(
				"procedure Node700 begin", "   Reply(700);", "   NOption(701, Node701, 4);", "end",
				"procedure Node701 begin", "   Reply(710);", "   NOption(711, Node999, 4);", "end"));

			String[] unreachable = Check(script).Of(DialogueQualityCode.UnreachableNode).Select(finding => finding.Node).ToArray();

			Assert.Equal(new[] { "Node700", "Node701" }, unreachable);
		}

		[Fact]
		public void A_call_statement_leads_to_a_node()
		{
			String script = Script(WithNodes("procedure Node050 begin", "   Reply(150);", "   NOption(151, Node999, 4);", "end")
				.Select(line => line == "   NMessage(120);" ? "   NMessage(120);\r\n   call Node050;" : line).ToArray());

			DialogueQualityReport report = Check(script);

			Assert.Empty(report.Findings);
		}

		[Fact]
		public void A_name_that_code_outside_the_nodes_uses_makes_the_node_an_entry()
		{
			String script = Script(WithNodes("procedure Node050 begin", "   Reply(150);", "   NOption(151, Node999, 4);", "end", "procedure use_p_proc begin", "   call Node050;", "end"));

			Assert.Empty(Check(script).Findings);
		}

		[Fact]
		public void A_name_a_node_mentions_in_a_statement_the_parser_does_not_know_counts_as_an_edge()
		{
			String script = Script(WithNodes("procedure Node050 begin", "   Reply(150);", "   NOption(151, Node999, 4);", "end"))
				.Replace("   NMessage(120);", "   NMessage(120);\r\n   MyJump(Node050);");

			Assert.Empty(Check(script).Findings);
		}

		[Fact]
		public void The_names_of_the_nodes_are_compared_without_the_case()
		{
			String script = Script(WellFormed()).Replace("NOption(101, Node001, 4)", "NOption(101, node001, 4)");

			Assert.Empty(Check(script).Findings);
		}

		[Fact]
		public void A_reply_without_option_call_or_exit_is_a_dead_end_at_the_line_of_the_reply()
		{
			String script = Script(WithNodes("procedure Node003 begin", "   Reply(130);", "end")).Replace("NOption(111, Node002, 4);", "NOption(111, Node002, 4);\r\n   NOption(114, Node003, 4);");

			DialogueQualityFinding finding = Assert.Single(Check(script).Findings);

			Assert.Equal(DialogueQualityCode.DeadEnd, finding.Code);
			Assert.Equal(DialogueQualitySeverity.Warning, finding.Severity);
			Assert.Equal("Node003", finding.Node);
			Assert.Equal(LineOf(script, "Reply(130)"), finding.Line);
		}

		[Theory]
		[InlineData("   NMessage(131);")]
		[InlineData("   end_dialogue;")]
		[InlineData("   gsay_end;")]
		[InlineData("   gdialog_barter;")]
		[InlineData("   attack_complex(self_obj, 0, 1, 0, 0, 30000, 0, 0);")]
		[InlineData("   NOption(131, Node999, 4);")]
		[InlineData("   NOption(131, finish_dialogue, 4);")]
		public void A_reply_followed_by_an_exit_is_not_a_dead_end(String exit)
		{
			String script = Script(WithNodes("procedure Node003 begin", "   Reply(130);", exit, "end", "procedure finish_dialogue begin", "end"))
				.Replace("NOption(111, Node002, 4);", "NOption(111, Node002, 4);\r\n   NOption(114, Node003, 4);");

			Assert.Empty(Check(script).Findings);
		}

		[Theory]
		[InlineData("procedure Node998 begin", "   set_local_var(0, 2);", "end")]
		[InlineData("procedure Node1000 begin", "   gdialog_mod_barter(0);", "end")]
		[InlineData("procedure Node004 begin", "   display_msg(\"code only\");", "end")]
		public void A_node_without_any_dialogue_statement_ends_the_dialogue(String header, String code, String end)
		{
			String script = Script(WithNodes(header, code, end)).Replace("NLowOption(113, Node999);", "NLowOption(113, Node999);\r\n   NOption(114, " + header.Split(' ')[1] + ", 4);");

			Assert.Empty(Check(script).Findings);
		}

		[Fact]
		public void An_option_that_names_an_unknown_procedure_is_an_error()
		{
			String script = Script(WellFormed()).Replace("NOption(101, Node001, 4);", "NOption(101, Node001, 4);\r\n   NOption(103, Node404, 4);");

			DialogueQualityFinding finding = Assert.Single(Check(script).Findings);

			Assert.Equal(DialogueQualityCode.UndefinedTarget, finding.Code);
			Assert.Equal(DialogueQualitySeverity.Error, finding.Severity);
			Assert.Equal("talk_p_proc", finding.Node);
			Assert.Equal("Node404", finding.RelatedNode);
			Assert.Equal(LineOf(script, "Node404"), finding.Line);
		}

		[Fact]
		public void An_unknown_target_is_not_reported_when_the_option_is_turned_off_and_is_never_a_dead_end()
		{
			String script = Script(WellFormed()).Replace("NOption(101, Node001, 4);", "NOption(101, Node001, 4);\r\n   NOption(103, Node404, 4);");

			DialogueQualityReport report = Check(script, new DialogueQualityOptions { ReportUndefinedTargets = false });

			Assert.Empty(report.Findings);
		}

		[Fact]
		public void A_macro_that_stands_for_a_node_is_followed_and_a_forward_declaration_or_a_variable_is_not_undefined()
		{
			String script = Script(WellFormed())
				.Replace("#define NAME SCRIPT_NPC", "#define NAME SCRIPT_NPC\r\n#define NodeEnd Node999\r\n#define NodeAlias (NodeEnd)\r\nprocedure Node500;\r\nvariable jump;")
				.Replace("NLowOption(102, Node999);", "NLowOption(102, NodeAlias);\r\n   NLowOption(103, Node500);\r\n   NLowOption(104, jump);\r\n   NLowOption(105, SomeMacro(1));")
				+ "\r\nprocedure Node500 begin\r\n   Reply(500);\r\n   NOption(501, NodeEnd, 4);\r\nend";

			DialogueQualityReport report = Check(script);

			Assert.DoesNotContain(report.Findings, finding => finding.Code is DialogueQualityCode.UndefinedTarget or DialogueQualityCode.UnreachableNode);
		}

		[Fact]
		public void Nodes_that_only_lead_to_each_other_are_an_exitless_cycle()
		{
			String script = Script(WithNodes(
				"procedure NodeA begin", "   Reply(200);", "   NOption(201, NodeB, 4);", "end",
				"procedure NodeB begin", "   Reply(210);", "   NOption(211, NodeA, 4);", "end")).Replace("NOption(111, Node002, 4);", "NOption(111, Node002, 4);\r\n   NOption(114, NodeA, 4);");

			DialogueQualityFinding finding = Assert.Single(Check(script).Findings);

			Assert.Equal(DialogueQualityCode.ExitlessCycle, finding.Code);
			Assert.Equal(DialogueQualitySeverity.Warning, finding.Severity);
			Assert.Equal("NodeA", finding.Node);
			Assert.Equal("NodeB", finding.RelatedNode);
			Assert.Equal(2, finding.Count);
			Assert.Equal(new[] { "NodeA", "NodeB" }, finding.AffectedNodes);
			Assert.True(finding.Affects("nodeb"));
		}

		[Fact]
		public void A_node_that_calls_itself_with_no_other_way_out_is_an_exitless_cycle()
		{
			String script = Script(WithNodes("procedure NodeA begin", "   Reply(200);", "   NOption(201, NodeA, 4);", "end")).Replace("NOption(111, Node002, 4);", "NOption(111, Node002, 4);\r\n   NOption(114, NodeA, 4);");

			DialogueQualityFinding finding = Assert.Single(Check(script).Findings);

			Assert.Equal(DialogueQualityCode.ExitlessCycle, finding.Code);
			Assert.Equal("NodeA", finding.Node);
			Assert.Equal(1, finding.Count);
		}

		[Theory]
		[InlineData("   NOption(212, Node999, 4);")]
		[InlineData("   NOption(212, Node001, 4);")]
		[InlineData("   end_dialogue;")]
		public void A_cycle_with_a_way_out_is_fine(String way)
		{
			String script = Script(WithNodes(
				"procedure NodeA begin", "   Reply(200);", "   NOption(201, NodeB, 4);", "end",
				"procedure NodeB begin", "   Reply(210);", "   NOption(211, NodeA, 4);", way, "end")).Replace("NOption(111, Node002, 4);", "NOption(111, Node002, 4);\r\n   NOption(114, NodeA, 4);");

			Assert.Empty(Check(script).Findings);
		}

		[Fact]
		public void A_main_menu_that_other_nodes_return_to_is_not_a_cycle_without_exit()
		{
			String script = Script(
				"procedure talk_p_proc begin",
				"   Reply(100);",
				"   NOption(101, Node001, 4);",
				"   NLowOption(102, Node999);",
				"end",
				"procedure Node001 begin",
				"   Reply(110);",
				"   NOption(111, Node002, 4);",
				"   NOption(112, Node003, 4);",
				"   NOption(113, Node999, 4);",
				"end",
				"procedure Node002 begin",
				"   Reply(120);",
				"   NOption(121, Node001, 4);",
				"end",
				"procedure Node003 begin",
				"   Reply(130);",
				"   NOption(131, Node001, 4);",
				"end",
				"procedure Node999 begin",
				"end");

			Assert.Empty(Check(script).Findings);
		}

		[Fact]
		public void Options_without_a_reply_are_noted_and_a_node_that_only_calls_another_is_not()
		{
			String script = Script(WithNodes("procedure Node040 begin", "   NOption(141, Node999, 4);", "end", "procedure Node041 begin", "   call Node002;", "end"))
				.Replace("NOption(111, Node002, 4);", "NOption(111, Node002, 4);\r\n   NOption(114, Node040, 4);\r\n   NOption(115, Node041, 4);");

			DialogueQualityFinding finding = Assert.Single(Check(script).Findings);

			Assert.Equal(DialogueQualityCode.NodeWithoutText, finding.Code);
			Assert.Equal(DialogueQualitySeverity.Info, finding.Severity);
			Assert.Equal("Node040", finding.Node);
			Assert.Equal(1, finding.Count);
		}

		[Fact]
		public void Two_options_to_the_same_node_are_noted_at_the_second_one()
		{
			String script = Script(WellFormed()).Replace("NOption(101, Node001, 4);", "NOption(101, Node001, 4);\r\n   NOption(103, Node001, 4);");

			DialogueQualityFinding finding = Assert.Single(Check(script).Findings);

			Assert.Equal(DialogueQualityCode.DuplicateTarget, finding.Code);
			Assert.Equal(DialogueQualitySeverity.Info, finding.Severity);
			Assert.Equal("talk_p_proc", finding.Node);
			Assert.Equal("Node001", finding.RelatedNode);
			Assert.Equal(2, finding.Count);
			Assert.Equal(LineOf(script, "NOption(103"), finding.Line);
		}

		[Fact]
		public void Options_to_an_exit_node_a_low_intelligence_twin_and_alternatives_are_not_duplicates()
		{
			String script = Script(WellFormed())
				.Replace("NOption(111, Node002, 4);", "NOption(111, Node002, 4);\r\n   NLowOption(114, Node002);\r\n   if (global_var(1) == 1) then begin\r\n      NOption(115, Node001, 4);\r\n   end else begin\r\n      NOption(116, Node001, 4);\r\n   end");

			Assert.Empty(Check(script).Findings);
		}

		[Fact]
		public void More_options_than_the_limit_are_noted_and_the_limit_is_an_option()
		{
			String[] options = Enumerable.Range(0, 9).Select(i => "   NOption(" + (300 + i) + ", Node999, 4);").ToArray();
			String script = Script(WithNodes(new[] { "procedure Node030 begin", "   Reply(299);" }.Concat(options).Concat(new[] { "end" }).ToArray()))
				.Replace("NOption(111, Node002, 4);", "NOption(111, Node002, 4);\r\n   NOption(114, Node030, 4);");

			DialogueQualityFinding finding = Assert.Single(Check(script).Findings);
			Assert.Equal(DialogueQualityCode.TooManyOptions, finding.Code);
			Assert.Equal(9, finding.Count);
			Assert.Equal("Node030", finding.Node);

			Assert.Empty(Check(script, new DialogueQualityOptions { MaxOptionsPerNode = 9 }).Findings);
		}

		[Fact]
		public void The_same_message_number_twice_among_the_options_is_a_warning_unless_they_are_alternatives()
		{
			String script = Script(WellFormed()).Replace("NOption(111, Node002, 4);", "NOption(111, Node002, 4);\r\n   NOption(111, Node001, 4);");

			DialogueQualityFinding finding = Assert.Single(Check(script).Findings, f => f.Code == DialogueQualityCode.DuplicateMessageNumber);

			Assert.Equal(DialogueQualitySeverity.Warning, finding.Severity);
			Assert.Equal(new[] { 111 }, finding.Numbers);
			Assert.Equal(LineOf(script, "NOption(111", 2), finding.Line);

			String alternatives = Script(WellFormed()).Replace("NOption(111, Node002, 4);", "if (global_var(1) == 1) then NOption(111, Node002, 4); else NOption(111, Node001, 4);");
			Assert.DoesNotContain(Check(alternatives).Findings, f => f.Code == DialogueQualityCode.DuplicateMessageNumber);
		}

		[Fact]
		public void The_messages_a_node_lacks_are_linked_to_the_node_without_repeating_the_error()
		{
			String script = Script(WellFormed());
			MessageDocument messages = ScriptMessageSamples.Messages("{100}{}{Hello.}", "{101}{}{More.}", "{110}{}{A.}", "{111}{}{B.}", "{112}{}{C.}", "{113}{}{D.}", "{120}{}{E.}");
			ScriptMessageReport messageReport = ScriptMessageSamples.Check(script, messages);
			Assert.Contains(messageReport.Findings, f => f.Kind == ScriptMessageFindingKind.MissingMessage && f.Number == 102);

			DialogueQualityFinding finding = Assert.Single(Check(script, messages: messageReport).Findings);

			Assert.Equal(DialogueQualityCode.MissingMessages, finding.Code);
			Assert.Equal(DialogueQualitySeverity.Info, finding.Severity);
			Assert.Equal("talk_p_proc", finding.Node);
			Assert.Equal(new[] { 102 }, finding.Numbers);
			Assert.Equal(1, finding.Count);
			Assert.Empty(Check(script).Findings);
		}

		[Fact]
		public void The_findings_come_in_script_order_and_a_comment_does_not_count()
		{
			String script = Script(WithNodes("procedure Node777 begin", "   // NOption(131, Node404, 4);", "   Reply(130);", "   NMessage(132);", "end", "procedure Node778 begin", "   /* NOption(131, Node405, 4); */", "   Reply(131);", "   NMessage(133);", "end"))
				.Replace("NOption(111, Node002, 4);", "NOption(111, Node002, 4);\r\n   NOption(114, Node406, 4);");

			DialogueQualityFinding[] findings = Check(script).Findings.ToArray();

			Assert.Equal(new[] { "Node406", null, null }, findings.Select(f => f.RelatedNode).ToArray());
			Assert.Equal(new[] { DialogueQualityCode.UndefinedTarget, DialogueQualityCode.UnreachableNode, DialogueQualityCode.UnreachableNode }, findings.Select(f => f.Code).ToArray());
			Assert.True(findings.Select(f => f.Line).SequenceEqual(findings.Select(f => f.Line).Order()));
		}

		[Fact]
		public void The_report_lists_the_findings_of_a_node()
		{
			String script = Script(WithNodes("procedure Node777 begin", "   Reply(130);", "   NMessage(131);", "end"));

			DialogueQualityReport report = Check(script);

			Assert.Single(report.ForNode("node777"));
			Assert.Empty(report.ForNode("Node001"));
			Assert.Single(report.Of(DialogueQualityCode.UnreachableNode));
		}

		[Fact]
		public void The_conventional_exit_names_are_an_option()
		{
			String script = Script(WithNodes("procedure NodeBye begin", "   Reply(130);", "end")).Replace("NOption(111, Node002, 4);", "NOption(111, Node002, 4);\r\n   NOption(114, NodeBye, 4);");

			Assert.Single(Check(script).Findings, finding => finding.Code == DialogueQualityCode.DeadEnd);
			Assert.Empty(Check(script, new DialogueQualityOptions { ExitNodeNames = new[] { "NodeBye" } }).Findings);
		}

		[Fact]
		public void A_script_with_unix_line_breaks_gives_the_same_findings()
		{
			String script = Script(WithNodes("procedure Node777 begin", "   Reply(130);", "   NMessage(131);", "end"));

			DialogueQualityReport crlf = Check(script);
			DialogueQualityReport lf = Check(script.Replace("\r\n", "\n"));

			Assert.Equal(crlf.Findings.Select(f => (f.Code, f.Node, f.Line)), lf.Findings.Select(f => (f.Code, f.Node, f.Line)));
		}

		[Fact]
		public void A_script_that_stops_in_the_middle_of_a_node_does_not_throw()
		{
			String script = Script(WellFormed()) + "\r\nprocedure Node050 begin\r\n   Reply(5);\r\n   NOption(6, Node0";

			DialogueQualityReport report = Check(script);

			Assert.True(report.HasDialogue);
		}
	}
}