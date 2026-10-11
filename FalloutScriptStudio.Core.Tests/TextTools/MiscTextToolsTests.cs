namespace FalloutScriptStudio.Core.Tests.TextTools
{
	using System;
	using FalloutScriptStudio.Core.TextTools;
	using Xunit;

	public class MiscTextToolsTests
	{
		[Fact]
		public void ToUpper_And_ToLower_ConvertSelection()
		{
			Assert.Equal("a FOO c", TextEditApplier.Apply("a foo c", new[] { CaseTools.ToUpper("a foo c", new TextSpan(2, 3))! }));
			Assert.Equal("A foo C", TextEditApplier.Apply("A FOO C", new[] { CaseTools.ToLower("A FOO C", new TextSpan(2, 3))! }));
		}

		[Fact]
		public void CaseTools_EmptySelectionOrUnchangedText_ReturnNull()
		{
			Assert.Null(CaseTools.ToUpper("abc", new TextSpan(1, 0)));
			Assert.Null(CaseTools.ToUpper("ABC", new TextSpan(0, 3)));
			Assert.Null(CaseTools.Capitalize("Abc", new TextSpan(0, 3)));
		}

		[Fact]
		public void Capitalize_UppercasesWordStartsOnly()
		{
			String text = "hello big_world";

			TextEdit? edit = CaseTools.Capitalize(text, new TextSpan(0, text.Length));

			Assert.Equal("Hello Big_World", TextEditApplier.Apply(text, new[] { edit! }));
		}

		[Fact]
		public void Capitalize_SelectionInsideWord_KeepsFirstLetter()
		{
			String text = "hello world";

			TextEdit? edit = CaseTools.Capitalize(text, new TextSpan(2, 9));

			Assert.Equal("hello World", TextEditApplier.Apply(text, new[] { edit! }));
		}

		[Theory]
		[InlineData("x := 0x1F;", 5, 4, "x := 31;")]
		[InlineData("x := 255;", 5, 3, "x := 0xFF;")]
		[InlineData("x := 0xFFFFFFFB;", 5, 10, "x := -5;")]
		[InlineData("x := -5;", 6, 1, "x := 0xFFFFFFFB;")]
		[InlineData("x := 0;", 5, 1, "x := 0x0;")]
		public void NumberBaseConverter_ConvertsBetweenHexAndDecimal(String text, Int32 offset, Int32 length, String expected)
		{
			TextEdit? edit = NumberBaseConverter.Convert(text, new TextSpan(offset, length));

			Assert.NotNull(edit);
			Assert.Equal(expected, TextEditApplier.Apply(text, new[] { edit! }));
		}

		[Theory]
		[InlineData("x := abc;", 5, 3)]
		[InlineData("x := 0xZZ;", 5, 4)]
		[InlineData("x := 99999999999;", 5, 11)]
		[InlineData("x := 5;", 5, 0)]
		public void NumberBaseConverter_NotANumber_ReturnsNull(String text, Int32 offset, Int32 length)
		{
			Assert.Null(NumberBaseConverter.Convert(text, new TextSpan(offset, length)));
		}

		[Theory]
		[InlineData("a\nb\r\nc\rd", "a\r\nb\r\nc\r\nd")]
		[InlineData("a\r\nb", "a\r\nb")]
		[InlineData("", "")]
		[InlineData("\n", "\r\n")]
		[InlineData("a\r", "a\r\n")]
		public void NormalizeNewLine_MakesEveryTerminatorCrLf(String input, String expected)
		{
			Assert.Equal(expected, NewLineTools.NormalizeNewLine(input));
		}

		[Fact]
		public void ConvertToUnixPlatform_OnlyConvertsForUnix()
		{
			Assert.Equal("a\nb", NewLineTools.ConvertToUnixPlatform("a\r\nb", true));
			Assert.Equal("a\r\nb", NewLineTools.ConvertToUnixPlatform("a\r\nb", false));
			Assert.Equal("a\nb\nc", NewLineTools.ToUnixLineEndings("a\r\nb\rc"));
		}

		[Fact]
		public void QuoteScanner_FindsPairsAndIgnoresTrailingQuote()
		{
			var quotes = QuoteScanner.FindQuotes("a \"b\" c \"d\" \"e");

			Assert.Equal(new[] { new QuoteSpan(2, 4), new QuoteSpan(8, 10) }, quotes);
			Assert.True(QuoteScanner.IsInsideQuotes("a \"bcd\" e", 4));
			Assert.False(QuoteScanner.IsInsideQuotes("a \"bcd\" e", 2));
			Assert.False(QuoteScanner.IsInsideQuotes("a \"bcd\" e", 6));
		}

		[Theory]
		[InlineData("foo bar", 0, "foo")]
		[InlineData("foo bar", 2, "foo")]
		[InlineData("foo bar", 3, "")]
		[InlineData("foo_1(bar)", 4, "foo_1")]
		[InlineData("foo", 3, "")]
		public void WordScanner_GetWordAt(String text, Int32 offset, String expected)
		{
			Assert.Equal(expected, WordScanner.GetWordAt(text, offset));
		}

		[Fact]
		public void WordScanner_FindsWordBoundaries()
		{
			Assert.Equal(3, WordScanner.FindWordStart("a  foo_1(", 8));
			Assert.Equal(8, WordScanner.FindWordEnd("a  foo_1(", 4));
		}

		[Theory]
		[InlineData("x := 1; // note", 10, LexicalContext.LineComment)]
		[InlineData("x := 1; // note\r\ny", 17, LexicalContext.Code)]
		[InlineData("x := \"abc", 8, LexicalContext.StringLiteral)]
		[InlineData("x := \"abc\" y", 11, LexicalContext.Code)]
		[InlineData("/* abc", 4, LexicalContext.BlockComment)]
		[InlineData("/* abc */ x", 10, LexicalContext.Code)]
		[InlineData("x /", 3, LexicalContext.Code)]
		public void SslLexicalScanner_GetContext(String text, Int32 offset, LexicalContext expected)
		{
			Assert.Equal(expected, SslLexicalScanner.GetContext(text, offset));
		}
	}
}
