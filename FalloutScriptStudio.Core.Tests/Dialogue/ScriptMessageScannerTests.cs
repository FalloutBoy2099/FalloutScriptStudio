namespace FalloutScriptStudio.Core.Tests.Dialogue
{
	using System;
	using System.Linq;
	using FalloutScriptStudio.Core.Dialogue;
	using Xunit;

	public class ScriptMessageScannerTests
	{
		private static Int32[] Numbers(ScriptMessageScan scan)
		{
			return scan.OwnUsages.Select(usage => usage.Number).ToArray();
		}

		[Fact]
		public void The_calls_of_the_dialogue_rules_give_their_message_numbers_and_places()
		{
			ScriptMessageScan scan = ScriptMessageSamples.Scan(ScriptMessageSamples.Script(
				"procedure talk_p_proc begin",     // 7
				"   Reply(100);",                  // 8
				"   NOption(101, Node001, 4);",    // 9
				"   NLowOption(102, Node999);",    // 10
				"   NMessage(103);",               // 11
				"end"));

			Assert.Equal(new[] { 100, 101, 102, 103 }, Numbers(scan));
			Assert.Equal(new[] { 8, 9, 10, 11 }, scan.Usages.Select(usage => usage.Line));
			ScriptMessageUsage first = scan.Usages[0];
			Assert.Equal(10, first.Column);
			Assert.Equal(3, first.Length);
			Assert.Equal(new[] { ScriptMessageUsageKind.NpcText, ScriptMessageUsageKind.Option, ScriptMessageUsageKind.Option, ScriptMessageUsageKind.NpcText }, scan.Usages.Select(usage => usage.Kind));
			Assert.Equal(0, scan.UnresolvedCount);
		}

		[Fact]
		public void The_engine_dialogue_functions_use_the_file_and_number_arguments()
		{
			ScriptMessageScan scan = ScriptMessageSamples.Scan(ScriptMessageSamples.Script(
				"procedure Node001 begin",
				"   gsay_reply(NAME, 110);",
				"   gsay_message(NAME, 111, 50);",
				"   gsay_option(NAME, 112, Node002, 50);",
				"   giq_option(4, NAME, 113, Node002, 50);",
				"end"));

			Assert.Equal(new[] { 110, 111, 112, 113 }, Numbers(scan));
			Assert.All(scan.Usages, usage => Assert.True(usage.IsOwnFile));
		}

		[Fact]
		public void Message_str_and_the_macros_that_expand_to_it_are_followed()
		{
			ScriptMessageScan scan = ScriptMessageSamples.Scan(ScriptMessageSamples.Script(
				"procedure start begin",
				"   display_msg(mstr(120));",
				"   floater(121);",
				"   display_msg(message_str(NAME, 122));",
				"   display_msg(message_str(SCRIPT_NPC, 123));",
				"end"));

			Assert.Equal(new[] { 120, 121, 122, 123 }, Numbers(scan));
			Assert.All(scan.Usages, usage => Assert.Equal(ScriptMessageUsageKind.Display, usage.Kind));
		}

		[Theory]
		[InlineData("Reply(MSG_BASE + 5);", new[] { 205 })]
		[InlineData("Reply(MSG(7));", new[] { 207 })]
		[InlineData("Reply(MSG_BASE + MSG(1) - 100);", new[] { 301 })]
		[InlineData("Reply(0x6E);", new[] { 110 })]
		[InlineData("Reply((MSG_BASE) * 2);", new[] { 400 })]
		[InlineData("Reply(random(300, 302));", new[] { 300, 301, 302 })]
		[InlineData("NMessage(mstr(MSG_BASE + 9));", new[] { 209 })]
		[InlineData("Reply   (  150  )  ;", new[] { 150 })]
		public void The_number_may_be_a_macro_a_sum_or_a_range(String statement, Int32[] expected)
		{
			ScriptMessageScan scan = ScriptMessageSamples.Scan(ScriptMessageSamples.Script("procedure Node001 begin", "   " + statement, "end"));

			Assert.Equal(expected, Numbers(scan));
			Assert.Equal(0, scan.UnresolvedCount);
		}

		[Fact]
		public void Comments_strings_and_directives_are_not_code()
		{
			ScriptMessageScan scan = ScriptMessageSamples.Scan(ScriptMessageSamples.Script(
				"#define OLD_REPLY Reply(903)",
				"procedure Node001 begin",
				"   // Reply(900);",
				"   /* Reply(901);",
				"      NMessage(902); */",
				"   display_msg(\"Reply(904)\");",
				"   Reply(100); // NMessage(905)",
				"end"));

			Assert.Equal(new[] { 100 }, Numbers(scan));
		}

		[Fact]
		public void A_call_may_span_several_lines()
		{
			ScriptMessageScan scan = ScriptMessageSamples.Scan(ScriptMessageSamples.Script(
				"procedure Node001 begin",   // 7
				"   NOption(",               // 8
				"      104,",                // 9
				"      Node002,",
				"      4);",
				"end"));

			ScriptMessageUsage usage = Assert.Single(scan.Usages);
			Assert.Equal(104, usage.Number);
			Assert.Equal(9, usage.Line);
			Assert.Equal(7, usage.Column);
		}

		[Fact]
		public void The_lines_are_the_same_with_unix_line_breaks()
		{
			String crlf = ScriptMessageSamples.Script("procedure Node001 begin", "   Reply(100);", "end");
			String lf = crlf.Replace("\r\n", "\n", StringComparison.Ordinal);

			Assert.Equal(ScriptMessageSamples.Scan(crlf).Usages, ScriptMessageSamples.Scan(lf).Usages);
		}

		[Fact]
		public void A_number_that_is_not_known_is_counted_as_unresolved_once()
		{
			ScriptMessageScan scan = ScriptMessageSamples.Scan(ScriptMessageSamples.Script(
				"procedure Node001 begin",
				"   Reply(msg_number);",
				"   Reply(mstr(other_number));",
				"   Reply(100);",
				"end"));

			Assert.Equal(new[] { 100 }, Numbers(scan));
			Assert.Equal(2, scan.UnresolvedCount);
		}

		[Fact]
		public void Other_message_files_are_kept_apart_and_game_messages_are_ignored()
		{
			ScriptMessageScan scan = ScriptMessageSamples.Scan(ScriptMessageSamples.Script(
				"procedure start begin",
				"   display_msg(message_str(124, 5));",
				"   display_msg(message_str_game(msg, 1));",
				"   display_msg(message_str(some_variable, 7));",
				"   display_msg(mstr(8));",
				"end"));

			Assert.Equal(new[] { 8 }, Numbers(scan));
			ScriptMessageUsage other = Assert.Single(scan.Usages, usage => !usage.IsOwnFile);
			Assert.Equal(124, other.FileNumber);
			Assert.Equal(5, other.Number);
			Assert.Equal(new[] { 124 }, scan.OtherFileNumbers);
			Assert.Equal(0, scan.UnresolvedCount);
		}

		[Fact]
		public void Procedure_declarations_and_names_of_procedures_are_not_calls()
		{
			ScriptMessageScan scan = ScriptMessageSamples.Scan(ScriptMessageSamples.Script(
				"procedure Reply(variable x);",
				"procedure ShowMessage(variable x);",
				"procedure Reply(variable x) begin",
				"end",
				"procedure ShowMessage(variable x) begin",
				"end",
				"procedure start begin",
				"   call ShowMessage(5);",
				"end"));

			Assert.Empty(scan.Usages);
		}

		[Fact]
		public void Without_user_rules_the_names_that_contain_the_dialogue_keywords_are_read_like_the_legacy_parser_did()
		{
			ScriptMessageScan scan = ScriptMessageSamples.Scan(
				ScriptMessageSamples.Script("procedure Node001 begin", "   NMessage(103);", "   NOption(101, Node002, 4);", "   BigReply(105);", "   float_msg(self_obj, \"text\", 0);", "end"),
				new DialogueFunctionRules());

			Assert.Equal(new[] { 103, 101, 105 }, Numbers(scan));
		}

		[Fact]
		public void The_name_macro_means_the_script_even_when_its_header_is_missing()
		{
			String script = TestScriptsOnly("procedure Node001 begin", "   gsay_reply(NAME, 100);", "end");

			ScriptMessageScan scan = ScriptMessageSamples.Scan(script);

			ScriptMessageUsage usage = Assert.Single(scan.Usages);
			Assert.True(usage.IsOwnFile);
		}

		[Fact]
		public void The_gender_of_the_player_is_noticed()
		{
			Assert.True(ScriptMessageSamples.Scan(ScriptMessageSamples.Script("procedure start begin", "   if (dude_stat(STAT_gender) == GENDER_FEMALE) then begin", "   end", "end")).UsesFemaleVariants);
			Assert.False(ScriptMessageSamples.Scan(ScriptMessageSamples.Script("procedure start begin", "   // female", "end")).UsesFemaleVariants);
		}

		[Fact]
		public void A_name_macro_that_is_another_script_makes_the_messages_foreign()
		{
			String script = String.Join("\r\n", "#define NAME SCRIPT_OTHER", "#define SCRIPT_OTHER 77", "#define SCRIPT_NPC 123", "procedure Node001 begin", "   gsay_reply(SCRIPT_NPC, 100);", "end");

			ScriptMessageScan scan = ScriptMessageScanner.Scan(script, ScriptMessageSamples.Info(script), ScriptMessageSamples.Rules());

			ScriptMessageUsage usage = Assert.Single(scan.Usages);
			Assert.Equal(123, usage.FileNumber);
			Assert.False(usage.IsOwnFile);
		}

		[Fact]
		public void Macros_that_refer_to_each_other_do_not_hang_the_scan()
		{
			String script = String.Join(
				"\r\n",
				"#define LOOP_A LOOP_B",
				"#define LOOP_B LOOP_A",
				"#define ping(x) pong(x)",
				"#define pong(x) ping(x)",
				"procedure Node001 begin",
				"   Reply(LOOP_A);",
				"   ping(5);",
				"   Reply(100);",
				"end");

			ScriptMessageScan scan = ScriptMessageSamples.Scan(script);

			Assert.Equal(new[] { 100 }, Numbers(scan));
			Assert.Equal(1, scan.UnresolvedCount);
		}

		[Theory]
		[InlineData("Reply(99999999999);")]
		[InlineData("Reply(2147483647 + 1);")]
		[InlineData("Reply(5 / 0);")]
		[InlineData("Reply(random(10, 5));")]
		[InlineData("Reply(random(1, 100000));")]
		[InlineData("Reply(100u);")]
		[InlineData("Reply();")]
		[InlineData("Reply(")]
		public void Numbers_that_cannot_be_used_never_throw_and_give_no_usage(String statement)
		{
			ScriptMessageScan scan = ScriptMessageSamples.Scan(ScriptMessageSamples.Script("procedure Node001 begin", "   " + statement, "end"));

			Assert.Empty(scan.Usages);
		}

		[Fact]
		public void Engine_functions_and_calls_with_a_text_are_not_taken_for_message_calls()
		{
			ScriptMessageScan scan = ScriptMessageSamples.Scan(
				ScriptMessageSamples.Script("#define lower_reply(x) show_it(x)", "procedure Node001 begin", "   create_message_window(\"Hello\");", "   create_message_window(text);", "   BigMessage(\"text\");", "   unknown_reply(7);", "   lower_reply(8);", "end"),
				new DialogueFunctionRules());

			Assert.Equal(new[] { 8 }, Numbers(scan));
			Assert.Equal(0, scan.UnresolvedCount);
		}

		private static String TestScriptsOnly(params String[] lines)
		{
			return String.Join("\r\n", lines);
		}
	}
}