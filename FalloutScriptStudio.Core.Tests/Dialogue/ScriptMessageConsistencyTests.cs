namespace FalloutScriptStudio.Core.Tests.Dialogue
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using FalloutScriptStudio.Core.Dialogue;
	using FalloutScriptStudio.Core.Messages;
	using FalloutScriptStudio.TestSupport;
	using Xunit;

	public class ScriptMessageConsistencyTests
	{
		private static readonly String _dialogue = ScriptMessageSamples.Script(
			"procedure talk_p_proc begin",        // 7
			"   Reply(100);",                     // 8
			"   NOption(101, Node001, 4);",       // 9
			"   NLowOption(102, Node999);",       // 10
			"end",                                // 11
			"procedure Node001 begin",            // 12
			"   NMessage(110);",                  // 13
			"end",                                // 14
			"procedure Node999 begin",
			"end");

		private static MessageDocument WellFormed()
		{
			return ScriptMessageSamples.Messages(
				"# NPC",
				"{100}{}{Hello.}",
				"\t{101}{}{More.}",
				"\t{102}{}{Bye.}",
				"{110}{}{A message.}");
		}

		private static IEnumerable<ScriptMessageFinding> Of(ScriptMessageReport report, ScriptMessageFindingKind kind)
		{
			return report.Of(kind);
		}

		[Fact]
		public void A_script_and_a_message_file_that_match_have_no_finding()
		{
			ScriptMessageReport report = ScriptMessageSamples.Check(_dialogue, WellFormed());

			Assert.Empty(report.Findings);
			Assert.Empty(report.MissingNumbers);
			Assert.Equal(new[] { 100, 101, 102, 110 }, report.UsedNumbers);
		}

		[Fact]
		public void A_number_the_file_lacks_is_an_error_at_its_place_in_the_script()
		{
			MessageDocument messages = ScriptMessageSamples.Messages("{100}{}{Hello.}", "{101}{}{More.}", "{110}{}{A message.}");

			ScriptMessageReport report = ScriptMessageSamples.Check(_dialogue, messages);

			ScriptMessageFinding missing = Assert.Single(report.Findings, f => f.Kind == ScriptMessageFindingKind.MissingMessage);
			Assert.Equal(ScriptMessageSeverity.Error, missing.Severity);
			Assert.Equal(102, missing.Number);
			Assert.True(missing.InScript);
			Assert.Equal(10, missing.Line);
			Assert.Equal(new[] { 102 }, report.MissingNumbers);
		}

		[Fact]
		public void Every_use_of_a_missing_number_is_reported_but_the_number_is_listed_once()
		{
			String script = ScriptMessageSamples.Script("procedure Node001 begin", "   Reply(500);", "   NMessage(500);", "   Reply(499);", "end");

			ScriptMessageReport report = ScriptMessageSamples.Check(script, ScriptMessageSamples.Messages("{1}{}{x}"));

			Assert.Equal(new[] { 8, 9, 10 }, Of(report, ScriptMessageFindingKind.MissingMessage).Select(f => f.Line));
			Assert.Equal(new[] { 499, 500 }, report.MissingNumbers);
		}

		[Fact]
		public void A_number_the_script_never_uses_is_information_at_its_line_in_the_file()
		{
			MessageDocument messages = ScriptMessageSamples.Messages(
				"# NPC",                  // 1
				"{100}{}{Hello.}",        // 2
				"\t{101}{}{More.}",       // 3
				"\t{102}{}{Bye.}",        // 4
				"{110}{}{A message.}",    // 5
				"",                       // 6
				"{777}{}{Nobody says me.}");

			ScriptMessageReport report = ScriptMessageSamples.Check(_dialogue, messages);

			ScriptMessageFinding unused = Assert.Single(report.Findings);
			Assert.Equal(ScriptMessageFindingKind.UnusedMessage, unused.Kind);
			Assert.Equal(ScriptMessageSeverity.Info, unused.Severity);
			Assert.Equal(777, unused.Number);
			Assert.False(unused.InScript);
			Assert.Equal(7, unused.Line);
		}

		[Fact]
		public void Comment_lines_and_unrecognized_lines_are_not_messages()
		{
			MessageDocument messages = ScriptMessageSamples.Messages("# {999}{}{a comment}", "garbage {888", "{100}{}{Hello.}", "{101}{}{More.}", "{102}{}{Bye.}", "{110}{}{A message.}");

			Assert.Empty(ScriptMessageSamples.Check(_dialogue, messages).Findings);
		}

		[Fact]
		public void The_lines_count_the_physical_lines_of_multi_line_messages()
		{
			MessageDocument messages = ScriptMessageSamples.Messages(
				"{100}{}{Hello,",       // 1
				"second line,",         // 2
				"third line.}",         // 3
				"{101}{}{More.}",       // 4
				"{102}{}{Bye.}",        // 5
				"{110}{}{A message.}",  // 6
				"{777}{}{Unused.}");    // 7

			ScriptMessageFinding unused = Assert.Single(ScriptMessageSamples.Check(_dialogue, messages).Findings);

			Assert.Equal(7, unused.Line);
		}

		[Fact]
		public void The_second_line_of_a_duplicated_number_is_an_error()
		{
			MessageDocument messages = ScriptMessageSamples.Messages("{100}{}{Hello.}", "{101}{}{More.}", "{102}{}{Bye.}", "{110}{}{A message.}", "{101}{}{Again.}");

			ScriptMessageReport report = ScriptMessageSamples.Check(_dialogue, messages);

			ScriptMessageFinding duplicate = Assert.Single(report.Findings);
			Assert.Equal(ScriptMessageFindingKind.DuplicateMessage, duplicate.Kind);
			Assert.Equal(ScriptMessageSeverity.Error, duplicate.Severity);
			Assert.Equal(101, duplicate.Number);
			Assert.Equal(5, duplicate.Line);
			Assert.False(duplicate.InScript);
		}

		[Fact]
		public void A_used_message_with_an_empty_text_is_a_warning_in_the_file()
		{
			MessageDocument messages = ScriptMessageSamples.Messages("{100}{}{Hello.}", "{101}{}{}", "{102}{}{   }", "{110}{}{A message.}");

			ScriptMessageReport report = ScriptMessageSamples.Check(_dialogue, messages);

			Assert.Equal(new[] { 101, 102 }, Of(report, ScriptMessageFindingKind.EmptyMessage).Select(f => f.Number));
			Assert.All(Of(report, ScriptMessageFindingKind.EmptyMessage), f => Assert.Equal(ScriptMessageSeverity.Warning, f.Severity));
			Assert.Equal(new[] { 2, 3 }, Of(report, ScriptMessageFindingKind.EmptyMessage).Select(f => f.Line));
		}

		[Fact]
		public void An_empty_message_nobody_uses_is_only_unused()
		{
			MessageDocument messages = ScriptMessageSamples.Messages("{100}{}{Hello.}", "{101}{}{More.}", "{102}{}{Bye.}", "{110}{}{A message.}", "{600}{}{}");

			ScriptMessageReport report = ScriptMessageSamples.Check(_dialogue, messages);

			ScriptMessageFinding finding = Assert.Single(report.Findings);
			Assert.Equal(ScriptMessageFindingKind.UnusedMessage, finding.Kind);
		}

		[Fact]
		public void Without_a_message_file_one_warning_says_so()
		{
			ScriptMessageReport report = ScriptMessageSamples.Check(_dialogue, null);

			ScriptMessageFinding finding = Assert.Single(report.Findings);
			Assert.Equal(ScriptMessageFindingKind.MessageFileMissing, finding.Kind);
			Assert.Equal(ScriptMessageSeverity.Warning, finding.Severity);
			Assert.Equal(4, finding.Count);
			Assert.Equal(-1, finding.Line);
			Assert.Equal(new[] { 100, 101, 102, 110 }, report.MissingNumbers);
		}

		[Fact]
		public void A_script_that_uses_no_message_needs_no_message_file()
		{
			ScriptMessageReport report = ScriptMessageSamples.Check(ScriptMessageSamples.Script("procedure start begin", "end"), null);

			Assert.Empty(report.Findings);
		}

		[Fact]
		public void A_number_that_cannot_be_worked_out_stops_the_unused_check_but_not_the_missing_check()
		{
			String script = ScriptMessageSamples.Script("procedure Node001 begin", "   Reply(100);", "   Reply(which);", "   Reply(321);", "end");
			MessageDocument messages = ScriptMessageSamples.Messages("{100}{}{Hello.}", "{555}{}{Used by the variable maybe.}");

			ScriptMessageReport report = ScriptMessageSamples.Check(script, messages);

			Assert.Equal(new[] { ScriptMessageFindingKind.MissingMessage, ScriptMessageFindingKind.UnresolvedUsages }, report.Findings.Select(f => f.Kind));
			Assert.Equal(1, Of(report, ScriptMessageFindingKind.UnresolvedUsages).Single().Count);
			Assert.Empty(Of(report, ScriptMessageFindingKind.UnusedMessage));
		}

		[Fact]
		public void A_script_without_any_message_use_does_not_list_every_line_as_unused()
		{
			ScriptMessageReport report = ScriptMessageSamples.Check(ScriptMessageSamples.Script("procedure start begin", "end"), WellFormed());

			ScriptMessageFinding finding = Assert.Single(report.Findings);
			Assert.Equal(ScriptMessageFindingKind.NoUsageFound, finding.Kind);
			Assert.Equal(4, finding.Count);
		}

		[Fact]
		public void The_female_variant_of_a_used_message_counts_as_used()
		{
			MessageDocument messages = ScriptMessageSamples.Messages("{100}{}{Hello.}", "{101}{}{More.}", "{102}{}{Bye.}", "{110}{}{A message.}", "{1100}{}{Hello, lady.}", "{1110}{}{A lady message.}", "{1101}{}{More, lady.}", "{1102}{}{Bye, lady.}");

			Assert.Empty(ScriptMessageSamples.Check(_dialogue, messages).Findings);
		}

		[Fact]
		public void A_used_dialogue_message_without_a_female_variant_is_information_when_the_file_has_variants()
		{
			MessageDocument messages = ScriptMessageSamples.Messages("{100}{}{Hello.}", "{101}{}{More.}", "{102}{}{Bye.}", "{110}{}{A message.}", "{1100}{}{Hello, lady.}");

			ScriptMessageReport report = ScriptMessageSamples.Check(_dialogue, messages);

			Assert.Equal(new[] { 101, 102, 110 }, Of(report, ScriptMessageFindingKind.MissingFemaleVariant).Select(f => f.Number));
			Assert.All(Of(report, ScriptMessageFindingKind.MissingFemaleVariant), f => Assert.Equal(ScriptMessageSeverity.Info, f.Severity));
			Assert.Equal(new[] { 2, 3, 4 }, Of(report, ScriptMessageFindingKind.MissingFemaleVariant).Select(f => f.Line));
		}

		[Fact]
		public void The_female_variants_are_only_checked_when_the_script_or_the_file_uses_them()
		{
			Assert.Empty(ScriptMessageSamples.Check(_dialogue, WellFormed()).Findings);

			String gendered = ScriptMessageSamples.Script("procedure talk_p_proc begin", "   if (dude_stat(STAT_gender) == GENDER_FEMALE) then begin", "      Reply(100);", "   end", "end");
			ScriptMessageReport report = ScriptMessageSamples.Check(gendered, ScriptMessageSamples.Messages("{100}{}{Hello.}"));

			Assert.Equal(100, Assert.Single(report.Findings).Number);
			Assert.Equal(ScriptMessageFindingKind.MissingFemaleVariant, report.Findings[0].Kind);
		}

		[Fact]
		public void Messages_that_are_not_dialogue_have_no_female_variant()
		{
			String script = ScriptMessageSamples.Script("procedure start begin", "   display_msg(mstr(300));", "end");
			MessageDocument messages = ScriptMessageSamples.Messages("{300}{}{Hello.}", "{1}{}{Z}", "{1001}{}{Z}");

			ScriptMessageReport report = ScriptMessageSamples.Check(script, messages);

			Assert.Empty(Of(report, ScriptMessageFindingKind.MissingFemaleVariant));
		}

		[Fact]
		public void The_female_check_can_be_turned_off_and_the_offset_changed()
		{
			MessageDocument messages = ScriptMessageSamples.Messages("{100}{}{Hello.}", "{101}{}{More.}", "{102}{}{Bye.}", "{110}{}{A message.}", "{1100}{}{Hello, lady.}", "{5100}{}{Other offset.}");

			Assert.Empty(Of(ScriptMessageSamples.Check(_dialogue, messages, null, null, new ScriptMessageOptions { CheckFemaleVariants = false }), ScriptMessageFindingKind.MissingFemaleVariant));
			ScriptMessageReport custom = ScriptMessageSamples.Check(_dialogue, messages, null, null, new ScriptMessageOptions { FemaleOffset = 5000 });
			Assert.Equal(new[] { 101, 102, 110 }, Of(custom, ScriptMessageFindingKind.MissingFemaleVariant).Select(f => f.Number));
		}

		[Fact]
		public void Messages_built_by_macros_and_sums_are_resolved()
		{
			String script = ScriptMessageSamples.Script(
				"procedure Node001 begin",
				"   Reply(MSG_BASE + 5);",
				"   NMessage(MSG(6));",
				"   display_msg(mstr(MSG_BASE + 7));",
				"   floater(MSG(8));",
				"end");
			MessageDocument messages = ScriptMessageSamples.Messages("{205}{}{a}", "{206}{}{b}", "{207}{}{c}", "{208}{}{d}");

			ScriptMessageReport report = ScriptMessageSamples.Check(script, messages);

			Assert.Empty(report.Findings);
			Assert.Equal(new[] { 205, 206, 207, 208 }, report.UsedNumbers);
		}

		[Fact]
		public void A_random_range_needs_every_message_of_the_range()
		{
			String script = ScriptMessageSamples.Script("procedure Node001 begin", "   Reply(random(300, 302));", "end");

			ScriptMessageReport report = ScriptMessageSamples.Check(script, ScriptMessageSamples.Messages("{300}{}{a}", "{302}{}{c}"));

			Assert.Equal(301, Assert.Single(report.Findings).Number);
		}

		[Fact]
		public void The_script_number_decides_which_message_file_a_number_belongs_to()
		{
			String script = ScriptMessageSamples.Script(
				"procedure start begin",
				"   display_msg(message_str(SCRIPT_NPC, 1));",   // 8, the script itself (123)
				"   display_msg(message_str(124, 2));",          // 9, the file 124 of scripts.lst
				"   display_msg(message_str(125, 3));",          // 10, the file 125, not given
				"end");
			MessageDocument own = ScriptMessageSamples.Messages("{1}{}{own}");
			Dictionary<Int32, MessageDocument> others = new Dictionary<Int32, MessageDocument> { [124] = ScriptMessageSamples.Messages("{1}{}{other}") };

			ScriptMessageReport report = ScriptMessageSamples.Check(script, own, null, others);

			ScriptMessageFinding finding = Assert.Single(report.Findings);
			Assert.Equal(ScriptMessageFindingKind.MissingMessage, finding.Kind);
			Assert.Equal(124, finding.FileNumber);
			Assert.Equal(2, finding.Number);
			Assert.Equal(9, finding.Line);
			Assert.Empty(report.MissingNumbers);
		}

		[Fact]
		public void Numbers_in_comments_and_strings_are_not_uses()
		{
			String script = ScriptMessageSamples.Script("procedure Node001 begin", "   // Reply(999);", "   display_msg(\"NMessage(998)\");", "   Reply(100);", "end");

			ScriptMessageReport report = ScriptMessageSamples.Check(script, ScriptMessageSamples.Messages("{100}{}{Hello.}"));

			Assert.Empty(report.Findings);
		}

		[Fact]
		public void The_order_is_script_order_first_then_file_order()
		{
			String script = ScriptMessageSamples.Script("procedure Node001 begin", "   Reply(300);", "   Reply(200);", "end");
			MessageDocument messages = ScriptMessageSamples.Messages("{900}{}{u}", "{200}{}{}", "{901}{}{u}", "{200}{}{dup}");

			ScriptMessageReport report = ScriptMessageSamples.Check(script, messages);

			Assert.Equal(
				new[] { ScriptMessageFindingKind.MissingMessage, ScriptMessageFindingKind.DuplicateMessage, ScriptMessageFindingKind.EmptyMessage, ScriptMessageFindingKind.UnusedMessage, ScriptMessageFindingKind.UnusedMessage },
				report.Findings.Select(f => f.Kind));
		}

		[Fact]
		public void The_pair_the_performance_tests_generate_is_consistent()
		{
			String script = PerformanceInputs.BuildDialogueScript(60);
			MessageDocument messages = MessageDocument.Parse(PerformanceInputs.BuildDialogueMessages(60).Split("\r\n"));

			ScriptMessageReport report = ScriptMessageConsistency.Check(script, ScriptMessageSamples.Info(script), new DialogueFunctionRules(), messages);

			Assert.Empty(report.Findings);
			Assert.Equal(4 + (60 * 6), report.UsedNumbers.Count);
		}

		[Fact]
		public void The_generated_pair_reports_exactly_the_message_that_was_removed()
		{
			String script = PerformanceInputs.BuildDialogueScript(20);
			String text = PerformanceInputs.BuildDialogueMessages(20);
			String[] lines = text.Split("\r\n").Where(line => !line.StartsWith("{1105}", StringComparison.Ordinal) && !line.StartsWith("\t{1052}", StringComparison.Ordinal)).ToArray();

			ScriptMessageReport report = ScriptMessageConsistency.Check(script, ScriptMessageSamples.Info(script), new DialogueFunctionRules(), MessageDocument.Parse(lines));

			Assert.Equal(new[] { 1052, 1105 }, report.MissingNumbers);
			Assert.Equal(2, report.Findings.Count);
		}
	}
}