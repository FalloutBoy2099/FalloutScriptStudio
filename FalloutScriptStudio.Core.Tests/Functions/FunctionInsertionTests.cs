namespace FalloutScriptStudio.Core.Tests.Functions
{
	using System;
	using FalloutScriptStudio.Core.Functions;
	using FalloutScriptStudio.Core.TextTools;
	using Xunit;

	public class FunctionInsertionTests
	{
		[Fact]
		public void Create_SimpleCode_IsInsertedAsIs()
		{
			FunctionInsertion insertion = FunctionInsertion.Create("call {procedure};", 4, false, ' ', '\r');

			Assert.Equal("call {procedure};", insertion.Text);
			Assert.Equal(new TextSpan(5, 11), insertion.FirstPlaceholder);
		}

		[Fact]
		public void Create_LineBreaks_AreIndentedToTheCaretColumn()
		{
			FunctionInsertion insertion = FunctionInsertion.Create("if {c} then begin<cr><cr>end<cr>", 3, false, ' ', '\r', "\n");

			Assert.Equal("if {c} then begin\n   \n   end\n   ", insertion.Text);
		}

		[Fact]
		public void Create_TouchingLettersGetSeparatingSpaces()
		{
			Assert.Equal(" abs({x})", FunctionInsertion.Create("abs({x})", 0, false, 'a', ';').Text);
			Assert.Equal("abs({x}) ", FunctionInsertion.Create("abs({x})", 0, false, ' ', 'b').Text);
			Assert.Equal("abs({x})", FunctionInsertion.Create("abs({x})", 0, true, 'a', 'b').Text);
		}

		[Fact]
		public void Create_MultiLineCodeGetsNoTrailingSpace()
		{
			Assert.Equal("x<cr>".Replace("<cr>", "\r\n"), FunctionInsertion.Create("x<cr>", 0, false, ' ', 'b').Text);
		}

		[Fact]
		public void Create_NoPlaceholder_HasNoSelection()
		{
			Assert.Null(FunctionInsertion.Create("exit;", 0, false, ' ', ' ').FirstPlaceholder);
		}

		[Theory]
		[InlineData("DEBUGMSG({string})<cr>", "DEBUGMSG")]
		[InlineData("  my_macro_1(x)", "my_macro_1")]
		[InlineData("{x}", "")]
		[InlineData("", "")]
		[InlineData("name", "name")]
		public void FunctionKeyword_ExtractsTheLeadingIdentifier(String code, String expected)
		{
			Assert.Equal(expected, FunctionKeyword.Extract(code));
		}

		[Fact]
		public void FunctionDefinition_ToIniLine_ConvertsLineBreaks()
		{
			var definition = new FunctionDefinition(" Name ", " Desc\r\nmore ", "a;\r\nb;\n\n");

			Assert.Equal("Name<d>Desc more<s>a;<cr>b;", definition.ToIniLine());
		}
	}
}