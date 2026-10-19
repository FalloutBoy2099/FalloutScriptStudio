namespace FalloutScriptStudio.Core.Tests.TextTools
{
	using System;
	using FalloutScriptStudio.Core.TextTools;
	using Xunit;

	public class KeywordExpanderTests
	{
		private readonly KeywordExpander _expander = new KeywordExpander();

		private static String Expand(KeywordExpander expander, String text, KeywordExpansionOptions? options = null)
		{
			KeywordExpansion? expansion = expander.TryExpand(text, text.Length, options ?? new KeywordExpansionOptions());
			Assert.NotNull(expansion);
			return TextEditApplier.Apply(text, new[] { expansion!.Edit });
		}

		[Fact]
		public void If_ExpandsToBlockWithPlaceholder()
		{
			String text = "   if";

			KeywordExpansion? expansion = this._expander.TryExpand(text, text.Length, new KeywordExpansionOptions());
			String result = TextEditApplier.Apply(text, new[] { expansion!.Edit });

			Assert.Equal("   if ({condition}) then begin\r\n       \r\n   end", result);
			Assert.Equal("{condition}", result.Substring(expansion.Selection.Offset, expansion.Selection.Length));
		}

		[Fact]
		public void If_AtLineStart_HasNoOuterIndentation()
		{
			Assert.Equal("if ({condition}) then begin\r\n    \r\nend", Expand(this._expander, "if"));
		}

		[Fact]
		public void If_WithTabs_UsesTabsForIndentation()
		{
			var options = new KeywordExpansionOptions { ConvertTabsToSpaces = false };

			Assert.Equal("\tif ({condition}) then begin\r\n\t\t\r\n\tend", Expand(this._expander, "\tif", options));
		}

		[Fact]
		public void If_WithTabsInTextButSpacesSetting_ConvertsTheIndentation()
		{
			Assert.Equal("\tif ({condition}) then begin\r\n        \r\n    end", Expand(this._expander, "\tif"));
		}

		[Fact]
		public void If_WithUnixNewLine()
		{
			var options = new KeywordExpansionOptions { NewLine = "\n", TabSize = 2 };

			Assert.Equal("if ({condition}) then begin\n  \nend", Expand(this._expander, "if", options));
		}

		[Fact]
		public void Ifel_ProducesIfElseBlocks()
		{
			Assert.Equal("if ({condition}) then begin\r\n    \r\nend else begin\r\n    \r\nend", Expand(this._expander, "ifel"));
		}

		[Fact]
		public void Elif_TurnsIntoElseIf()
		{
			Assert.Equal("end else if ({condition}) then begin\r\n    \r\nend", Expand(this._expander, "end elif"));
		}

		[Fact]
		public void Else_ExpandsToBeginBlockAndPutsTheCaretInTheBody()
		{
			String text = "else";

			KeywordExpansion? expansion = this._expander.TryExpand(text, text.Length, new KeywordExpansionOptions());
			String result = TextEditApplier.Apply(text, new[] { expansion!.Edit });

			Assert.Equal("else begin\r\n    \r\nend", result);
			Assert.True(expansion.Selection.IsEmpty);
			Assert.Equal(result.IndexOf("\r\n", StringComparison.Ordinal) + 2 + 4, expansion.Selection.Offset);
		}

		[Fact]
		public void For_Foreach_While_Switch_UseTheirTemplates()
		{
			Assert.Equal("for ({iterator} := 0; {condition}; {iterator}++) begin\r\n    \r\nend", Expand(this._expander, "for"));
			Assert.Equal("foreach ({iterator}: {item} in {array}) begin\r\n    \r\nend", Expand(this._expander, "foreach"));
			Assert.Equal("while ({condition}) do begin\r\n    \r\nend", Expand(this._expander, "while"));
			Assert.Equal(
				"switch ({condition}) begin\r\n    case {constant} : {code}\r\n    default         : {code}\r\nend",
				Expand(this._expander, "switch"));
		}

		[Fact]
		public void Then_Var_Proc_CompleteTheKeyword()
		{
			Assert.Equal("if (a) then begin", Expand(this._expander, "if (a) then"));
			Assert.Equal("variable ", Expand(this._expander, "var"));
			Assert.Equal("procedure ", Expand(this._expander, "proc"));
		}

		[Fact]
		public void CaretInTheMiddleOfTheWord_ExpandsThePartBeforeTheCaret()
		{
			String text = "ifx";

			KeywordExpansion? expansion = this._expander.TryExpand(text, 2, new KeywordExpansionOptions());

			Assert.NotNull(expansion);
			Assert.EndsWith("end ", expansion!.Edit.NewText);
		}

		[Theory]
		[InlineData("// if")]
		[InlineData("x := \"if")]
		[InlineData("/* if")]
		[InlineData("foo")]
		[InlineData("x")]
		[InlineData("IF")]
		[InlineData("myif")]
		public void NotAKeywordOrInCommentOrString_ReturnsNull(String text)
		{
			Assert.Null(this._expander.TryExpand(text, text.Length, new KeywordExpansionOptions()));
		}

		[Fact]
		public void IsKeyword_KnowsTheEleven()
		{
			Assert.Equal(11, this._expander.Keywords.Count);
			Assert.True(this._expander.IsKeyword("switch"));
			Assert.False(this._expander.IsKeyword("begin"));
		}
	}
}