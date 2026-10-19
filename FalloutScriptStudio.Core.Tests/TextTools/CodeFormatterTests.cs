namespace FalloutScriptStudio.Core.Tests.TextTools
{
	using System;
	using FalloutScriptStudio.Core.TextTools;
	using Xunit;

	public class CodeFormatterTests
	{
		private readonly CodeFormatter _formatter = new CodeFormatter();

		[Theory]
		[InlineData("   x:=1;", "   x := 1;")]
		[InlineData("   if(a==b) then", "   if(a == b) then")]
		[InlineData("   foo(a,b,c);", "   foo(a, b, c);")]
		[InlineData("   x := a+b*c;", "   x := a + b * c;")]
		[InlineData("   x := a>=b;", "   x := a >= b;")]
		[InlineData("   x+=2;", "   x += 2;")]
		[InlineData("   x:=a/b%c;", "   x := a / b % c;")]
		[InlineData("   if (a!=b) then", "   if (a != b) then")]
		public void Format_PutsSpacesAroundOperators(String input, String expected)
		{
			Assert.Equal(expected, this._formatter.Format(input));
		}

		[Theory]
		[InlineData("   x := -1;")]
		[InlineData("   foo(-1, -2);")]
		[InlineData("   x := (-5);")]
		[InlineData("   i++;")]
		[InlineData("   i--;")]
		[InlineData("   x := a - -1;")]
		public void Format_LeavesNegativeNumbersAndIncrementsAlone(String input)
		{
			Assert.Equal(input, this._formatter.Format(input));
		}

		[Fact]
		public void Format_BinaryMinusBetweenOperands_GetsSpaces()
		{
			Assert.Equal("   x := a - 1;", this._formatter.Format("   x := a-1;"));
		}

		[Fact]
		public void Format_DoesNotTouchStrings()
		{
			Assert.Equal("   display_msg(\"a+b,c:=d\");", this._formatter.Format("   display_msg(\"a+b,c:=d\");"));
			Assert.Equal("   x := \"a+b\" + y;", this._formatter.Format("   x:=\"a+b\"+y;"));
		}

		[Fact]
		public void Format_ChangedLineKeepsFollowingStringProtected()
		{
			// The positions of the strings move when spaces are inserted before them.
			Assert.Equal("   x := \"a,b\" + \"c+d\";", this._formatter.Format("   x:=\"a,b\"+\"c+d\";"));
		}

		[Fact]
		public void Format_DoesNotTouchComments()
		{
			Assert.Equal("   x := 1; // a+b,c", this._formatter.Format("   x:=1; // a+b,c"));
			Assert.Equal("   // x:=1;", this._formatter.Format("   // x:=1;"));
			Assert.Equal("   x := 1; /* a+b */ y := 2;", this._formatter.Format("   x:=1; /* a+b */ y:=2;"));
		}

		[Fact]
		public void Format_DoesNotTouchMultiLineBlockComments()
		{
			String text = TestScripts.Crlf("/* start", "a+b,c:=d", "end */", "x:=1;");

			Assert.Equal(TestScripts.Crlf("/* start", "a+b,c:=d", "end */", "x := 1;"), this._formatter.Format(text));
		}

		[Fact]
		public void Format_KeepsLineTerminators()
		{
			Assert.Equal("a := 1;\r\nb := 2;\r\n", this._formatter.Format("a:=1;\r\nb:=2;\r\n"));
			Assert.Equal("a := 1;\nb := 2;\n", this._formatter.Format("a:=1;\nb:=2;\n"));
		}

		[Fact]
		public void Format_OperatorAtLineBoundaries_DoesNotThrow()
		{
			Assert.Equal("-1;", this._formatter.Format("-1;"));
			Assert.Equal("   x := y -", this._formatter.Format("   x:=y -"));
			Assert.Equal("abc +", this._formatter.Format("abc+"));
		}

		[Fact]
		public void FormatSelectionOrLine_WithoutSelection_FormatsCaretLine()
		{
			String text = TestScripts.Crlf("a := 1;", "b:=2;", "c:=3;");

			TextEdit? edit = this._formatter.FormatSelectionOrLine(text, new TextSpan(11, 0));

			Assert.NotNull(edit);
			Assert.Equal(TestScripts.Crlf("a := 1;", "b := 2;", "c:=3;"), TextEditApplier.Apply(text, new[] { edit! }));
		}

		[Fact]
		public void FormatSelectionOrLine_WithSelection_FormatsOnlySelectedText()
		{
			String text = "a:=1;\r\nb:=2;";

			TextEdit? edit = this._formatter.FormatSelectionOrLine(text, new TextSpan(0, 5));

			Assert.Equal("a := 1;\r\nb:=2;", TextEditApplier.Apply(text, new[] { edit! }));
		}

		[Fact]
		public void FormatSelectionOrLine_AlreadyFormatted_ReturnsNull()
		{
			Assert.Null(this._formatter.FormatSelectionOrLine("a := 1;", new TextSpan(2, 0)));
		}

		[Fact]
		public void FormatSmart_FormatsTextBeforeCaretOnly()
		{
			String text = "   x:=1+2; y:=3;";

			// The caret is just after the first semicolon.
			SmartFormatResult? result = this._formatter.FormatSmart(text, 11);

			Assert.NotNull(result);
			Assert.Equal("   x := 1 + 2; y:=3;", TextEditApplier.Apply(text, new[] { result!.Edit }));
			Assert.Equal(11 + 4, result.CaretOffset);
		}

		[Fact]
		public void FormatSmart_NothingToInsert_ReturnsNull()
		{
			Assert.Null(this._formatter.FormatSmart("   x := 1;", 10));
		}
	}
}