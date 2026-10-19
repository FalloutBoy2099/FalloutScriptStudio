namespace FalloutScriptStudio.Core.Tests.Analysis
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading;
	using FalloutScriptStudio.Core.Analysis;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.TestSupport;
	using Xunit;

	/// <summary>The details of <see cref="ScriptDiagnostics"/>: where a finding is, what its message says, how the environment changes it.</summary>
	public class ScriptDiagnosticsTests
	{
		private static ScriptDiagnostic Single(IEnumerable<ScriptDiagnostic> diagnostics, String code)
		{
			return Assert.Single(diagnostics, d => d.Code == code);
		}

		private static ScriptEnvironment WithMacros(params ScriptMacro[] macros)
		{
			Dictionary<String, ScriptMacro> map = macros.ToDictionary(m => m.Token, StringComparer.Ordinal);
			return new ScriptEnvironment { FindMacro = name => map.GetValueOrDefault(name), Functions = DiagnosticsFixture.ShippedCatalog };
		}

		private static ScriptMacro Macro(String token, String code)
		{
			return new ScriptMacro(token, token, code, "header.h", 1, null);
		}

		[Fact]
		public void A_wrong_argument_count_points_at_the_name_and_states_the_range()
		{
			String text = Ssl.Crlf(
				"procedure foo(variable a, variable b := 1) begin end",
				"procedure start begin",
				"\tcall foo(1, 2, 3);",
				"end");

			ScriptDiagnostic diagnostic = Single(DiagnosticsFixture.Analyze(text), "SSL1001");

			Assert.Equal(ScriptDiagnosticSeverity.Error, diagnostic.Severity);
			Assert.Equal("Str.Editor.Diag.WrongArgumentCount", diagnostic.MessageKey);
			Assert.Equal(new[] { "foo", "1-2", "3" }, diagnostic.MessageArguments);
			Assert.Equal(3, diagnostic.Line);
			Assert.Equal(7, diagnostic.Column);
			Assert.Equal("foo", text.Substring(diagnostic.Offset, diagnostic.Length));
		}

		[Fact]
		public void The_hint_key_follows_the_message_key()
		{
			ScriptDiagnostic diagnostic = Single(DiagnosticsFixture.AnalyzeLines("procedure start begin", "\tundefined_thing(1);", "end"), "SSL1002");

			Assert.Equal("Str.Editor.Diag.UndefinedProcedure", diagnostic.MessageKey);
			Assert.Equal("Str.Editor.Diag.UndefinedProcedureHint", diagnostic.HintKey);
			Assert.Equal(ScriptDiagnosticCodes.UndefinedProcedure.HintKey, diagnostic.HintKey);
		}

		[Fact]
		public void A_typo_gets_the_nearest_known_name_as_a_rename_fix()
		{
			String text = Ssl.Crlf("procedure start begin", "\tdisplay_msgg(\"a\");", "end");

			ScriptDiagnostic diagnostic = Single(DiagnosticsFixture.Analyze(text), "SSL1002");

			Assert.NotNull(diagnostic.Fix);
			Assert.Equal(ScriptQuickFix.RenameId, diagnostic.Fix!.Id);
			Assert.Equal("display_msg", diagnostic.Fix.Argument);
			Assert.Equal(new[] { "display_msgg" }, diagnostic.MessageArguments);
		}

		[Fact]
		public void The_nearest_name_may_be_a_procedure_or_a_variable_of_the_script()
		{
			String text = Ssl.Crlf(
				"variable counter;",
				"procedure compute_total(variable a) begin return a; end",
				"procedure start begin",
				"\tvariable x;",
				"\tx := compute_totl(counter);",
				"\tx := countr;",
				"end");

			IReadOnlyList<ScriptDiagnostic> diagnostics = DiagnosticsFixture.Analyze(text);

			Assert.Equal("compute_total", Single(diagnostics, "SSL1002").Fix!.Argument);
			Assert.Equal("counter", Single(diagnostics, "SSL1003").Fix!.Argument);
		}

		[Fact]
		public void A_name_with_nothing_near_gets_no_fix()
		{
			ScriptDiagnostic diagnostic = Single(DiagnosticsFixture.AnalyzeLines("procedure start begin", "\tqqqqzzzzxxxx(1);", "end"), "SSL1002");

			Assert.Null(diagnostic.Fix);
		}

		[Fact]
		public void Suggestions_can_be_turned_off()
		{
			ScriptDiagnosticOptions options = new ScriptDiagnosticOptions { SuggestNames = false };

			IReadOnlyList<ScriptDiagnostic> diagnostics = ScriptDiagnostics.Analyze("procedure start begin\r\n\tdisplay_msgg(\"a\");\r\nend\r\n", DiagnosticsFixture.ShippedEnvironment, options);

			Assert.Null(Single(diagnostics, "SSL1002").Fix);
		}

		[Fact]
		public void A_procedure_used_before_its_definition_offers_a_forward_declaration()
		{
			String text = Ssl.Crlf(
				"procedure start begin",
				"\tcall foo(1, 2);",
				"end",
				"procedure foo(variable a, variable b) begin end");

			ScriptDiagnostic diagnostic = Single(DiagnosticsFixture.Analyze(text), "SSL1004");

			Assert.Equal(new[] { "foo", "4" }, diagnostic.MessageArguments);
			Assert.NotNull(diagnostic.Fix);
			Assert.Equal(ScriptQuickFix.ForwardDeclareId, diagnostic.Fix!.Id);
			Assert.Equal("procedure foo(variable a, variable b);", diagnostic.Fix.Argument);
			Assert.Equal(0, diagnostic.Fix.TargetOffset);
		}

		[Fact]
		public void The_forward_declaration_goes_in_front_of_the_first_procedure_not_in_front_of_the_comment_of_another()
		{
			String text = Ssl.Crlf(
				"#include \"a.h\"",
				"variable g;",
				"  procedure helper begin end",
				"procedure start begin",
				"\tcall late;",
				"end",
				"procedure late begin end");

			ScriptDiagnostic diagnostic = Single(DiagnosticsFixture.Analyze(text), "SSL1004");

			Assert.Equal(text.IndexOf("  procedure helper", StringComparison.Ordinal), diagnostic.Fix!.TargetOffset);
		}

		[Fact]
		public void A_procedure_with_default_values_or_modifiers_gets_no_forward_declaration_fix()
		{
			String defaults = Ssl.Crlf("procedure start begin", "\tcall foo;", "end", "procedure foo(variable a := 1) begin end");
			String modifier = Ssl.Crlf("procedure start begin", "\tcall foo;", "end", "inline procedure foo begin end");

			Assert.Null(Single(DiagnosticsFixture.Analyze(defaults), "SSL1004").Fix);
			Assert.Null(Single(DiagnosticsFixture.Analyze(modifier), "SSL1004").Fix);
		}

		[Fact]
		public void A_duplicate_variable_without_a_value_can_be_removed_and_one_with_a_value_cannot()
		{
			String plain = Ssl.Crlf("variable a;", "variable a;", "procedure start begin end");
			String valued = Ssl.Crlf("variable a;", "variable a := 5;", "procedure start begin end");

			ScriptDiagnostic removable = Single(DiagnosticsFixture.Analyze(plain), "SSL1009");
			ScriptDiagnostic kept = Single(DiagnosticsFixture.Analyze(valued), "SSL1009");

			Assert.Equal(ScriptQuickFix.RemoveDeclarationId, removable.Fix!.Id);
			Assert.Equal(new[] { "a", "1" }, removable.MessageArguments);
			Assert.Null(kept.Fix);
		}

		[Fact]
		public void A_forward_declaration_after_the_definition_can_be_removed()
		{
			String text = Ssl.Crlf("procedure foo begin end", "procedure foo;", "procedure start begin end");

			ScriptDiagnostic diagnostic = Single(DiagnosticsFixture.Analyze(text), "SSL1008");

			Assert.Equal(ScriptQuickFix.RemoveDeclarationId, diagnostic.Fix!.Id);
			Assert.Equal(ScriptQuickFixes.StatementTarget, diagnostic.Fix.Argument);
		}

		[Fact]
		public void A_begin_without_end_is_reported_at_the_open_begin_with_the_name_of_the_procedure()
		{
			String text = Ssl.Crlf("procedure first begin", "\tdisplay_msg(\"a\");", "procedure second begin", "end");

			ScriptDiagnostic diagnostic = Single(DiagnosticsFixture.Analyze(text), "SSL1010");

			Assert.Equal(new[] { "first" }, diagnostic.MessageArguments);
			Assert.Equal(1, diagnostic.Line);
			Assert.Equal("begin", text.Substring(diagnostic.Offset, diagnostic.Length));
		}

		[Fact]
		public void An_end_without_begin_is_reported_at_the_stray_end()
		{
			String text = Ssl.Crlf("procedure start begin", "end", "end");

			ScriptDiagnostic diagnostic = Single(DiagnosticsFixture.Analyze(text), "SSL1011");

			Assert.Equal(3, diagnostic.Line);
			Assert.Equal(1, diagnostic.Column);
			Assert.Equal(3, diagnostic.Length);
		}

		[Fact]
		public void A_missing_then_is_reported_at_the_token_that_ends_the_condition()
		{
			String text = Ssl.Crlf("procedure start begin", "\tif (1) begin", "\tend", "end");

			ScriptDiagnostic diagnostic = Single(DiagnosticsFixture.Analyze(text), "SSL1012");

			Assert.Equal(2, diagnostic.Line);
			Assert.Equal("begin", text.Substring(diagnostic.Offset, diagnostic.Length));
		}

		[Fact]
		public void A_condition_with_parentheses_in_it_is_read_up_to_its_then()
		{
			String text = Ssl.Crlf("procedure start begin", "\tvariable a;", "\tif (a == 1) and (a < (2 + 3)) then begin", "\tend", "end");

			Assert.DoesNotContain(DiagnosticsFixture.Analyze(text), d => d.Code is "SSL1012" or "SSL1013");
		}

		[Fact]
		public void A_macro_that_may_hide_a_then_silences_the_check()
		{
			ScriptEnvironment environment = WithMacros(Macro("THEN_BEGIN", "then begin"));
			String text = Ssl.Crlf("procedure start begin", "\tif (1) THEN_BEGIN", "\tend", "end");

			IReadOnlyList<ScriptDiagnostic> diagnostics = ScriptDiagnostics.Analyze(text, environment);

			Assert.DoesNotContain(diagnostics, d => d.Code is "SSL1012" or "SSL1010" or "SSL1011");
		}

		[Fact]
		public void A_macro_that_declares_variables_silences_the_missing_names_of_its_procedure_only()
		{
			ScriptEnvironment environment = WithMacros(Macro("DECLARE", "variable tmp;"));
			String text = Ssl.Crlf(
				"procedure first begin",
				"\tDECLARE",
				"\ttmp := 1;",
				"end",
				"procedure second begin",
				"\ttmp := 1;",
				"end");

			IReadOnlyList<ScriptDiagnostic> diagnostics = ScriptDiagnostics.Analyze(text, environment);

			ScriptDiagnostic diagnostic = Assert.Single(diagnostics);
			Assert.Equal(6, diagnostic.Line);
		}

		[Fact]
		public void A_use_of_a_macro_at_the_top_level_silences_the_missing_names_after_it()
		{
			ScriptEnvironment environment = WithMacros(Macro("MAKE", "procedure made begin end"));
			String text = Ssl.Crlf(
				"procedure before begin",
				"\tcall nothing_yet;",
				"end",
				"MAKE",
				"procedure start begin",
				"\tcall made;",
				"end");

			IReadOnlyList<ScriptDiagnostic> diagnostics = ScriptDiagnostics.Analyze(text, environment);

			ScriptDiagnostic diagnostic = Assert.Single(diagnostics);
			Assert.Equal("SSL1002", diagnostic.Code);
			Assert.Equal(2, diagnostic.Line);
		}

		[Fact]
		public void Macros_of_the_environment_are_names_and_are_case_sensitive()
		{
			ScriptEnvironment environment = WithMacros(Macro("LIMIT", "10"));
			String text = Ssl.Crlf("procedure start begin", "\tvariable x;", "\tx := LIMIT;", "\tx := limit;", "end");

			IReadOnlyList<ScriptDiagnostic> diagnostics = ScriptDiagnostics.Analyze(text, environment);

			ScriptDiagnostic diagnostic = Assert.Single(diagnostics);
			Assert.Equal("SSL1003", diagnostic.Code);
			Assert.Equal(4, diagnostic.Line);
		}

		[Fact]
		public void A_constant_macro_of_the_environment_cannot_be_assigned()
		{
			ScriptEnvironment environment = WithMacros(Macro("LIMIT", "(10)"), Macro("CURRENT", "g_current"));
			String text = Ssl.Crlf("variable g_current;", "procedure start begin", "\tLIMIT := 3;", "\tCURRENT := 3;", "end");

			IReadOnlyList<ScriptDiagnostic> diagnostics = ScriptDiagnostics.Analyze(text, environment);

			ScriptDiagnostic diagnostic = Assert.Single(diagnostics);
			Assert.Equal("SSL1007", diagnostic.Code);
			Assert.Equal(3, diagnostic.Line);
		}

		[Fact]
		public void A_header_that_could_not_be_read_silences_the_missing_names_but_not_the_structure()
		{
			ScriptEnvironment environment = new ScriptEnvironment { IsComplete = false, Functions = DiagnosticsFixture.ShippedCatalog };
			String text = Ssl.Crlf("procedure start begin", "\tundefined_thing(1);", "\tx := y;", "end", "end");

			IReadOnlyList<ScriptDiagnostic> diagnostics = ScriptDiagnostics.Analyze(text, environment);

			ScriptDiagnostic diagnostic = Assert.Single(diagnostics);
			Assert.Equal("SSL1011", diagnostic.Code);
		}

		[Fact]
		public void Procedures_and_variables_of_the_headers_are_known_after_the_include()
		{
			ExternalSymbols symbols = ExternalSymbols.Create(new[]
			{
				new ScriptSymbolTable(new[] { new ScriptProcedureSymbol("lib_helper", 1, 2, true) }, new[] { "lib_counter" }, Array.Empty<String>(), false),
			});
			ScriptEnvironment environment = new ScriptEnvironment { Symbols = symbols, Functions = DiagnosticsFixture.ShippedCatalog };
			String text = Ssl.Crlf(
				"#include \"lib.h\"",
				"procedure start begin",
				"\tvariable x;",
				"\tx := lib_helper(1);",
				"\tx := lib_helper(1, 2);",
				"\tx := lib_helper();",
				"\tlib_counter := 1;",
				"end");

			IReadOnlyList<ScriptDiagnostic> diagnostics = ScriptDiagnostics.Analyze(text, environment);

			ScriptDiagnostic diagnostic = Assert.Single(diagnostics);
			Assert.Equal("SSL1001", diagnostic.Code);
			Assert.Equal(6, diagnostic.Line);
			Assert.Equal(new[] { "lib_helper", "1-2", "0" }, diagnostic.MessageArguments);
		}

		[Fact]
		public void A_name_of_a_header_used_before_the_include_is_not_known_yet()
		{
			ExternalSymbols symbols = ExternalSymbols.Create(new[] { new ScriptSymbolTable(Array.Empty<ScriptProcedureSymbol>(), new[] { "lib_counter" }, Array.Empty<String>(), false) });
			ScriptEnvironment environment = new ScriptEnvironment { Symbols = symbols, Functions = DiagnosticsFixture.ShippedCatalog };
			String text = Ssl.Crlf("variable early := lib_counter;", "procedure start begin", "\tlib_counter := 1;", "end", "#include \"late.h\"", "procedure other begin", "\tlib_counter := 2;", "end");

			IReadOnlyList<ScriptDiagnostic> diagnostics = ScriptDiagnostics.Analyze(text, environment);

			Assert.Equal(new[] { 3 }, diagnostics.Where(d => d.Code == "SSL1003").Select(d => d.Line));
		}

		[Fact]
		public void A_name_that_is_only_declared_under_another_condition_is_never_missing_and_errors_inside_a_condition_are_warnings()
		{
			String text = Ssl.Crlf(
				"#ifdef MAIN",
				"procedure start begin",
				"\tundefined_thing(1);",
				"end",
				"#endif");

			ScriptDiagnostic diagnostic = Single(DiagnosticsFixture.Analyze(text), "SSL1002");

			Assert.Equal(ScriptDiagnosticSeverity.Warning, diagnostic.Severity);
		}

		[Fact]
		public void Duplicates_in_different_conditional_blocks_are_not_duplicates()
		{
			String text = Ssl.Crlf(
				"#ifdef A",
				"variable v;",
				"#endif",
				"#ifdef B",
				"variable v;",
				"#endif",
				"procedure start begin end");

			Assert.DoesNotContain(DiagnosticsFixture.Analyze(text), d => d.Code == "SSL1009");
		}

		[Fact]
		public void Duplicates_in_the_same_conditional_block_are_reported()
		{
			String text = Ssl.Crlf("#ifndef GUARD", "#define GUARD", "variable v;", "variable v;", "#endif", "procedure start begin end");

			ScriptDiagnostic diagnostic = Single(DiagnosticsFixture.Analyze(text), "SSL1009");

			Assert.Equal(4, diagnostic.Line);
		}

		[Fact]
		public void The_include_guard_of_a_header_does_not_hide_its_declarations()
		{
			String text = Ssl.Crlf("#ifndef LIB_H", "#define LIB_H", "procedure lib_a begin end", "procedure lib_a begin end", "#endif");

			Assert.Contains(DiagnosticsFixture.Analyze(text), d => d.Code == "SSL1008");
		}

		[Fact]
		public void A_catalog_call_with_the_wrong_count_states_the_range_of_the_description()
		{
			ScriptDiagnostic diagnostic = Single(DiagnosticsFixture.AnalyzeLines("procedure start begin", "\tdisplay_msg(\"a\", \"b\");", "end"), "SSL2001");

			Assert.Equal(ScriptDiagnosticSeverity.Warning, diagnostic.Severity);
			Assert.Equal(new[] { "display_msg", "1", "2" }, diagnostic.MessageArguments);
		}

		[Fact]
		public void Optional_and_variadic_parameters_widen_the_range()
		{
			FunctionSignatureCatalog catalog = FunctionSignatureCatalog.Create(
				new[] { "int  message_box (string message, [int flags], [int color1]) | - shows a box", "string string_format (string format, any val1, ...) | - formats" },
				Array.Empty<String>());
			ScriptEnvironment environment = new ScriptEnvironment { Functions = catalog };
			String text = Ssl.Crlf(
				"procedure start begin",
				"\tvariable x;",
				"\tx := message_box(\"a\");",
				"\tx := message_box(\"a\", 1, 2);",
				"\tx := message_box();",
				"\tx := message_box(\"a\", 1, 2, 3);",
				"\tx := string_format(\"a\", 1);",
				"\tx := string_format(\"a\", 1, 2, 3, 4, 5);",
				"\tx := string_format(\"a\");",
				"end");

			IReadOnlyList<ScriptDiagnostic> diagnostics = ScriptDiagnostics.Analyze(text, environment);

			Assert.Equal(new[] { 5, 6, 9 }, diagnostics.Where(d => d.Code == "SSL2001").Select(d => d.Line));
			Assert.Equal(new[] { "message_box", "1-3", "0" }, diagnostics.First(d => d.Line == 5).MessageArguments);
			Assert.Equal(new[] { "string_format", "2+", "1" }, diagnostics.First(d => d.Line == 9).MessageArguments);
		}

		[Fact]
		public void A_deprecated_function_is_an_information()
		{
			FunctionSignatureCatalog catalog = FunctionSignatureCatalog.Create(new[] { "int old_thing (int a) | - Deprecated, use new_thing instead" }, Array.Empty<String>());
			ScriptEnvironment environment = new ScriptEnvironment { Functions = catalog };

			IReadOnlyList<ScriptDiagnostic> diagnostics = ScriptDiagnostics.Analyze("procedure start begin\r\n\tvariable x;\r\n\tx := old_thing(1);\r\nend\r\n", environment);

			ScriptDiagnostic diagnostic = Assert.Single(diagnostics);
			Assert.Equal("SSL2003", diagnostic.Code);
			Assert.Equal(ScriptDiagnosticSeverity.Info, diagnostic.Severity);
		}

		[Fact]
		public void The_types_of_literals_are_compared_with_the_description()
		{
			FunctionSignatureCatalog catalog = FunctionSignatureCatalog.Create(
				new[] { "void show (string text, int count, ObjectPtr who, any thing) | - shows" },
				Array.Empty<String>());
			ScriptEnvironment environment = new ScriptEnvironment { Functions = catalog };
			String text = Ssl.Crlf("procedure start begin", "\tshow(\"a\", \"b\", dude_obj, \"c\");", "\tshow(1, 2, \"x\", 5);", "end");

			IReadOnlyList<ScriptDiagnostic> diagnostics = ScriptDiagnostics.Analyze(text, environment);

			Assert.Equal(new[] { (2, "SSL2002"), (3, "SSL2002") }, diagnostics.Select(d => (d.Line, d.Code)));
			Assert.Equal(new[] { "show", "2", "int", "string" }, diagnostics[0].MessageArguments);
			Assert.Equal(new[] { "show", "3", "ObjectPtr", "string" }, diagnostics[1].MessageArguments);
		}

		[Fact]
		public void A_number_given_for_a_string_is_only_reported_when_asked_for()
		{
			FunctionSignatureCatalog catalog = FunctionSignatureCatalog.Create(new[] { "void show (string text) | - shows" }, Array.Empty<String>());
			ScriptEnvironment environment = new ScriptEnvironment { Functions = catalog };
			String text = "procedure start begin\r\n\tshow(5);\r\nend\r\n";

			Assert.Empty(ScriptDiagnostics.Analyze(text, environment));
			ScriptDiagnostic diagnostic = Assert.Single(ScriptDiagnostics.Analyze(text, environment, new ScriptDiagnosticOptions { ReportNumbersForStrings = true }));
			Assert.Equal(ScriptDiagnosticSeverity.Info, diagnostic.Severity);
		}

		[Fact]
		public void The_checks_of_functions_can_be_turned_off()
		{
			ScriptDiagnosticOptions options = new ScriptDiagnosticOptions { CheckFunctionCalls = false };

			IReadOnlyList<ScriptDiagnostic> diagnostics = ScriptDiagnostics.Analyze("procedure start begin\r\n\tdisplay_msg();\r\nend\r\n", DiagnosticsFixture.ShippedEnvironment, options);

			Assert.Empty(diagnostics);
		}

		[Fact]
		public void A_name_that_is_a_user_procedure_or_a_macro_is_not_checked_against_the_catalog()
		{
			ScriptEnvironment environment = WithMacros(Macro("get_critter_stat", "stat_hook"));
			String text = Ssl.Crlf(
				"procedure display_msg(variable a, variable b) begin end",
				"procedure start begin",
				"\tcall display_msg(1, 2);",
				"\tvariable x;",
				"\tx := get_critter_stat(1);",
				"end");

			Assert.Empty(ScriptDiagnostics.Analyze(text, environment));
		}

		[Fact]
		public void Names_of_the_catalog_that_the_compiler_does_not_know_by_itself_are_not_missing()
		{
			FunctionSignatureCatalog catalog = FunctionSignatureCatalog.Create(Array.Empty<String>(), new[] { "NewSfallThing<d>[int] New<s>new_sfall_thing({int a})" });
			ScriptEnvironment environment = new ScriptEnvironment { Functions = catalog };
			String text = "procedure start begin\r\n\tvariable x;\r\n\tx := new_sfall_thing(1);\r\n\tx := new_sfall_thing(1, 2);\r\nend\r\n";

			IReadOnlyList<ScriptDiagnostic> diagnostics = ScriptDiagnostics.Analyze(text, environment);

			ScriptDiagnostic diagnostic = Assert.Single(diagnostics);
			Assert.Equal("SSL2001", diagnostic.Code);
			Assert.Equal(4, diagnostic.Line);
		}

		[Fact]
		public void The_diagnostics_are_in_text_order()
		{
			String text = Ssl.Crlf("procedure start begin", "\tb_missing;", "\ta_missing;", "end", "end");

			IReadOnlyList<ScriptDiagnostic> diagnostics = DiagnosticsFixture.Analyze(text);

			Assert.Equal(diagnostics.OrderBy(d => d.Offset), diagnostics);
			Assert.Equal(3, diagnostics.Count);
		}

		[Fact]
		public void The_number_of_diagnostics_is_limited()
		{
			String text = "procedure start begin\r\n" + String.Concat(Enumerable.Range(0, 800).Select(i => "\tmissing_" + i + ";\r\n")) + "end\r\n";

			IReadOnlyList<ScriptDiagnostic> diagnostics = ScriptDiagnostics.Analyze(text, null, new ScriptDiagnosticOptions { MaximumCount = 25 });

			Assert.Equal(25, diagnostics.Count);
		}

		[Fact]
		public void A_cancelled_analysis_throws()
		{
			using CancellationTokenSource source = new CancellationTokenSource();
			source.Cancel();
			String text = "procedure start begin\r\nend\r\n" + String.Concat(Enumerable.Range(0, 3000).Select(i => "procedure p" + i + " begin end\r\n"));

			Assert.ThrowsAny<OperationCanceledException>(() => ScriptDiagnostics.Analyze(text, null, null, source.Token));
		}

		[Fact]
		public void FindAt_prefers_the_most_serious_diagnostic_and_can_require_a_fix()
		{
			ScriptDiagnostic warning = new ScriptDiagnostic("SSL2001", ScriptDiagnosticSeverity.Warning, "k", Array.Empty<String>(), 10, 5, 1, 11);
			ScriptDiagnostic error = new ScriptDiagnostic("SSL1001", ScriptDiagnosticSeverity.Error, "k", Array.Empty<String>(), 12, 2, 1, 13) { Fix = new ScriptQuickFix(ScriptQuickFix.RenameId, "x", 12, 2) };
			ScriptDiagnostic[] all = { warning, error };

			Assert.Same(error, ScriptDiagnostics.FindAt(all, 13));
			Assert.Same(warning, ScriptDiagnostics.FindAt(all, 10));
			Assert.Same(error, ScriptDiagnostics.FindAt(all, 12, true));
			Assert.Null(ScriptDiagnostics.FindAt(all, 10, true));
			Assert.Null(ScriptDiagnostics.FindAt(all, 99));
		}

		[Fact]
		public void Every_rule_has_a_unique_code_and_a_name_that_is_part_of_the_keys()
		{
			Assert.Equal(ScriptDiagnosticCodes.All.Count, ScriptDiagnosticCodes.All.Select(r => r.Code).Distinct().Count());
			Assert.Equal(ScriptDiagnosticCodes.All.Count, ScriptDiagnosticCodes.All.Select(r => r.Name).Distinct().Count());
			Assert.All(ScriptDiagnosticCodes.All, rule =>
			{
				Assert.Equal("Str.Editor.Diag." + rule.Name, rule.MessageKey);
				Assert.Equal(rule, ScriptDiagnosticCodes.Find(rule.Code));
			});
			Assert.Null(ScriptDiagnosticCodes.Find("SSL0"));
			Assert.All(ScriptDiagnosticCodes.All.Where(r => r.Code.StartsWith("SSL2", StringComparison.Ordinal)), rule => Assert.True(rule.Severity < ScriptDiagnosticSeverity.Error));
		}
	}
}