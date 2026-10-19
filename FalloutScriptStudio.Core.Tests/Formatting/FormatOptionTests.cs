namespace FalloutScriptStudio.Core.Tests.Formatting
{
	using System;
	using FalloutScriptStudio.Core.Formatting;
	using FalloutScriptStudio.Core.TextTools;
	using Xunit;

	/// <summary>One test per formatting option: a text before and after, and the same text formatted again.</summary>
	public sealed class FormatOptionTests
	{
		private static readonly CodeFormatter _formatter = new CodeFormatter();

		private static String Crlf(params String[] lines)
		{
			return String.Join("\r\n", lines) + "\r\n";
		}

		private static void Check(String before, String after, FormatOptions options)
		{
			String formatted = _formatter.Format(before, options);
			Assert.Equal(after, formatted);
			Assert.Equal(after, _formatter.Format(formatted, options));
		}

		[Fact]
		public void Defaults_OnlyPutSpacesAroundOperatorsAndAfterCommas()
		{
			String text = Crlf("procedure start begin", "variable x:=foo(1,2);", "if(x==1)then begin", "x+=1;", "end", "end");

			Assert.Equal(Crlf("procedure start begin", "variable x := foo(1, 2);", "if(x == 1)then begin", "x += 1;", "end", "end"), _formatter.Format(text));
			Assert.False(FormatOptions.Default.IsStructural);
		}

		[Fact]
		public void OperatorSpacing_Remove_DropsTheSpacesAroundBinaryOperators()
		{
			Check(
				Crlf("x := a + b * c - d / e % f;", "if (a == b and c != d) then x := e >= f;"),
				Crlf("x := a+b*c-d/e%f;", "if (a==b and c!=d) then x := e>=f;"),
				new FormatOptions { OperatorSpacing = SpacingStyle.Remove });
		}

		[Fact]
		public void OperatorSpacing_Remove_KeepsUnaryOperatorsAndNeverFusesTwoSymbols()
		{
			Check(
				Crlf("x := - 1;", "y := a - -1;", "z := a + +b;", "w := return_value(-1, - 2);", "v := a - b;"),
				Crlf("x := - 1;", "y := a- -1;", "z := a+ +b;", "w := return_value(-1, - 2);", "v := a-b;"),
				new FormatOptions { OperatorSpacing = SpacingStyle.Remove });
		}

		[Fact]
		public void AssignmentSpacing_Remove_DropsTheSpacesAroundAssignments()
		{
			Check(Crlf("x := 1;", "x += 2;", "y -= x * 2;"), Crlf("x:=1;", "x+=2;", "y-=x * 2;"), new FormatOptions { AssignmentSpacing = SpacingStyle.Remove });
		}

		[Fact]
		public void CommaSpacing_Remove_DropsTheSpacesAroundCommas()
		{
			Check(Crlf("foo(a, b , c);", "variable x, y;"), Crlf("foo(a,b,c);", "variable x,y;"), new FormatOptions { CommaSpacing = SpacingStyle.Remove });
		}

		[Fact]
		public void Spacing_Keep_LeavesTheTextAlone()
		{
			String text = Crlf("x:=a+b , c;", "y := d  *  e;");

			Check(text, text, new FormatOptions { OperatorSpacing = SpacingStyle.Keep, AssignmentSpacing = SpacingStyle.Keep, CommaSpacing = SpacingStyle.Keep });
		}

		[Fact]
		public void Spacing_AddOnlyForTheCategoriesThatAreOn()
		{
			Check(Crlf("x:=a+b,c;"), Crlf("x:=a + b,c;"), new FormatOptions { AssignmentSpacing = SpacingStyle.Keep, CommaSpacing = SpacingStyle.Keep });
		}

		[Fact]
		public void Spacing_NeverTouchesStringsOrComments()
		{
			Check(
				Crlf("x := \"a+b , c:=d\"; // a+b,c", "/* a+b", "   c:=d */ y:=1;"),
				Crlf("x:=\"a+b , c:=d\"; // a+b,c", "/* a+b", "   c:=d */ y:=1;"),
				new FormatOptions { OperatorSpacing = SpacingStyle.Remove, AssignmentSpacing = SpacingStyle.Remove, CommaSpacing = SpacingStyle.Remove });
		}

		[Fact]
		public void FormatPreprocessorLines_Off_LeavesTheDirectivesAlone()
		{
			String text = Crlf("#define A(x) x+1", "#define B 1+2", "y:=A(1)+B;");

			Check(text, Crlf("#define A(x) x+1", "#define B 1+2", "y := A(1) + B;"), new FormatOptions { FormatPreprocessorLines = false });
			Check(text, Crlf("#define A(x) x + 1", "#define B 1 + 2", "y := A(1) + B;"), new FormatOptions());
		}

		[Fact]
		public void IncludeLines_AreNeverTouched()
		{
			Check(Crlf("#include <a/b+c.h>", "x:=1;"), Crlf("#include <a/b+c.h>", "x := 1;"), new FormatOptions());
		}

		[Theory]
		[InlineData(InnerSpacingStyle.None, "foo(a, b);", "foo(a, b);")]
		[InlineData(InnerSpacingStyle.None, "foo( a, b );", "foo(a, b);")]
		[InlineData(InnerSpacingStyle.None, "if (( a ) and ( b )) then", "if ((a) and (b)) then")]
		[InlineData(InnerSpacingStyle.One, "foo(a, b);", "foo( a, b );")]
		[InlineData(InnerSpacingStyle.One, "foo(  a, b  );", "foo( a, b );")]
		[InlineData(InnerSpacingStyle.One, "foo();", "foo();")]
		[InlineData(InnerSpacingStyle.One, "if ((a) and (b)) then", "if ( ( a ) and ( b ) ) then")]
		[InlineData(InnerSpacingStyle.Keep, "foo(  a, b );", "foo(  a, b );")]
		public void ParenthesisSpacing_SetsTheSpaceInsideParentheses(InnerSpacingStyle style, String before, String after)
		{
			Check(Crlf(before), Crlf(after), new FormatOptions { ParenthesisSpacing = style });
		}

		[Fact]
		public void ParenthesisSpacing_LeavesLineBreaksAndCommentsAlone()
		{
			Check(Crlf("foo(", "   a, // first", "   b", ");"), Crlf("foo(", "   a, // first", "   b", ");"), new FormatOptions { ParenthesisSpacing = InnerSpacingStyle.None });
			Check(Crlf("foo(a /* x */ );", "bar( \"(\" );"), Crlf("foo( a /* x */ );", "bar( \"(\" );"), new FormatOptions { ParenthesisSpacing = InnerSpacingStyle.One });
		}

		[Fact]
		public void BeginPlacement_SameLine_JoinsBeginToItsHeader()
		{
			Check(
				Crlf("procedure start", "begin", "   if (x) then", "   begin", "      foo;", "   end", "   while (y) do", "   begin", "   end", "end"),
				Crlf("procedure start begin", "   if (x) then begin", "      foo;", "   end", "   while (y) do begin", "   end", "end"),
				new FormatOptions { Begin = BeginPlacement.SameLine });
		}

		[Fact]
		public void BeginPlacement_SameLine_KeepsBeginAfterAComment()
		{
			String text = Crlf("procedure start // the start", "begin", "end");

			Check(text, text, new FormatOptions { Begin = BeginPlacement.SameLine });
		}

		[Fact]
		public void BeginPlacement_NextLine_MovesBeginUnderItsHeader()
		{
			Check(
				Crlf("procedure start begin", "   if (x) then begin // yes", "      foo;", "   end else begin", "      bar;", "   end", "end"),
				Crlf("procedure start", "begin", "   if (x) then", "   begin // yes", "      foo;", "   end else", "   begin", "      bar;", "   end", "end"),
				new FormatOptions { Begin = BeginPlacement.NextLine });
		}

		[Fact]
		public void BeginPlacement_NextLine_LeavesAOneLineBlockAlone()
		{
			String text = Crlf("procedure start begin", "   if (x) then begin y := 1; end", "end");

			Check(text, Crlf("procedure start", "begin", "   if (x) then begin y := 1; end", "end"), new FormatOptions { Begin = BeginPlacement.NextLine });
		}

		[Fact]
		public void ElseBegin_Join_PutsElseAndBeginOnOneLine()
		{
			Check(
				Crlf("if (x) then begin", "end else", "begin", "end"),
				Crlf("if (x) then begin", "end else begin", "end"),
				new FormatOptions { ElseBegin = ElseBeginStyle.Join });
		}

		[Fact]
		public void ElseBegin_Split_PutsBeginOnTheLineAfterElse()
		{
			Check(
				Crlf("if (x) then begin", "end else begin", "end"),
				Crlf("if (x) then begin", "end else", "begin", "end"),
				new FormatOptions { ElseBegin = ElseBeginStyle.Split });
		}

		[Fact]
		public void ElseBegin_WinsOverBeginPlacementForThatPair()
		{
			Check(
				Crlf("if (x) then", "begin", "end", "else", "begin", "end"),
				Crlf("if (x) then begin", "end", "else begin", "end"),
				new FormatOptions { Begin = BeginPlacement.SameLine, ElseBegin = ElseBeginStyle.Join });
		}

		[Fact]
		public void ElsePlacement_AfterEnd_PutsElseOnTheLineOfEnd()
		{
			Check(
				Crlf("if (x) then begin", "end", "else begin", "end"),
				Crlf("if (x) then begin", "end else begin", "end"),
				new FormatOptions { Else = ElsePlacement.AfterEnd });
		}

		[Fact]
		public void ElsePlacement_OwnLine_PutsElseUnderEnd()
		{
			Check(
				Crlf("   if (x) then begin", "   end else begin", "   end"),
				Crlf("   if (x) then begin", "   end", "   else begin", "   end"),
				new FormatOptions { Else = ElsePlacement.OwnLine });
		}

		[Fact]
		public void Placement_NeverMovesAcrossACommentOrADirective()
		{
			String text = Crlf("if (x) then begin", "end // done", "else begin", "end", "#ifdef A", "else", "#endif");

			Check(text, text, new FormatOptions { Else = ElsePlacement.AfterEnd });
		}

		[Fact]
		public void Reindent_FollowsTheBlocks()
		{
			Check(
				Crlf("procedure start begin", "variable x;", "if (x) then begin", "foo;", "if (y) then begin", "bar;", "end", "end", "end"),
				Crlf("procedure start begin", "   variable x;", "   if (x) then begin", "      foo;", "      if (y) then begin", "         bar;", "      end", "   end", "end"),
				new FormatOptions { Reindent = true });
		}

		[Fact]
		public void Reindent_ElseIfChainAndCuddledElse()
		{
			Check(
				Crlf("procedure start begin", "if (a) then begin", "x;", "end else if (b) then begin", "y;", "end else begin", "z;", "end", "w;", "end"),
				Crlf("procedure start begin", "   if (a) then begin", "      x;", "   end else if (b) then begin", "      y;", "   end else begin", "      z;", "   end", "   w;", "end"),
				new FormatOptions { Reindent = true });
		}

		[Fact]
		public void Reindent_BodiesWithoutBegin()
		{
			Check(
				Crlf("procedure start begin", "if (a) then", "x := 1;", "else", "y := 2;", "while (a) do", "z := 3;", "w := 4;", "end"),
				Crlf("procedure start begin", "   if (a) then", "      x := 1;", "   else", "      y := 2;", "   while (a) do", "      z := 3;", "   w := 4;", "end"),
				new FormatOptions { Reindent = true });
		}

		[Fact]
		public void Reindent_DanglingElseBelongsToTheNearestIf()
		{
			Check(
				Crlf("procedure start begin", "if (a) then", "if (b) then", "x;", "else", "y;", "else", "z;", "end"),
				Crlf("procedure start begin", "   if (a) then", "      if (b) then", "         x;", "      else", "         y;", "   else", "      z;", "end"),
				new FormatOptions { Reindent = true });
		}

		[Fact]
		public void Reindent_AllmanBegin()
		{
			Check(
				Crlf("procedure start", "begin", "if (a) then", "begin", "x;", "end", "else", "begin", "y;", "end", "end"),
				Crlf("procedure start", "begin", "   if (a) then", "   begin", "      x;", "   end", "   else", "   begin", "      y;", "   end", "end"),
				new FormatOptions { Reindent = true });
		}

		[Fact]
		public void Reindent_SwitchCases()
		{
			Check(
				Crlf("procedure start begin", "switch (x) begin", "case 1: y := 1;", "case 2:", "y := 2;", "z := 3;", "case 3: begin", "y := 4;", "end", "default: y := 0;", "end", "end"),
				Crlf("procedure start begin", "   switch (x) begin", "      case 1: y := 1;", "      case 2:", "         y := 2;", "         z := 3;", "      case 3: begin", "         y := 4;", "      end", "      default: y := 0;", "   end", "end"),
				new FormatOptions { Reindent = true });
		}

		[Fact]
		public void Reindent_ContinuationLines()
		{
			Check(
				Crlf("procedure start begin", "x := foo(a,", "b,", "c);", "y := a +", "b;", "variable p,", "q;", "z := 1;", "end"),
				Crlf("procedure start begin", "   x := foo(a,", "      b,", "      c);", "   y := a +", "      b;", "   variable p,", "      q;", "   z := 1;", "end"),
				new FormatOptions { Reindent = true });
		}

		[Fact]
		public void Reindent_ContinuationIndentOfTwoLevels()
		{
			Check(
				Crlf("procedure start begin", "x := foo(a,", "b);", "end"),
				Crlf("procedure start begin", "   x := foo(a,", "         b);", "end"),
				new FormatOptions { Reindent = true, ContinuationIndent = 2 });
		}

		[Fact]
		public void Reindent_AlignContinuationToBracket()
		{
			Check(
				Crlf("procedure start begin", "x := foo(a,", "b, bar(c,", "d),", "e);", "end"),
				Crlf("procedure start begin", "   x := foo(a,", "            b, bar(c,", "                   d),", "            e);", "end"),
				new FormatOptions { Reindent = true, AlignContinuationToBracket = true });
		}

		[Fact]
		public void Reindent_AlignContinuationFallsBackToIndentWhenTheBracketEndsTheLine()
		{
			Check(
				Crlf("procedure start begin", "foo(", "a,", "b", ");", "end"),
				Crlf("procedure start begin", "   foo(", "      a,", "      b", "      );", "end"),
				new FormatOptions { Reindent = true, AlignContinuationToBracket = true });
		}

		[Fact]
		public void Reindent_Tabs()
		{
			Check(
				Crlf("procedure start begin", "if (a) then begin", "x;", "end", "end"),
				Crlf("procedure start begin", "\tif (a) then begin", "\t\tx;", "\tend", "end"),
				new FormatOptions { Reindent = true, UseTabs = true });
		}

		[Fact]
		public void Reindent_IndentSize()
		{
			Check(
				Crlf("procedure start begin", "if (a) then begin", "x;", "end", "end"),
				Crlf("procedure start begin", "    if (a) then begin", "        x;", "    end", "end"),
				new FormatOptions { Reindent = true, IndentSize = 4 });
		}

		[Fact]
		public void Reindent_CommentsFollowTheCodeAndMultiLineCommentsStayAsTheyAre()
		{
			Check(
				Crlf("procedure start begin", "// before x", "x;", "   /* one", "      two */", "y; // trailing", "// before end", "end"),
				Crlf("procedure start begin", "   // before x", "   x;", "   /* one", "      two */", "   y; // trailing", "   // before end", "end"),
				new FormatOptions { Reindent = true });
		}

		[Fact]
		public void Reindent_LeavesPreprocessorLinesAndBlankLinesAlone()
		{
			Check(
				Crlf("procedure start begin", "  #define A 1", "", "x;", "#ifdef DEBUG", "y;", "#endif", "end"),
				Crlf("procedure start begin", "  #define A 1", "", "   x;", "#ifdef DEBUG", "   y;", "#endif", "end"),
				new FormatOptions { Reindent = true });
		}

		[Fact]
		public void Reindent_ConditionalBranchesWithDifferentOpenersDoNotDrift()
		{
			Check(
				Crlf("procedure start begin", "#ifdef A", "if (a) then begin", "#else", "if (b) then begin", "#endif", "x;", "end", "end"),
				Crlf("procedure start begin", "#ifdef A", "   if (a) then begin", "#else", "   if (b) then begin", "#endif", "      x;", "   end", "end"),
				new FormatOptions { Reindent = true });
		}

		[Fact]
		public void Reindent_UnbalancedTextDoesNotThrowAndNeverGoesBelowZero()
		{
			Check(Crlf("end", "end", "x;"), Crlf("end", "end", "x;"), new FormatOptions { Reindent = true });
		}

		[Fact]
		public void Reindent_StatementWithoutSemicolonBeforeAnotherStatementIsNotAContinuation()
		{
			Check(
				Crlf("procedure start begin", "MACRO_BLOCK(x)", "y := 1;", "end"),
				Crlf("procedure start begin", "   MACRO_BLOCK(x)", "   y := 1;", "end"),
				new FormatOptions { Reindent = true });
		}

		[Theory]
		[InlineData(0, "")]
		[InlineData(1, "\r\n")]
		[InlineData(2, "\r\n\r\n")]
		[InlineData(3, "\r\n\r\n\r\n")]
		public void BlankLinesBetweenProcedures_SetsTheNumberOfBlankLines(Int32 count, String blank)
		{
			String before = Crlf("procedure a begin", "end", "", "", "", "procedure b begin", "end");
			String after = "procedure a begin\r\nend\r\n" + blank + "procedure b begin\r\nend\r\n";

			Check(before, after, new FormatOptions { BlankLinesBetweenProcedures = count });
			Check(Crlf("procedure a begin", "end", "procedure b begin", "end"), after, new FormatOptions { BlankLinesBetweenProcedures = count });
		}

		[Fact]
		public void BlankLinesBetweenProcedures_KeepsTheCommentOfAProcedureWithIt()
		{
			Check(
				Crlf("procedure a begin", "end", "// about b", "/* more */", "procedure b begin", "end"),
				Crlf("procedure a begin", "end", "", "// about b", "/* more */", "procedure b begin", "end"),
				new FormatOptions { BlankLinesBetweenProcedures = 1 });
		}

		[Fact]
		public void BlankLinesBetweenProcedures_IgnoresDeclarationsAndOtherText()
		{
			String text = Crlf("procedure a;", "procedure b;", "", "", "procedure a begin", "end", "#define X 1", "procedure b begin", "end");

			Check(text, text, new FormatOptions { BlankLinesBetweenProcedures = 1 });
		}

		[Fact]
		public void MaxConsecutiveBlankLines_LimitsTheRuns()
		{
			Check(Crlf("a;", "", "", "", "", "b;", "", "c;"), Crlf("a;", "", "", "b;", "", "c;"), new FormatOptions { MaxConsecutiveBlankLines = 2 });
			Check(Crlf("a;", "  ", "", "b;"), Crlf("a;", "  ", "b;"), new FormatOptions { MaxConsecutiveBlankLines = 1 });
		}

		[Fact]
		public void MaxConsecutiveBlankLines_NeverTouchesTheInsideOfAComment()
		{
			String text = Crlf("/* a", "", "", "", "b */", "x;");

			Check(text, text, new FormatOptions { MaxConsecutiveBlankLines = 1 });
		}

		[Fact]
		public void TrimTrailingWhitespace_RemovesSpacesAndTabs()
		{
			Check("a := 1;   \r\nb := 2;\t\r\n   \r\nc := 3;  ", "a := 1;\r\nb := 2;\r\n\r\nc := 3;", new FormatOptions { TrimTrailingWhitespace = true });
		}

		[Fact]
		public void TrimTrailingWhitespace_KeepsCommentsAndContinuationLines()
		{
			String text = "/* a   \r\n   b   */\r\n#define A 1 \\  \r\nx;\r\n";

			Check(text, text, new FormatOptions { TrimTrailingWhitespace = true });
		}

		[Fact]
		public void EnsureFinalNewline_AddsTheLineBreakOfTheDocument()
		{
			Check("a := 1;\r\nb := 2;", "a := 1;\r\nb := 2;\r\n", new FormatOptions { EnsureFinalNewline = true });
			Check("a := 1;\nb := 2;", "a := 1;\nb := 2;\n", new FormatOptions { EnsureFinalNewline = true });
			Check("a := 1;\r\n", "a := 1;\r\n", new FormatOptions { EnsureFinalNewline = true });
			Check(String.Empty, String.Empty, new FormatOptions { EnsureFinalNewline = true });
		}

		[Fact]
		public void AlignDefines_AlignsTheValuesOfConsecutiveDefines()
		{
			Check(
				Crlf("#define A 1", "#define LONG_NAME 2", "#define F(x) (x)", "#define EMPTY", "", "#define B   3"),
				Crlf("#define A         1", "#define LONG_NAME 2", "#define F(x)      (x)", "#define EMPTY", "", "#define B 3"),
				new FormatOptions { AlignDefines = true });
		}

		[Fact]
		public void AlignDefines_KeepsTheTrailingCommentOfTheValue()
		{
			Check(Crlf("#define A 1 // one", "#define BB 22 // two"), Crlf("#define A  1 // one", "#define BB 22 // two"), new FormatOptions { AlignDefines = true });
		}

		[Fact]
		public void MaxLineLength_WrapsLongArgumentLists()
		{
			Check(
				Crlf("procedure start begin", "   call a_procedure(first_argument, second_argument, third_argument, fourth_argument);", "end"),
				Crlf("procedure start begin", "   call a_procedure(first_argument, second_argument,", "      third_argument, fourth_argument);", "end"),
				new FormatOptions { MaxLineLength = 60 });
		}

		[Fact]
		public void MaxLineLength_AlignsToTheBracketWhenAsked()
		{
			Check(
				Crlf("procedure start begin", "   call a_procedure(first_argument, second_argument, third_argument, fourth_argument);", "end"),
				Crlf("procedure start begin", "   call a_procedure(first_argument, second_argument,", "                    third_argument, fourth_argument);", "end"),
				new FormatOptions { MaxLineLength = 60, AlignContinuationToBracket = true });
		}

		[Fact]
		public void MaxLineLength_LeavesLinesWithoutAFittingCommaAndCommentsAlone()
		{
			String text = Crlf("   x := a_very_long_name_without_any_comma + another_very_long_name_without_any_comma + third;", "   // a very long comment, with commas, that goes on and on and on and on and on and on");

			Check(text, text, new FormatOptions { MaxLineLength = 60 });
		}

		[Fact]
		public void MaxLineLength_NeverBreaksAStringOrAComma_OutsideBrackets()
		{
			String text = Crlf("   display_msg(\"a long text, with a comma, that is longer than the limit of the line to wrap\");", "variable aaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbb, cccccccccccccccccccccccccccc, dddddddddd;");

			Check(text, text, new FormatOptions { MaxLineLength = 60 });
		}

		[Fact]
		public void MaxLineLength_WorksWithTabs()
		{
			Check(
				Crlf("procedure start begin", "\tcall a_procedure(first_argument, second_argument, third_argument, fourth_argument);", "end"),
				Crlf("procedure start begin", "\tcall a_procedure(first_argument, second_argument,", "\t\tthird_argument, fourth_argument);", "end"),
				new FormatOptions { MaxLineLength = 56, UseTabs = true, IndentSize = 4 });
		}

		[Fact]
		public void LineBreaks_ArePreserved()
		{
			FormatOptions options = new FormatOptions { Reindent = true, Begin = BeginPlacement.NextLine, TrimTrailingWhitespace = true, BlankLinesBetweenProcedures = 1 };

			Assert.Equal("procedure a\nbegin\n   x := 1;\nend\n\nprocedure b\nbegin\nend\n", _formatter.Format("procedure a begin\nx:=1;  \nend\nprocedure b begin\nend\n", options));
			Assert.Equal("procedure a\r\nbegin\r\n   x := 1;\r\nend\r\n\r\nprocedure b\r\nbegin\r\nend\r\n", _formatter.Format("procedure a begin\r\nx:=1;  \r\nend\r\nprocedure b begin\r\nend\r\n", options));
		}

		[Fact]
		public void Normalize_RepairsOutOfRangeValues()
		{
			FormatOptions options = new FormatOptions { IndentSize = 99, ContinuationIndent = 0, BlankLinesBetweenProcedures = 9, MaxConsecutiveBlankLines = -3, MaxLineLength = 5 }.Normalize();

			Assert.Equal(FormatOptions.MaxIndentSize, options.IndentSize);
			Assert.Equal(1, options.ContinuationIndent);
			Assert.Equal(FormatOptions.MaxBlankLines, options.BlankLinesBetweenProcedures);
			Assert.Equal(0, options.MaxConsecutiveBlankLines);
			Assert.Equal(FormatOptions.MinLineLength, options.MaxLineLength);
		}

		[Fact]
		public void TheSampleOfThePreviewShowsEveryOption()
		{
			FormatOptions all = new FormatOptions
			{
				Reindent = true,
				Begin = BeginPlacement.SameLine,
				Else = ElsePlacement.AfterEnd,
				ParenthesisSpacing = InnerSpacingStyle.None,
				BlankLinesBetweenProcedures = 1,
				MaxConsecutiveBlankLines = 1,
				TrimTrailingWhitespace = true,
				AlignDefines = true,
				MaxLineLength = 80,
			};

			String formatted = _formatter.Format(FormatPreviewSample.Text, all);

			Assert.NotEqual(FormatPreviewSample.Text, formatted);
			Assert.Equal(formatted, _formatter.Format(formatted, all));
			Assert.NotEqual(_formatter.Format(FormatPreviewSample.Text), formatted);
		}
	}
}