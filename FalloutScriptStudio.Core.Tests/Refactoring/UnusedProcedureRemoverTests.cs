namespace FalloutScriptStudio.Core.Tests.Refactoring
{
	using System;
	using FalloutScriptStudio.Core.Analysis;
	using FalloutScriptStudio.Core.Refactoring;
	using FalloutScriptStudio.Core.Tests.Analysis;
	using FalloutScriptStudio.Core.TextTools;
	using Xunit;

	public class UnusedProcedureRemoverTests
	{
		private static String Apply(String text, String name)
		{
			ProcedureRemovalPlan? plan = UnusedProcedureRemover.Plan(text, name);
			Assert.NotNull(plan);
			return TextEditApplier.Apply(text, plan!.Edits);
		}

		private static readonly String Script = Ssl.Crlf(
			"#include \"define.h\"",
			"",
			"procedure start;",
			"procedure helper(variable a);",
			"procedure other;",
			"",
			"procedure start begin",
			"\tother();",
			"end",
			"",
			"// Helps with a.",
			"// Returns a.",
			"procedure helper(variable a) begin",
			"\treturn a;",
			"end",
			"",
			"procedure other begin",
			"end");

		[Fact]
		public void Plan_RemovesTheProcedureItsCommentAndItsForwardDeclaration()
		{
			ProcedureRemovalPlan? plan = UnusedProcedureRemover.Plan(Script, "helper");

			Assert.NotNull(plan);
			Assert.Equal("helper", plan!.Name);
			Assert.Equal(11, plan.FirstLine);
			Assert.Equal(15, plan.LastLine);
			Assert.Equal(Ssl.Crlf("// Helps with a.", "// Returns a.", "procedure helper(variable a) begin", "\treturn a;", "end"), plan.PreviewText);
			Assert.Equal("procedure helper(variable a);", plan.DeclarationText);
			Assert.False(plan.IsExported);

			Assert.Equal(
				Ssl.Crlf(
					"#include \"define.h\"",
					"",
					"procedure start;",
					"procedure other;",
					"",
					"procedure start begin",
					"\tother();",
					"end",
					"",
					"procedure other begin",
					"end"),
				TextEditApplier.Apply(Script, plan.Edits));
		}

		[Fact]
		public void Plan_TheProcedureIsFound_WhateverTheCase()
		{
			Assert.NotNull(UnusedProcedureRemover.Plan(Script, "HELPER"));
		}

		[Fact]
		public void Plan_ReturnsNothingForAnUnknownProcedureOrOneWithoutABody()
		{
			Assert.Null(UnusedProcedureRemover.Plan(Script, "missing"));
			Assert.Null(UnusedProcedureRemover.Plan(Ssl.Crlf("procedure only;", "procedure start begin", "end"), "only"));
		}

		[Fact]
		public void Plan_TheLastProcedureOfTheFileTakesTheBlankLineBeforeIt()
		{
			String result = Apply(Script, "other");

			Assert.Equal(
				Ssl.Crlf(
					"#include \"define.h\"",
					"",
					"procedure start;",
					"procedure helper(variable a);",
					"",
					"procedure start begin",
					"\tother();",
					"end",
					"",
					"// Helps with a.",
					"// Returns a.",
					"procedure helper(variable a) begin",
					"\treturn a;",
					"end"),
				result);
		}

		[Fact]
		public void Plan_AProcedureAtTheTopOfTheFileTakesTheBlankLineAfterIt()
		{
			String text = Ssl.Crlf("procedure first begin", "end", "", "procedure second begin", "end");

			Assert.Equal(Ssl.Crlf("procedure second begin", "end"), Apply(text, "first"));
		}

		[Fact]
		public void Plan_ABlockCommentIsPartOfTheProcedure()
		{
			String text = Ssl.Crlf("procedure a begin", "end", "", "/** Does b.", " * Details.", " **/", "procedure b begin", "end", "", "procedure c begin", "end");

			ProcedureRemovalPlan? plan = UnusedProcedureRemover.Plan(text, "b");

			Assert.Equal(4, plan!.FirstLine);
			Assert.Equal(Ssl.Crlf("procedure a begin", "end", "", "procedure c begin", "end"), TextEditApplier.Apply(text, plan.Edits));
		}

		[Fact]
		public void Plan_ASectionSeparatorIsNotPartOfTheProcedure()
		{
			String text = Ssl.Crlf("/* ===== bundled ===== */", "procedure a begin", "end", "", "procedure b begin", "end");

			ProcedureRemovalPlan? plan = UnusedProcedureRemover.Plan(text, "a");

			Assert.Equal(2, plan!.FirstLine);
			Assert.Equal(Ssl.Crlf("/* ===== bundled ===== */", "", "procedure b begin", "end"), TextEditApplier.Apply(text, plan.Edits));
		}

		[Fact]
		public void Plan_ACommentSeparatedByABlankLineStays()
		{
			String text = Ssl.Crlf("// About the file", "", "procedure a begin", "end", "procedure b begin", "end");

			Assert.Equal(Ssl.Crlf("// About the file", "", "procedure b begin", "end"), Apply(text, "a"));
		}

		[Fact]
		public void Plan_WorksWithLineFeedsAndWithoutAFinalLineBreak()
		{
			String text = Ssl.Lf("procedure a begin", "end", "", "procedure b begin", "end");

			Assert.Equal(Ssl.Lf("procedure a begin", "end"), Apply(text, "b"));
		}

		[Fact]
		public void Plan_AnExportedProcedureIsMarked()
		{
			String text = Ssl.Crlf("export procedure api begin", "end");

			Assert.True(UnusedProcedureRemover.Plan(text, "api")!.IsExported);
		}

		[Fact]
		public void Plan_TheBodyMayContainKeywordsInStringsAndComments()
		{
			String text = Ssl.Crlf("procedure a begin", "\t// end", "\tdisplay_msg(\"end of procedure\");", "end", "", "procedure b begin", "end");

			Assert.Equal(Ssl.Crlf("procedure b begin", "end"), Apply(text, "a"));
		}

		[Theory]
		[InlineData("start", true)]
		[InlineData("Start", true)]
		[InlineData("look_at_p_proc", true)]
		[InlineData("custom_p_proc", true)]
		[InlineData("map_enter_p_proc", true)]
		[InlineData("Node001", false)]
		[InlineData("helper", false)]
		public void IsEngineHandler_KnowsTheHandlersOfTheGame(String name, Boolean expected)
		{
			Assert.Equal(expected, UnusedProcedureRemover.IsEngineHandler(name));
		}

		[Fact]
		public void IsEngineHandler_UsesTheNamesOfProcHandlersIni()
		{
			String[] names = { "Node999", "Example(arg1, arg2)" };

			Assert.True(UnusedProcedureRemover.IsEngineHandler("node999", names));
			Assert.True(UnusedProcedureRemover.IsEngineHandler("Example", names));
			Assert.False(UnusedProcedureRemover.IsEngineHandler("Node998", names));
		}

		[Fact]
		public void IsUsage_ADeclarationIsNotAUse()
		{
			Assert.False(UnusedProcedureRemover.IsUsage(new SymbolOccurrence("a", 0, 1, 1, 1, SymbolOccurrenceKind.Declaration)));
			Assert.True(UnusedProcedureRemover.IsUsage(new SymbolOccurrence("a", 0, 1, 1, 1, SymbolOccurrenceKind.Reference)));
			Assert.True(UnusedProcedureRemover.IsUsage(new SymbolOccurrence("a", 0, 1, 1, 1, SymbolOccurrenceKind.StringLiteral)));
		}
	}
}
