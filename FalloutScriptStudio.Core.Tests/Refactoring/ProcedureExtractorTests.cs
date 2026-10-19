namespace FalloutScriptStudio.Core.Tests.Refactoring
{
	using System;
	using System.Linq;
	using FalloutScriptStudio.Core.Refactoring;
	using FalloutScriptStudio.Core.Tests.Analysis;
	using Xunit;

	public class ProcedureExtractorTests
	{
		private static (Int32 Start, Int32 Length) Select(String text, String from, String to)
		{
			Int32 start = text.IndexOf(from, StringComparison.Ordinal);
			Int32 end = text.IndexOf(to, start, StringComparison.Ordinal) + to.Length;
			Assert.True(start >= 0 && end > start, "selection not found");
			return (start, end - start);
		}

		private static ExtractAnalysis Analyze(String text, String from, String to)
		{
			(Int32 start, Int32 length) = Select(text, from, to);
			return ProcedureExtractor.Analyze(text, start, length);
		}

		private static ExtractResult Extract(String text, String from, String to, String declaration, String indent = "\t")
		{
			ExtractAnalysis analysis = Analyze(text, from, to);
			Assert.True(analysis.IsValid, analysis.Failure.ToString());
			return ProcedureExtractor.CreateEdits(text, analysis, declaration, indent);
		}

		[Fact]
		public void Extract_MovesTheBlockIntoAProcedureBeforeTheCurrentOne()
		{
			String text = Ssl.Crlf(
				"procedure Foo(variable a) begin",
				"\tdebug_msg(\"start\");",
				"\tdebug_msg(a);",
				"\tdebug_msg(a + 1);",
				"\tdebug_msg(\"done\");",
				"end");

			ExtractResult result = Extract(text, "debug_msg(a);", "debug_msg(a + 1);", "Helper(a)");

			Assert.Equal(
				Ssl.Crlf(
					"procedure Helper(variable a) begin",
					"\tdebug_msg(a);",
					"\tdebug_msg(a + 1);",
					"end",
					"",
					"procedure Foo(variable a) begin",
					"\tdebug_msg(\"start\");",
					"\tcall Helper(a);",
					"\tdebug_msg(\"done\");",
					"end"),
				result.NewText);
			Assert.Equal(1, result.ProcedureLine);
			Assert.Equal(8, result.CallLine);
		}

		[Fact]
		public void Extract_AProcedureWithoutParametersIsCalledWithEmptyParentheses()
		{
			String text = Ssl.Crlf("procedure Foo begin", "\tdisplay_msg(\"a\");", "\tdisplay_msg(\"b\");", "end");

			ExtractResult result = Extract(text, "display_msg(\"a\");", "display_msg(\"a\");", "Helper");

			Assert.Equal(
				Ssl.Crlf(
					"procedure Helper begin",
					"\tdisplay_msg(\"a\");",
					"end",
					"",
					"procedure Foo begin",
					"\tcall Helper();",
					"\tdisplay_msg(\"b\");",
					"end"),
				result.NewText);
		}

		[Fact]
		public void Extract_TheNewProcedureComesBeforeTheCommentOfTheCurrentProcedureAndAfterTheBlankLine()
		{
			String text = Ssl.Crlf(
				"procedure Other begin",
				"end",
				"",
				"// Does the work.",
				"procedure Foo begin",
				"\tdisplay_msg(\"a\");",
				"end");

			ExtractResult result = Extract(text, "display_msg(\"a\");", "display_msg(\"a\");", "Helper");

			Assert.Equal(
				Ssl.Crlf(
					"procedure Other begin",
					"end",
					"",
					"procedure Helper begin",
					"\tdisplay_msg(\"a\");",
					"end",
					"",
					"// Does the work.",
					"procedure Foo begin",
					"\tcall Helper();",
					"end"),
				result.NewText);
			Assert.Equal(4, result.ProcedureLine);
		}

		[Fact]
		public void Extract_AlignsTheBodyToTheIndentationUnit_WhateverTheOriginalIndentation()
		{
			String text = Ssl.Crlf(
				"procedure Foo begin",
				"\t\tif (1) then begin",
				"\t\t\tdisplay_msg(\"a\");",
				"\t\t\tdisplay_msg(\"b\");",
				"\t\tend",
				"end");

			ExtractResult result = Extract(text, "if (1)", "\t\tend", "Helper", "    ");

			Assert.Equal(
				Ssl.Crlf(
					"procedure Helper begin",
					"    if (1) then begin",
					"    \tdisplay_msg(\"a\");",
					"    \tdisplay_msg(\"b\");",
					"    end",
					"end",
					"",
					"procedure Foo begin",
					"\t\tcall Helper();",
					"end"),
				result.NewText);
		}

		[Fact]
		public void Extract_WorksWithLineFeedsAndSpaces()
		{
			String text = Ssl.Lf("procedure Foo(variable a) begin", "    a = 1;", "    debug_msg(a);", "    debug_msg(2);", "end");

			ExtractResult result = Extract(text, "debug_msg(a);", "debug_msg(2);", "Helper(a)", "    ");

			Assert.Equal(
				Ssl.Lf(
					"procedure Helper(variable a) begin",
					"    debug_msg(a);",
					"    debug_msg(2);",
					"end",
					"",
					"procedure Foo(variable a) begin",
					"    a = 1;",
					"    call Helper(a);",
					"end"),
				result.NewText);
		}

		[Fact]
		public void Extract_ALocalVariableOnlyTheBlockUsesMovesIntoTheNewProcedure()
		{
			String text = Ssl.Crlf(
				"procedure Foo(variable a) begin",
				"\tvariable b, c;",
				"\tc = 1;",
				"\tb = a + 1;",
				"\tdebug_msg(b);",
				"\tdebug_msg(c);",
				"end");

			ExtractResult result = Extract(text, "b = a + 1;", "debug_msg(b);", "Helper(a)");

			Assert.Equal(
				Ssl.Crlf(
					"procedure Helper(variable a) begin",
					"\tvariable b;",
					"",
					"\tb = a + 1;",
					"\tdebug_msg(b);",
					"end",
					"",
					"procedure Foo(variable a) begin",
					"\tvariable c;",
					"\tc = 1;",
					"\tcall Helper(a);",
					"\tdebug_msg(c);",
					"end"),
				result.NewText);
		}

		[Fact]
		public void Extract_ALocalWithAnInitializerIsPassedAsAParameter()
		{
			String text = Ssl.Crlf("procedure Foo begin", "\tvariable b = 5;", "\tdebug_msg(b);", "end");

			ExtractAnalysis analysis = Analyze(text, "debug_msg(b);", "debug_msg(b);");

			Assert.True(analysis.IsValid);
			Assert.Equal(new[] { "b" }, analysis.Parameters.ToArray());
			Assert.Empty(analysis.MovedLocals);
		}

		[Fact]
		public void Extract_AVariableDeclaredInTheBlockStaysThere()
		{
			String text = Ssl.Crlf("procedure Foo begin", "\tvariable x = 1;", "\tvariable t = x + 1;", "\tdebug_msg(t);", "\tdebug_msg(x);", "end");

			ExtractAnalysis analysis = Analyze(text, "variable t", "debug_msg(t);");

			Assert.True(analysis.IsValid);
			Assert.Equal(new[] { "x" }, analysis.Parameters.ToArray());
			Assert.Empty(analysis.MovedLocals);
		}

		[Fact]
		public void Extract_AVariableDeclaredInTheBlockAndUsedAfterIsRefused()
		{
			String text = Ssl.Crlf("procedure Foo begin", "\tvariable t = 1;", "\tdebug_msg(t);", "end");

			ExtractAnalysis analysis = Analyze(text, "variable t", "t = 1;");

			Assert.Equal(ExtractFailure.DeclaredVariableUsedAfter, analysis.Failure);
			Assert.Equal("t", analysis.Detail);
		}

		[Fact]
		public void Extract_AVariableTheBlockChangesAndTheRestReadsIsReturned()
		{
			String text = Ssl.Crlf(
				"procedure Foo(variable a) begin",
				"\tvariable total = 0;",
				"\ttotal = total + a;",
				"\tdebug_msg(total);",
				"end");

			ExtractResult result = Extract(text, "total = total + a;", "total = total + a;", "Helper(total, a)");

			Assert.Equal(
				Ssl.Crlf(
					"procedure Helper(variable total, variable a) begin",
					"\ttotal = total + a;",
					"\treturn total;",
					"end",
					"",
					"procedure Foo(variable a) begin",
					"\tvariable total = 0;",
					"\ttotal = Helper(total, a);",
					"\tdebug_msg(total);",
					"end"),
				result.NewText);
		}

		[Fact]
		public void Extract_AVariableThatIsOnlyAssignedInTheBlockIsDeclaredInTheNewProcedure()
		{
			String text = Ssl.Crlf(
				"procedure Foo(variable a) begin",
				"\tvariable result = 0;",
				"\tresult = a * 2;",
				"\tdebug_msg(result);",
				"end");

			ExtractResult result = Extract(text, "result = a * 2;", "result = a * 2;", "Helper(a)");

			Assert.Equal(
				Ssl.Crlf(
					"procedure Helper(variable a) begin",
					"\tvariable result;",
					"",
					"\tresult = a * 2;",
					"\treturn result;",
					"end",
					"",
					"procedure Foo(variable a) begin",
					"\tvariable result = 0;",
					"\tresult = Helper(a);",
					"\tdebug_msg(result);",
					"end"),
				result.NewText);
		}

		[Fact]
		public void Extract_TheCallUsesTheAssignmentOperatorOfTheScript()
		{
			String text = Ssl.Crlf(
				"procedure Foo(variable a) begin",
				"\tvariable total := 0;",
				"\ttotal := total + a;",
				"\tdebug_msg(total);",
				"end");

			ExtractResult result = Extract(text, "total := total + a;", "total := total + a;", "Helper(total, a)");

			Assert.Contains("\ttotal := Helper(total, a);", result.NewText, StringComparison.Ordinal);
		}

		[Fact]
		public void Extract_SeveralVariablesChangedAndReadAfterwardsAreRefused()
		{
			String text = Ssl.Crlf(
				"procedure Foo begin",
				"\tvariable a = 0, b = 0;",
				"\ta = 1;",
				"\tb = 2;",
				"\tdebug_msg(a + b);",
				"end");

			ExtractAnalysis analysis = Analyze(text, "a = 1;", "b = 2;");

			Assert.Equal(ExtractFailure.TooManyResults, analysis.Failure);
		}

		[Fact]
		public void Extract_AVariableAssignedBeforeBeingReadAfterwardsIsNotReturned()
		{
			String text = Ssl.Crlf("procedure Foo begin", "\tvariable a = 0;", "\ta = 1;", "\ta = 2;", "\tdebug_msg(a);", "end");

			ExtractAnalysis analysis = Analyze(text, "a = 1;", "a = 1;");

			Assert.True(analysis.IsValid);
			Assert.Null(analysis.ResultVariable);
		}

		[Fact]
		public void Extract_AVariableChangedInALoopThatContainsTheBlockIsReturned()
		{
			String text = Ssl.Crlf(
				"procedure Foo begin",
				"\tvariable i = 0, count = 0;",
				"\twhile (count < 5) do begin",
				"\t\tcount++;",
				"\t\tdebug_msg(i);",
				"\tend",
				"end");

			ExtractAnalysis analysis = Analyze(text, "count++;", "count++;");

			Assert.True(analysis.IsValid);
			Assert.Equal("count", analysis.ResultVariable);
			Assert.Equal(new[] { "count" }, analysis.Parameters.ToArray());
		}

		[Fact]
		public void Extract_ScriptVariablesAndMacrosAreNotParameters()
		{
			String text = Ssl.Crlf("variable counter;", "#define LIMIT 5", "procedure Foo begin", "\tcounter = LIMIT;", "\tdebug_msg(counter);", "end");

			ExtractAnalysis analysis = Analyze(text, "counter = LIMIT;", "debug_msg(counter);");

			Assert.True(analysis.IsValid);
			Assert.Empty(analysis.Parameters);
		}

		[Fact]
		public void Extract_AShadowingLocalIsAParameterNotAGlobal()
		{
			String text = Ssl.Crlf("variable v;", "procedure Foo begin", "\tvariable v = 1;", "\tdebug_msg(v);", "end");

			ExtractAnalysis analysis = Analyze(text, "debug_msg(v);", "debug_msg(v);");

			Assert.Equal(new[] { "v" }, analysis.Parameters.ToArray());
		}

		[Fact]
		public void Extract_ParametersFollowTheFirstUseInTheBlock()
		{
			String text = Ssl.Crlf("procedure Foo(variable p, variable q, variable r) begin", "\tdebug_msg(r);", "\tdebug_msg(p + q + r);", "end");

			ExtractAnalysis analysis = Analyze(text, "debug_msg(r);", "debug_msg(p + q + r);");

			Assert.Equal(new[] { "r", "p", "q" }, analysis.Parameters.ToArray());
		}

		[Fact]
		public void Extract_AcceptsTheParametersInAnyOrder_ButNoOtherList()
		{
			String text = Ssl.Crlf("procedure Foo(variable p, variable q) begin", "\tdebug_msg(p + q);", "end");
			ExtractAnalysis analysis = Analyze(text, "debug_msg(p + q);", "debug_msg(p + q);");

			Assert.True(analysis.MatchesParameters(new[] { "q", "P" }));
			Assert.False(analysis.MatchesParameters(new[] { "p" }));
			Assert.False(analysis.MatchesParameters(new[] { "p", "q", "r" }));
			Assert.False(analysis.MatchesParameters(new[] { "p", "p" }));
			ExtractResult result = ProcedureExtractor.CreateEdits(text, analysis, "Helper(q, p)", "\t");
			Assert.Contains("procedure Helper(variable q, variable p) begin", result.NewText, StringComparison.Ordinal);
			Assert.Contains("\tcall Helper(q, p);", result.NewText, StringComparison.Ordinal);
			Assert.Throws<ArgumentException>(() => ProcedureExtractor.CreateEdits(text, analysis, "Helper(p)", "\t"));
			Assert.Throws<ArgumentException>(() => ProcedureExtractor.CreateEdits(text, analysis, "not valid", "\t"));
		}

		[Fact]
		public void Extract_NestedBlocksCommentsAndStringsWithKeywordsAreCarriedAlong()
		{
			String text = Ssl.Crlf(
				"procedure Foo(variable a) begin",
				"\tif (a) then begin",
				"\t\t// the end of it",
				"\t\tdebug_msg(\"begin end\");",
				"\t\twhile (a > 0) do begin",
				"\t\t\ta--;",
				"\t\tend",
				"\tend else begin",
				"\t\tdebug_msg(\"else\");",
				"\tend",
				"\tdebug_msg(\"after\");",
				"end");

			ExtractAnalysis analysis = Analyze(text, "if (a) then begin", "\tend else begin\r\n\t\tdebug_msg(\"else\");\r\n\tend");

			Assert.True(analysis.IsValid, analysis.Failure.ToString());
			Assert.Equal(new[] { "a" }, analysis.Parameters.ToArray());
		}

		[Fact]
		public void Extract_BreakAndContinueInsideALoopOfTheBlockAreFine()
		{
			String text = Ssl.Crlf(
				"procedure Foo(variable n) begin",
				"\twhile (n > 0) do begin",
				"\t\tn--;",
				"\t\tif (n == 3) then break;",
				"\t\tcontinue;",
				"\tend",
				"\tdebug_msg(1);",
				"end");

			ExtractAnalysis analysis = Analyze(text, "while (n > 0)", "\tend");

			Assert.True(analysis.IsValid, analysis.Failure.ToString());
		}

		[Fact]
		public void Extract_BreakOfALoopOutsideTheBlockIsRefused()
		{
			String text = Ssl.Crlf(
				"procedure Foo(variable n) begin",
				"\twhile (n > 0) do begin",
				"\t\tn--;",
				"\t\tif (n == 3) then break;",
				"\tend",
				"end");

			ExtractAnalysis analysis = Analyze(text, "if (n == 3) then break;", "break;");

			Assert.Equal(ExtractFailure.ContainsLoopControl, analysis.Failure);
		}

		[Fact]
		public void Extract_ContinueInASwitchOfTheBlockIsRefused_ButBreakIsFine()
		{
			String withContinue = Ssl.Crlf("procedure Foo(variable n) begin", "\tswitch (n) begin", "\t\tcase 1: continue;", "\tend", "end");
			String withBreak = Ssl.Crlf("procedure Foo(variable n) begin", "\tswitch (n) begin", "\t\tcase 1: break;", "\tend", "end");

			Assert.Equal(ExtractFailure.ContainsLoopControl, Analyze(withContinue, "switch", "\tend").Failure);
			Assert.True(Analyze(withBreak, "switch", "\tend").IsValid);
		}

		[Fact]
		public void Extract_ReturnIsRefused()
		{
			String text = Ssl.Crlf("procedure Foo(variable a) begin", "\tif (a) then begin", "\t\treturn 1;", "\tend", "\tdebug_msg(1);", "end");

			Assert.Equal(ExtractFailure.ContainsReturn, Analyze(text, "if (a)", "\tend").Failure);
		}

		[Fact]
		public void Extract_UnbalancedBeginEndIsRefused()
		{
			String text = Ssl.Crlf("procedure Foo(variable a) begin", "\tif (a) then begin", "\t\tdebug_msg(1);", "\tend", "end");

			Assert.Equal(ExtractFailure.UnbalancedBlock, Analyze(text, "if (a)", "debug_msg(1);").Failure);
			Assert.Equal(ExtractFailure.UnbalancedBlock, Analyze(text, "debug_msg(1);", "\tend").Failure);
			Assert.Equal(ExtractFailure.UnbalancedBlock, Analyze(text, "debug_msg(", "1").Failure);
		}

		[Theory]
		[InlineData("debug_msg(1)")]
		[InlineData("(1);")]
		[InlineData("msg(1); debug_msg(")]
		public void Extract_APartialStatementIsRefused(String selection)
		{
			String text = Ssl.Crlf("procedure Foo(variable a) begin", "\tdebug_msg(1);", "\tdebug_msg(2);", "\tmsg(1); debug_msg(3);", "end");
			(Int32 start, Int32 length) = (text.IndexOf(selection, StringComparison.Ordinal), selection.Length);

			ExtractFailure failure = ProcedureExtractor.Analyze(text, start, length).Failure;

			Assert.True(failure is ExtractFailure.PartialStatement or ExtractFailure.UnbalancedBlock, failure.ToString());
		}

		[Fact]
		public void Extract_ASelectionThatStartsInsideAWordIsRefused()
		{
			String text = Ssl.Crlf("procedure Foo begin", "\tdebug_msg(1);", "end");
			Int32 start = text.IndexOf("debug_msg", StringComparison.Ordinal) + 2;

			Assert.Equal(ExtractFailure.PartialStatement, ProcedureExtractor.Analyze(text, start, text.IndexOf(");", StringComparison.Ordinal) + 2 - start).Failure);
		}

		[Fact]
		public void Extract_TheThenPartOfAnIfWithElseIsRefused()
		{
			String text = Ssl.Crlf("procedure Foo(variable a) begin", "\tif (a) then debug_msg(1); else debug_msg(2);", "end");

			Assert.Equal(ExtractFailure.PartialStatement, Analyze(text, "debug_msg(1);", "debug_msg(1);").Failure);
			Assert.Equal(ExtractFailure.PartialStatement, Analyze(text, "else debug_msg(2);", "debug_msg(2);").Failure);
			Assert.True(Analyze(text, "if (a)", "debug_msg(2);").IsValid);
		}

		[Fact]
		public void Extract_TwoStatementsAfterThenWithoutBeginAreRefused()
		{
			String text = Ssl.Crlf("procedure Foo(variable a) begin", "\tif (a) then debug_msg(1); debug_msg(2);", "end");

			Assert.Equal(ExtractFailure.PartialStatement, Analyze(text, "debug_msg(1);", "debug_msg(2);").Failure);
			Assert.True(Analyze(text, "debug_msg(1);", "debug_msg(1);").IsValid);
		}

		[Fact]
		public void Extract_ACaseLabelIsRefused_ButTheWholeSwitchIsFine()
		{
			String text = Ssl.Crlf(
				"procedure Foo(variable a) begin",
				"\tswitch (a) begin",
				"\t\tcase 1: debug_msg(1);",
				"\t\tdefault: debug_msg(2);",
				"\tend",
				"end");

			Assert.Equal(ExtractFailure.PartialStatement, Analyze(text, "case 1: debug_msg(1);", "debug_msg(2);").Failure);
			Assert.True(Analyze(text, "switch (a)", "\tend").IsValid);
		}

		[Fact]
		public void Extract_OutsideAProcedureIsRefused()
		{
			String text = Ssl.Crlf("variable a;", "procedure Foo begin", "end");

			Assert.Equal(ExtractFailure.NotInsideProcedure, Analyze(text, "variable a;", "variable a;").Failure);
		}

		[Fact]
		public void Extract_TheWholeBodyIncludingBeginOrEndIsRefused()
		{
			String text = Ssl.Crlf("procedure Foo begin", "\tdebug_msg(1);", "end");

			Assert.Equal(ExtractFailure.NotInsideProcedure, Analyze(text, "begin", "end").Failure);
		}

		[Fact]
		public void Extract_APreprocessorLineInTheSelectionIsRefused()
		{
			String text = Ssl.Crlf("procedure Foo begin", "\tdebug_msg(1);", "#define X 1", "\tdebug_msg(2);", "end");

			Assert.Equal(ExtractFailure.ContainsPreprocessorLine, Analyze(text, "debug_msg(1);", "debug_msg(2);").Failure);
		}

		[Theory]
		[InlineData(0, 0)]
		[InlineData(-1, 3)]
		[InlineData(5, 100000)]
		public void Extract_AnEmptyOrInvalidSelectionIsRefused(Int32 start, Int32 length)
		{
			Assert.Equal(ExtractFailure.EmptySelection, ProcedureExtractor.Analyze("procedure a begin end", start, length).Failure);
		}

		[Fact]
		public void Extract_ASelectionOfOnlyWhitespaceOrCommentsIsRefused()
		{
			String text = Ssl.Crlf("procedure Foo begin", "\t// nothing", "\t", "end");
			Int32 start = text.IndexOf("//", StringComparison.Ordinal);

			Assert.Equal(ExtractFailure.EmptySelection, ProcedureExtractor.Analyze(text, start, 10).Failure);
		}

		[Fact]
		public void Extract_ATrailingCommentOfTheLastLineStaysWhereItIs()
		{
			String text = Ssl.Crlf("procedure Foo begin", "\tdebug_msg(1); // one", "\tdebug_msg(2);", "end");

			ExtractResult result = Extract(text, "debug_msg(1);", "debug_msg(1);", "Helper");

			Assert.Contains("\tcall Helper(); // one", result.NewText, StringComparison.Ordinal);
		}

		[Fact]
		public void Extract_WhenTheNewProcedureDeclarationHasAnEmptyParameterList_TheCallHasEmptyParentheses()
		{
			String text = Ssl.Crlf("procedure Foo begin", "\tdebug_msg(1);", "end");

			ExtractResult result = Extract(text, "debug_msg(1);", "debug_msg(1);", "Helper()");

			Assert.Contains("procedure Helper() begin", result.NewText, StringComparison.Ordinal);
			Assert.Contains("\tcall Helper();", result.NewText, StringComparison.Ordinal);
		}

		[Fact]
		public void Extract_ProducesNonOverlappingEditsThatMatchTheNewText()
		{
			String text = Ssl.Crlf(
				"procedure Foo(variable a) begin",
				"\tvariable b, c;",
				"\tb = a;",
				"\tdebug_msg(b);",
				"\tc = 1;",
				"\tdebug_msg(c);",
				"end");

			ExtractResult result = Extract(text, "b = a;", "debug_msg(b);", "Helper(a)");

			Assert.Equal(result.NewText, FalloutScriptStudio.Core.TextTools.TextEditApplier.Apply(text, result.Edits));
			Assert.True(result.Edits.Count >= 3);
		}

		[Fact]
		public void Extract_AllLocalsOfADeclarationMoveWhenAllAreOnlyUsedInTheBlock()
		{
			String text = Ssl.Crlf(
				"procedure Foo begin",
				"\tvariable b, c;",
				"\tb = 1;",
				"\tc = 2;",
				"\tdebug_msg(b + c);",
				"end");

			ExtractResult result = Extract(text, "b = 1;", "debug_msg(b + c);", "Helper");

			Assert.Equal(
				Ssl.Crlf(
					"procedure Helper begin",
					"\tvariable b, c;",
					"",
					"\tb = 1;",
					"\tc = 2;",
					"\tdebug_msg(b + c);",
					"end",
					"",
					"procedure Foo begin",
					"\tcall Helper();",
					"end"),
				result.NewText);
		}

		[Fact]
		public void Extract_TheSecondProcedureOfAFileIsHandled()
		{
			String text = Ssl.Crlf(
				"procedure start begin",
				"\tdebug_msg(0);",
				"end",
				"",
				"procedure Foo(variable a) begin",
				"\tdebug_msg(a);",
				"end");

			ExtractResult result = Extract(text, "debug_msg(a);", "debug_msg(a);", "Helper(a)");

			Assert.Equal(
				Ssl.Crlf(
					"procedure start begin",
					"\tdebug_msg(0);",
					"end",
					"",
					"procedure Helper(variable a) begin",
					"\tdebug_msg(a);",
					"end",
					"",
					"procedure Foo(variable a) begin",
					"\tcall Helper(a);",
					"end"),
				result.NewText);
			Assert.Equal(5, result.ProcedureLine);
		}
	}
}