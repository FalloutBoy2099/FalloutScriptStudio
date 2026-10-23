namespace FalloutScriptStudio.Core.Tests.Globals
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using FalloutScriptStudio.Core.Globals;
	using Xunit;

	public sealed class GlobalVariableAnalyzerTests
	{
		private static IReadOnlyList<GlobalFinding> Analyze(String script, GlobalAnalysisOptions? options = null, String? header = null)
		{
			List<KeyValuePair<String, String>> files = [new("c:\\project\\a.ssl", "procedure start begin\r\n" + script + "end\r\n")];
			if (header is not null)
			{
				files.Add(new("c:\\project\\h.h", header));
			}

			return GlobalVariableAnalyzer.Analyze(GlobalVariableIndex.FromTexts(files), options);
		}

		private static GlobalFinding[] Of(IReadOnlyList<GlobalFinding> findings, GlobalFindingKind kind)
		{
			return findings.Where(f => f.Kind == kind).ToArray();
		}

		[Fact]
		public void A_variable_that_is_written_and_never_read_is_information()
		{
			IReadOnlyList<GlobalFinding> findings = Analyze("\tset_global_var(GVAR_QUEST, 1);\r\n\tset_global_var(GVAR_QUEST, 2);\r\n");

			GlobalFinding finding = Assert.Single(Of(findings, GlobalFindingKind.WrittenNeverRead));
			Assert.Equal(GlobalFindingSeverity.Info, finding.Severity);
			Assert.Equal("GVAR_QUEST", finding.Variable);
			Assert.Equal(2, finding.Number);
			Assert.Equal(2, finding.Line);
			Assert.Equal("start", finding.Procedure);
		}

		[Fact]
		public void A_variable_that_is_read_and_never_written_is_a_warning()
		{
			IReadOnlyList<GlobalFinding> findings = Analyze("\tvariable v := global_var(GVAR_STATE);\r\n");

			GlobalFinding finding = Assert.Single(Of(findings, GlobalFindingKind.ReadNeverWritten));
			Assert.Equal(GlobalFindingSeverity.Warning, finding.Severity);
			Assert.Equal(2, finding.Line);
		}

		[Fact]
		public void A_variable_that_is_read_and_written_is_fine()
		{
			Assert.Empty(Analyze("\tset_global_var(GVAR_OK, 1);\r\n\tvariable v := global_var(GVAR_OK);\r\n"));
		}

		[Fact]
		public void An_increment_both_reads_and_writes()
		{
			Assert.Empty(Analyze("\tinc_global_var(GVAR_COUNT);\r\n"));
			Assert.Empty(Analyze("\tset_gvar_bit_on(GVAR_BITS, 1);\r\n"));
		}

		[Fact]
		public void A_write_in_one_file_and_a_read_in_another_is_fine()
		{
			IReadOnlyList<GlobalFinding> findings = GlobalVariableAnalyzer.Analyze(GlobalVariableIndex.FromTexts(
			[
				new("c:\\p\\a.ssl", "procedure start begin\r\n\tset_global_var(5, 1);\r\nend\r\n"),
				new("c:\\p\\b.ssl", "procedure start begin\r\n\tvariable v := global_var(5);\r\nend\r\n"),
			]));

			Assert.Empty(findings);
		}

		[Fact]
		public void The_variables_the_engine_sets_are_not_reported_as_never_written()
		{
			Assert.Empty(Analyze("\tvariable v := global_var(GVAR_PLAYER_REPUTATION);\r\n\tv := global_var(27);\r\n\tv := global_var(0);\r\n"));
			Assert.Equal(2, Analyze("\tvariable v := global_var(GVAR_PLAYER_REPUTATION);\r\n\tv := global_var(27);\r\n", new GlobalAnalysisOptions { SkipEngineVariables = false }).Count);
		}

		[Fact]
		public void The_engine_variables_are_found_by_their_macro_too()
		{
			Assert.Empty(Analyze("\tvariable v := global_var(GVAR_LOAD_MAP_INDEX);\r\n", null, "#define GVAR_LOAD_MAP_INDEX (27)\r\n"));
		}

		[Fact]
		public void The_ignore_list_hides_a_variable_by_name_number_or_prefix()
		{
			String script = "\tvariable v := global_var(GVAR_QUEST_ALPHA);\r\n\tv := global_var(GVAR_QUEST_BRAVO);\r\n\tv := global_var(GVAR_OTHER);\r\n\tv := global_var(77);\r\n";

			Assert.Equal(4, Analyze(script).Count);
			Assert.Equal(3, Analyze(script, new GlobalAnalysisOptions { Ignored = ["gvar_quest_alpha"] }).Count);
			Assert.Equal(2, Analyze(script, new GlobalAnalysisOptions { Ignored = ["GVAR_QUEST_*"] }).Count);
			Assert.Equal(3, Analyze(script, new GlobalAnalysisOptions { Ignored = ["77"] }).Count);
			Assert.Equal(4, Analyze(script, new GlobalAnalysisOptions { Ignored = ["", "  ", "*"] }).Count);
		}

		[Fact]
		public void The_ignore_list_matches_the_macro_of_a_resolved_variable()
		{
			Assert.Empty(Analyze("\tvariable v := global_var(GVAR_IGNORED);\r\n", new GlobalAnalysisOptions { Ignored = ["GVAR_IGNORED"] }, "#define GVAR_IGNORED (9)\r\n"));
			Assert.Empty(Analyze("\tvariable v := global_var(GVAR_IGNORED);\r\n", new GlobalAnalysisOptions { Ignored = ["9"] }, "#define GVAR_IGNORED (9)\r\n"));
		}

		[Fact]
		public void An_sfall_global_used_as_int_and_float_is_a_warning()
		{
			IReadOnlyList<GlobalFinding> findings = Analyze("\tset_sfall_global(\"abcdefgh\", 1);\r\n\tset_sfall_global(\"abcdefgh\", 2);\r\n\tvariable v := get_sfall_global_float(\"abcdefgh\");\r\n");

			GlobalFinding finding = Assert.Single(Of(findings, GlobalFindingKind.TypeConflict));
			Assert.Equal(GlobalFindingSeverity.Warning, finding.Severity);
			Assert.Equal(4, finding.Line);
		}

		[Fact]
		public void The_same_type_everywhere_is_not_a_conflict()
		{
			Assert.Empty(Of(Analyze("\tset_sfall_global(\"abcdefgh\", 1.5);\r\n\tvariable v := get_sfall_global_float(\"abcdefgh\");\r\n"), GlobalFindingKind.TypeConflict));
			Assert.Empty(Of(Analyze("\tset_sfall_global(\"abcdefgh\", v1);\r\n\tvariable v := get_sfall_global_float(\"abcdefgh\");\r\n"), GlobalFindingKind.TypeConflict));
		}

		[Fact]
		public void A_name_that_is_not_8_characters_long_is_a_warning()
		{
			IReadOnlyList<GlobalFinding> findings = Analyze("\tset_sfall_global(\"short\", 1);\r\n\tvariable v := get_sfall_global_int(\"short\");\r\n\tset_sfall_global(\"abcdefghij\", 1);\r\n\tv := get_sfall_global_int(\"abcdefghij\");\r\n\tset_sfall_global(\"exactly8\", 1);\r\n\tv := get_sfall_global_int(\"exactly8\");\r\n");

			GlobalFinding[] lengths = Of(findings, GlobalFindingKind.NameLength);
			Assert.Equal(new[] { "abcdefghij", "short" }, lengths.Select(static f => f.Variable).OrderBy(static n => n, StringComparer.Ordinal).ToArray());
			Assert.Equal(5, lengths.Single(static f => f.Variable == "short").Number);
			Assert.Equal(10, lengths.Single(static f => f.Variable == "abcdefghij").Number);
			Assert.All(lengths, static f => Assert.Equal(GlobalFindingSeverity.Warning, f.Severity));
		}

		[Fact]
		public void The_length_rule_is_for_sfall_names_only()
		{
			Assert.Empty(Of(Analyze("\tvariable v := global_var(GVAR_A_LONG_NAME_INDEED);\r\n"), GlobalFindingKind.NameLength));
		}

		[Fact]
		public void Two_long_names_with_the_same_first_8_characters_collide()
		{
			IReadOnlyList<GlobalFinding> findings = Analyze("\tset_sfall_global(\"questflag_one\", 1);\r\n\tset_sfall_global(\"questflag_two\", 1);\r\n\tset_sfall_global(\"questfla\", 1);\r\n");

			GlobalFinding collision = Assert.Single(Of(findings, GlobalFindingKind.PrefixCollision));
			Assert.Equal(GlobalFindingSeverity.Warning, collision.Severity);
			Assert.Equal("questflag_two", collision.Variable);
			Assert.Equal("questflag_one", collision.Other);
			Assert.Equal(8, collision.Number);
		}

		[Theory]
		[InlineData("GVAR_QUEST_DONE", "GVAR_QUEST_DONr")]
		[InlineData("GVAR_QUEST_DONE", "gvar_quest_done")]
		[InlineData("GVAR_QUEST_DONE", "GVAR_QUEST_DON")]
		[InlineData("GVAR_QUEST_DONE", "GVAR_QUEST_DONEE")]
		[InlineData("GVAR_QUEST_DONE", "GVAR_QUEST_DOEN")]
		public void A_name_one_character_away_from_another_is_a_probable_typo(String first, String second)
		{
			IReadOnlyList<GlobalFinding> findings = Analyze("\tset_global_var(" + first + ", 1);\r\n\tvariable v := global_var(" + first + ");\r\n\tv := global_var(" + first + ");\r\n\tset_global_var(" + second + ", 1);\r\n\tv := global_var(" + second + ");\r\n");

			GlobalFinding finding = Assert.Single(Of(findings, GlobalFindingKind.SimilarName));
			Assert.Equal(GlobalFindingSeverity.Info, finding.Severity);
			Assert.Equal(second, finding.Variable);
			Assert.Equal(first, finding.Other);
		}

		[Theory]
		[InlineData("GVAR_ROOM1", "GVAR_ROOM2")]
		[InlineData("GVAR_ROOM_A", "GVAR_ROOM_B1")]
		[InlineData("GVAR_QUEST_DONE", "GVAR_QUEST_OPEN")]
		public void Names_that_differ_by_a_digit_or_by_more_are_not_typos(String first, String second)
		{
			IReadOnlyList<GlobalFinding> findings = Analyze("\tset_global_var(" + first + ", 1);\r\n\tvariable v := global_var(" + first + ");\r\n\tset_global_var(" + second + ", 1);\r\n\tv := global_var(" + second + ");\r\n");

			Assert.Empty(Of(findings, GlobalFindingKind.SimilarName));
		}

		[Fact]
		public void Two_macros_of_one_variable_are_not_a_typo()
		{
			Assert.Empty(Of(Analyze("\tset_global_var(GVAR_QUEST_DONE, 1);\r\n\tvariable v := global_var(GVAR_QUEST_DONr);\r\n", null, "#define GVAR_QUEST_DONE (3)\r\n#define GVAR_QUEST_DONr (3)\r\n"), GlobalFindingKind.SimilarName));
		}

		[Fact]
		public void Similar_sfall_names_are_found()
		{
			IReadOnlyList<GlobalFinding> findings = Analyze("\tset_sfall_global(\"questone\", 1);\r\n\tvariable v := get_sfall_global_int(\"questone\");\r\n\tv := get_sfall_global_int(\"questonf\");\r\n\tset_sfall_global(\"questonf\", 1);\r\n\tv := get_sfall_global_int(\"questone\");\r\n");

			Assert.Single(Of(findings, GlobalFindingKind.SimilarName));
		}

		[Fact]
		public void Local_variables_of_different_scripts_are_not_compared()
		{
			IReadOnlyList<GlobalFinding> findings = GlobalVariableAnalyzer.Analyze(GlobalVariableIndex.FromTexts(
			[
				new("c:\\p\\a.ssl", "procedure start begin\r\n\tset_local_var(LVAR_Done, 1);\r\n\tvariable v := local_var(LVAR_Done);\r\nend\r\n"),
				new("c:\\p\\b.ssl", "procedure start begin\r\n\tset_local_var(LVAR_Dona, 1);\r\n\tvariable v := local_var(LVAR_Dona);\r\nend\r\n"),
			]));

			Assert.Empty(findings);
		}

		[Fact]
		public void The_local_variables_of_a_header_say_nothing()
		{
			IReadOnlyList<GlobalFinding> findings = GlobalVariableAnalyzer.Analyze(GlobalVariableIndex.FromTexts(
			[
				new("c:\\p\\common.h", "#define mark_seen set_local_var(LVAR_Seen, 1)\r\n"),
			]));

			Assert.Empty(findings);
		}

		[Fact]
		public void Findings_are_ordered_by_file_and_line_and_capped()
		{
			String script = String.Concat(Enumerable.Range(0, 30).Select(static i => "\tset_global_var(" + (100 + i).ToString(System.Globalization.CultureInfo.InvariantCulture) + ", 1);\r\n"));

			IReadOnlyList<GlobalFinding> all = Analyze(script);
			IReadOnlyList<GlobalFinding> capped = Analyze(script, new GlobalAnalysisOptions { MaxFindings = 5 });

			Assert.Equal(30, all.Count);
			Assert.Equal(all.Select(static f => f.Line).OrderBy(static l => l).ToArray(), all.Select(static f => f.Line).ToArray());
			Assert.Equal(5, capped.Count);
		}

		[Theory]
		[InlineData("abcd", "abcd", true)]
		[InlineData("abcd", "abce", true)]
		[InlineData("abcd", "abdc", true)]
		[InlineData("abcd", "abcde", true)]
		[InlineData("abcd", "bcd", false)]
		[InlineData("abcd", "abc1", false)]
		[InlineData("abc1", "abc2", false)]
		[InlineData("abcd", "abxy", false)]
		[InlineData("abcdef", "abcdefgh", false)]
		public void The_typo_rule(String a, String b, Boolean expected)
		{
			Assert.Equal(expected, GlobalVariableAnalyzer.IsProbableTypo(a, b));
			Assert.Equal(expected, GlobalVariableAnalyzer.IsProbableTypo(b, a));
		}

		[Fact]
		public void The_csv_has_a_header_and_one_row_per_use_with_quoting()
		{
			GlobalVariableIndex index = GlobalVariableIndex.FromTexts(
			[
				new("c:\\p\\a,b.ssl", "#define GVAR_A (4)\r\nprocedure start begin\r\n\tset_global_var(GVAR_A, 7);\r\n\tvariable v := global_var(4);\r\nend\r\n"),
			]);

			String[] lines = GlobalVariableCsv.Format(index.Variables).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

			Assert.Equal("Kind,Variable,Number,Macros,Access,Function,Type,Value,File,Line,Procedure", lines[0]);
			Assert.Equal(3, lines.Length);
			Assert.Equal("GameVariable,GVAR_A,4,GVAR_A,Write,set_global_var,,7,\"c:\\p\\a,b.ssl\",3,start", lines[1]);
			Assert.Equal("GameVariable,GVAR_A,4,GVAR_A,Read,global_var,,,\"c:\\p\\a,b.ssl\",4,start", lines[2]);
		}

		[Fact]
		public void The_csv_doubles_the_quotes()
		{
			GlobalVariableIndex index = GlobalVariableIndex.FromTexts([new("c:\\p\\a\"b.ssl", "procedure start begin\r\n\tset_global_var(1, 1);\r\nend\r\n")]);

			Assert.Contains("\"c:\\p\\a\"\"b.ssl\"", GlobalVariableCsv.Format(index.Variables), StringComparison.Ordinal);
		}
	}
}