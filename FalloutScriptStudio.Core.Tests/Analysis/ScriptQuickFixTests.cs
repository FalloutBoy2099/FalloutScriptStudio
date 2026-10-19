namespace FalloutScriptStudio.Core.Tests.Analysis
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using FalloutScriptStudio.Core.Analysis;
	using FalloutScriptStudio.Core.TextTools;
	using FalloutScriptStudio.TestSupport;
	using Xunit;

	public class ScriptQuickFixTests
	{
		private static String Apply(String text, String code, out ScriptDiagnostic diagnostic)
		{
			diagnostic = Assert.Single(DiagnosticsFixture.Analyze(text), d => d.Code == code);
			Assert.NotNull(diagnostic.Fix);
			IReadOnlyList<TextEdit> edits = ScriptQuickFixes.CreateEdits(text, diagnostic.Fix!);
			Assert.NotEmpty(edits);
			return TextEditApplier.Apply(text, edits);
		}

		private static void AssertClean(String text)
		{
			Assert.DoesNotContain(DiagnosticsFixture.Analyze(text), d => d.Severity == ScriptDiagnosticSeverity.Error);
		}

		[Fact]
		public void A_rename_replaces_the_misspelled_name()
		{
			String text = Ssl.Crlf("procedure start begin", "\tdisplay_msgg(\"a\");", "end");

			String fixedText = Apply(text, "SSL1002", out _);

			Assert.Equal(Ssl.Crlf("procedure start begin", "\tdisplay_msg(\"a\");", "end"), fixedText);
			AssertClean(fixedText);
		}

		[Fact]
		public void The_forward_declaration_is_inserted_in_front_of_the_first_procedure_with_the_line_breaks_of_the_script()
		{
			String crlf = Ssl.Crlf("variable g;", "procedure start begin", "\tcall late(1, 2);", "end", "procedure late(variable a, variable b) begin end");
			String lf = Ssl.Lf("variable g;", "procedure start begin", "\tcall late(1, 2);", "end", "procedure late(variable a, variable b) begin end");

			String fixedCrlf = Apply(crlf, "SSL1004", out _);
			String fixedLf = Apply(lf, "SSL1004", out _);

			Assert.Equal(Ssl.Crlf("variable g;", "procedure late(variable a, variable b);", "procedure start begin", "\tcall late(1, 2);", "end", "procedure late(variable a, variable b) begin end"), fixedCrlf);
			Assert.Equal(Ssl.Lf("variable g;", "procedure late(variable a, variable b);", "procedure start begin", "\tcall late(1, 2);", "end", "procedure late(variable a, variable b) begin end"), fixedLf);
			AssertClean(fixedCrlf);
			AssertClean(fixedLf);
		}

		[Fact]
		public void A_duplicate_variable_is_removed_alone_or_from_a_list()
		{
			String alone = Ssl.Crlf("procedure start begin", "\tvariable a;", "\tvariable a;", "\ta := 1;", "end");
			String listed = Ssl.Crlf("procedure start begin", "\tvariable a;", "\tvariable b, a, c;", "\ta := 1; b := 2; c := 3;", "end");

			String fixedAlone = Apply(alone, "SSL1009", out _);
			String fixedListed = Apply(listed, "SSL1009", out _);

			Assert.Equal(Ssl.Crlf("procedure start begin", "\tvariable a;", "\ta := 1;", "end"), fixedAlone);
			Assert.Equal(Ssl.Crlf("procedure start begin", "\tvariable a;", "\tvariable b, c;", "\ta := 1; b := 2; c := 3;", "end"), fixedListed);
			AssertClean(fixedAlone);
			AssertClean(fixedListed);
		}

		[Fact]
		public void A_forward_declaration_after_the_definition_is_removed_with_its_line()
		{
			String text = Ssl.Crlf("procedure foo begin end", "procedure foo;", "procedure start begin end");

			String fixedText = Apply(text, "SSL1008", out _);

			Assert.Equal(Ssl.Crlf("procedure foo begin end", "procedure start begin end"), fixedText);
			AssertClean(fixedText);
		}

		[Fact]
		public void A_fix_made_for_another_text_is_refused_or_harmless()
		{
			ScriptQuickFix outside = new ScriptQuickFix(ScriptQuickFix.RenameId, "x", 100, 5);
			ScriptQuickFix unknown = new ScriptQuickFix("other", "x", 0, 1);
			ScriptQuickFix empty = new ScriptQuickFix(ScriptQuickFix.RenameId, String.Empty, 0, 1);
			ScriptQuickFix target = new ScriptQuickFix(ScriptQuickFix.RemoveDeclarationId, "nothing", 0, 1);

			Assert.Empty(ScriptQuickFixes.CreateEdits("short", outside));
			Assert.Empty(ScriptQuickFixes.CreateEdits("short", unknown));
			Assert.Empty(ScriptQuickFixes.CreateEdits("short", empty));
			Assert.Empty(ScriptQuickFixes.CreateEdits("short", target));
			Assert.Empty(ScriptQuickFixes.CreateEdits("variable a;", new ScriptQuickFix(ScriptQuickFix.RemoveDeclarationId, ScriptQuickFixes.VariableTarget, 3, 1)));
		}

		[Theory]
		[InlineData("a\r\nb", "\r\n")]
		[InlineData("a\nb", "\n")]
		[InlineData("no line break", "\r\n")]
		[InlineData("\nfirst", "\n")]
		public void The_line_break_of_a_text_is_copied(String text, String expected)
		{
			Assert.Equal(expected, ScriptQuickFixes.GetNewLine(text));
		}

		[Fact]
		public void Every_fix_of_the_table_removes_its_diagnostic_and_adds_no_error()
		{
			Int32 fixes = 0;
			foreach (ScriptDiagnosticCase item in ScriptDiagnosticCase.All.Where(c => c.CompilerRejects))
			{
				foreach (ScriptDiagnostic diagnostic in DiagnosticsFixture.Analyze(item.Crlf).Where(d => d.Fix is not null))
				{
					String fixedText = TextEditApplier.Apply(item.Crlf, ScriptQuickFixes.CreateEdits(item.Crlf, diagnostic.Fix!));

					IReadOnlyList<ScriptDiagnostic> after = DiagnosticsFixture.Analyze(fixedText);

					Assert.True(after.Count(d => d.Code == diagnostic.Code) < DiagnosticsFixture.Analyze(item.Crlf).Count(d => d.Code == diagnostic.Code), item.Name + ": " + diagnostic.Code + " is still reported after the fix");
					fixes++;
				}
			}

			Assert.True(fixes >= 8, "fixes: " + fixes);
		}
	}
}