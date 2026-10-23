namespace FalloutScriptStudio.Core.Tests.Globals
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using FalloutScriptStudio.Core.Analysis;
	using FalloutScriptStudio.Core.Globals;
	using FalloutScriptStudio.TestSupport;
	using Xunit;

	public sealed class GlobalUseScannerTests
	{
		private static RawGlobalUse[] Uses(String text)
		{
			return GlobalUseScanner.ScanText(text).Uses;
		}

		private static String Names(RawGlobalUse[] uses)
		{
			return String.Join(",", uses.Select(static u => u.Function.Name + ":" + u.Key));
		}

		[Fact]
		public void The_table_names_exist_in_the_description_files()
		{
			String folder = Path.Combine(TestFiles.RepositoryRoot, "FalloutScriptStudio", "Content", "descriptions");
			String opcodes = File.ReadAllText(Path.Combine(folder, "opcodes.txt"));
			String functions = File.ReadAllText(Path.Combine(folder, "functions.ini"));
			foreach (GlobalFunction function in GlobalFunctionTable.Functions)
			{
				Boolean known = opcodes.Contains(" " + function.Name, StringComparison.OrdinalIgnoreCase) || functions.Contains(function.Name, StringComparison.OrdinalIgnoreCase);
				Assert.True(known, function.Name + " is in neither description file");
			}
		}

		[Theory]
		[InlineData("set_sfall_arg")]
		[InlineData("get_sfall_arg")]
		[InlineData("get_sfall_arg_at")]
		[InlineData("set_sfall_return")]
		[InlineData("set_global_script_repeat")]
		public void Hook_arguments_are_not_global_variables(String function)
		{
			Assert.False(GlobalFunctionTable.TryGet(function, out _));
			Assert.Empty(Uses("procedure start begin\r\n\t" + function + "(0, 1);\r\nend\r\n"));
		}

		[Fact]
		public void Every_family_is_found_with_its_access()
		{
			RawGlobalUse[] uses = Uses(
				"procedure start begin\r\n" +
				"\tvariable a;\r\n" +
				"\ta := global_var(10);\r\n" +
				"\tset_global_var(11, 5);\r\n" +
				"\tinc_global_var(12);\r\n" +
				"\tdec_map_var_amt(3, 2);\r\n" +
				"\tset_local_var(4, -1);\r\n" +
				"\ta := lvar_bit(5, 8);\r\n" +
				"\tset_gvar_bit_on(13, 4);\r\n" +
				"\tset_sfall_global(\"abcdefgh\", 7);\r\n" +
				"\ta := get_sfall_global_int(\"abcdefgh\");\r\n" +
				"\ta := get_sfall_global_float(99);\r\n" +
				"end\r\n");

			Assert.Equal(
				"global_var:10,set_global_var:11,inc_global_var:12,dec_map_var_amt:3,set_local_var:4,lvar_bit:5,set_gvar_bit_on:13,set_sfall_global:abcdefgh,get_sfall_global_int:abcdefgh,get_sfall_global_float:99",
				Names(uses));
			Assert.Equal(GlobalAccess.Read, uses[0].Function.Access);
			Assert.Equal(GlobalAccess.Write, uses[1].Function.Access);
			Assert.Equal(GlobalAccess.ReadWrite, uses[2].Function.Access);
			Assert.Equal(GlobalKeyKind.Name, uses[7].KeyKind);
			Assert.Equal(GlobalKeyKind.Id, uses[9].KeyKind);
		}

		[Fact]
		public void The_line_the_procedure_and_the_literal_are_recorded()
		{
			FileGlobalScan scan = GlobalUseScanner.ScanText(
				"procedure first begin\r\n" +
				"\tset_global_var(1, 5);\r\n" +
				"end\r\n" +
				"\r\n" +
				"procedure second begin\r\n" +
				"\tset_sfall_global(\"abcdefgh\", 2.5);\r\n" +
				"\tset_sfall_global(\"abcdefgh\", -3);\r\n" +
				"\tset_sfall_global(\"abcdefgh\", 1 + 2);\r\n" +
				"end\r\n");

			Assert.Equal(4, scan.Uses.Length);
			Assert.Equal(2, scan.Uses[0].Line);
			Assert.Equal("first", scan.Procedures[scan.Uses[0].Procedure]);
			Assert.Equal("5", scan.Uses[0].Value);
			Assert.Equal("second", scan.Procedures[scan.Uses[1].Procedure]);
			Assert.Equal("2.5", scan.Uses[1].Value);
			Assert.Equal("-3", scan.Uses[2].Value);
			Assert.Null(scan.Uses[3].Value);
		}

		[Fact]
		public void Macros_and_numbers_are_kept_as_written()
		{
			RawGlobalUse[] uses = Uses("#define GVAR_FOO (27)\r\nprocedure start begin\r\n\tset_global_var(GVAR_FOO, 1);\r\n\tset_global_var((GVAR_FOO), 1);\r\n\tset_global_var(0x1B, 1);\r\nend\r\n");

			Assert.Equal(GlobalKeyKind.Symbol, uses[0].KeyKind);
			Assert.Equal("GVAR_FOO", uses[0].Key);
			Assert.Equal("GVAR_FOO", uses[1].Key);
			Assert.Equal(GlobalKeyKind.Id, uses[2].KeyKind);
			Assert.Equal("27", uses[2].Key);
		}

		[Fact]
		public void Comments_and_strings_are_not_scanned()
		{
			Assert.Empty(Uses(
				"procedure start begin\r\n" +
				"\t// set_global_var(1, 2);\r\n" +
				"\t/* global_var(3) */\r\n" +
				"\tdisplay_msg(\"global_var(4) and set_sfall_global(\\\"abcdefgh\\\", 1)\");\r\n" +
				"end\r\n"));
		}

		[Fact]
		public void Variables_parameters_and_expressions_are_not_global_variable_names()
		{
			Assert.Empty(Uses(
				"procedure helper(variable index) begin\r\n" +
				"\tvariable slot;\r\n" +
				"\tslot := global_var(index);\r\n" +
				"\tslot := global_var(slot);\r\n" +
				"\tslot := global_var(slot + 1);\r\n" +
				"\tslot := global_var(random(1, 3));\r\n" +
				"\tset_sfall_global(\"abcdefgh\" + \"x\", 1);\r\n" +
				"\tset_global_var(\"text\", 1);\r\n" +
				"end\r\n"));
		}

		[Fact]
		public void A_variable_is_a_macro_only_when_the_script_does_not_declare_it()
		{
			Assert.Single(Uses("variable shared;\r\nprocedure start begin\r\n\tset_global_var(shared, 1);\r\n\tset_global_var(GVAR_REAL, 1);\r\nend\r\n"));
		}

		[Fact]
		public void The_body_of_a_macro_counts_and_its_parameters_do_not()
		{
			FileGlobalScan scan = GlobalUseScanner.ScanText(
				"#define set_flag(value) set_global_var(GVAR_FLAG, value)\r\n" +
				"#define read_any(index) global_var(index)\r\n" +
				"#define read_flag global_var(GVAR_FLAG)\r\n" +
				"procedure start begin\r\n" +
				"end\r\n");

			Assert.Equal(2, scan.Uses.Length);
			Assert.Equal("set_global_var", scan.Uses[0].Function.Name);
			Assert.Equal("GVAR_FLAG", scan.Uses[0].Key);
			Assert.True(scan.Uses[0].ValueIsSymbol is false);
			Assert.Equal("#define set_flag", scan.Procedures[scan.Uses[0].Procedure]);
			Assert.Equal("#define read_flag", scan.Procedures[scan.Uses[1].Procedure]);
		}

		[Fact]
		public void The_definition_of_a_function_like_macro_with_the_name_of_a_function_is_not_a_use()
		{
			Assert.Empty(Uses("#define global_var(x) (x)\r\n#define map_var(x) 0\r\n"));
		}

		[Fact]
		public void A_value_that_is_a_macro_is_kept_as_a_symbol()
		{
			RawGlobalUse use = Uses("procedure start begin\r\n\tset_global_var(5, VALUE_FULL);\r\nend\r\n").Single();

			Assert.Equal("VALUE_FULL", use.Value);
			Assert.True(use.ValueIsSymbol);
		}

		[Fact]
		public void A_call_spread_over_several_lines_is_found()
		{
			RawGlobalUse use = Uses("procedure start begin\r\n\tset_global_var(\r\n\t\tGVAR_A,\r\n\t\t3\r\n\t);\r\nend\r\n").Single();

			Assert.Equal("GVAR_A", use.Key);
			Assert.Equal("3", use.Value);
			Assert.Equal(2, use.Line);
		}

		[Fact]
		public void An_unfinished_call_is_ignored()
		{
			Assert.Empty(Uses("procedure start begin\r\n\tset_global_var(1, 2;\r\nend\r\n"));
			Assert.Empty(Uses("procedure start begin\r\n\tset_global_var(\r\n"));
			Assert.Empty(Uses("procedure start begin\r\n\tset_global_var()\r\nend\r\n"));
		}

		[Fact]
		public void A_name_without_parentheses_is_not_a_call()
		{
			Assert.Empty(Uses("procedure start begin\r\n\tvariable a := global_var;\r\nend\r\n"));
		}

		[Fact]
		public void The_function_names_ignore_the_case()
		{
			Assert.Equal(2, Uses("procedure start begin\r\n\tSet_Global_Var(1, 2);\r\n\tGLOBAL_VAR(1);\r\nend\r\n").Length);
		}

		[Theory]
		[InlineData("(27)", "Number", "27")]
		[InlineData("27", "Number", "27")]
		[InlineData("((27))", "Number", "27")]
		[InlineData("0x1B", "Number", "0x1B")]
		[InlineData("(-1)", "Number", "-1")]
		[InlineData("1.5", "Number", "1.5")]
		[InlineData("\"mygvar01\"", "String", "mygvar01")]
		[InlineData("(\"mygvar01\")", "String", "mygvar01")]
		[InlineData("OTHER_NAME", "Alias", "OTHER_NAME")]
		[InlineData("(OTHER_NAME) // trailing", "Alias", "OTHER_NAME")]
		[InlineData("27 /* note */", "Number", "27")]
		public void Constants_are_read(String body, String kind, String value)
		{
			Assert.True(GlobalUseScanner.TryParseConstant(body, out MacroConstantKind actualKind, out String actualValue));
			Assert.Equal(kind, actualKind.ToString());
			Assert.Equal(value, actualValue);
		}

		[Theory]
		[InlineData("")]
		[InlineData("(1 + 2)")]
		[InlineData("a + b")]
		[InlineData("-NAME")]
		[InlineData("\"a\" \"b\"")]
		[InlineData("call something(1)")]
		public void Other_bodies_are_not_constants(String body)
		{
			Assert.False(GlobalUseScanner.TryParseConstant(body, out _, out _));
		}

		[Fact]
		public void The_constants_of_a_file_are_collected_from_its_object_like_macros()
		{
			FileGlobalScan scan = GlobalUseScanner.ScanText("#define GVAR_A (1)\r\n#define NAME_A \"abcdefgh\"\r\n#define ALIAS GVAR_A\r\n#define FN(x) (x)\r\n#define EXPR (1 + 2)\r\n");

			Assert.Equal(new[] { "ALIAS", "GVAR_A", "NAME_A" }, scan.Constants.Select(static c => c.Name).OrderBy(static n => n, StringComparer.Ordinal).ToArray());
		}

		[Fact]
		public void The_hint_test_skips_the_files_without_a_candidate()
		{
			Assert.False(GlobalUseScanner.MayContainUses("procedure start begin\r\n\tdisplay_msg(\"hi\");\r\nend\r\n"));
			Assert.True(GlobalUseScanner.MayContainUses("x := GLOBAL_VAR(1);"));
			Assert.True(GlobalUseScanner.MayContainUses("set_sfall_global"));
			Assert.True(GlobalUseScanner.MayContainUses("set_mvar_bit_on(1, 2)"));
		}

		[Theory]
		[InlineData("27", 27L)]
		[InlineData("-1", -1L)]
		[InlineData("0x1B", 27L)]
		[InlineData("0X10", 16L)]
		public void Integers_are_parsed(String text, Int64 expected)
		{
			Assert.True(GlobalNumbers.TryParseInteger(text, out Int64 value));
			Assert.Equal(expected, value);
		}

		[Theory]
		[InlineData("1.5")]
		[InlineData("abc")]
		[InlineData("")]
		[InlineData("-")]
		[InlineData("0x")]
		public void Other_texts_are_not_integers(String text)
		{
			Assert.False(GlobalNumbers.TryParseInteger(text, out _));
		}

		[Fact]
		public void Equal_texts_share_one_instance_through_the_pool()
		{
			GlobalStringPool pool = new();
			String text = "procedure a begin\r\n\tset_global_var(GVAR_SHARED, 1);\r\nend\r\n";
			FileGlobalScan first = GlobalUseScanner.ScanText(text, pool);
			FileGlobalScan second = GlobalUseScanner.ScanText(text, pool);

			Assert.Same(first.Uses[0].Key, second.Uses[0].Key);
			Assert.Same(first.Procedures[0], second.Procedures[0]);
		}

		[Fact]
		public void A_script_of_20000_lines_is_scanned_without_failing()
		{
			String block = "procedure p{0} begin\r\n\tvariable v;\r\n\tv := global_var(GVAR_X{0});\r\n\tset_sfall_global(\"abcdefgh\", v);\r\nend\r\n";
			String text = String.Concat(Enumerable.Range(0, 4000).Select(i => String.Format(System.Globalization.CultureInfo.InvariantCulture, block, i)));

			FileGlobalScan scan = GlobalUseScanner.ScanText(text);

			Assert.Equal(8000, scan.Uses.Length);
			Assert.Equal(4000, scan.Procedures.Length);
		}
	}
}