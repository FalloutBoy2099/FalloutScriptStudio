namespace FalloutScriptStudio.Core.Tests.Analysis
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using FalloutScriptStudio.Core.Analysis;
	using Xunit;

	public class SslTokenizerTests
	{
		private static List<String> Texts(SslTokenStream stream)
		{
			return Enumerable.Range(0, stream.Count).Select(stream.GetText).ToList();
		}

		[Fact]
		public void Tokenize_SplitsWordsNumbersStringsAndOperators()
		{
			SslTokenStream stream = SslTokenizer.Tokenize("variable x := 0x1F + foo(\"a b\", 2.5);");

			Assert.Equal(new[] { "variable", "x", ":=", "0x1F", "+", "foo", "(", "\"a b\"", ",", "2.5", ")", ";" }, Texts(stream));
			Assert.Equal(SslTokenKind.Identifier, stream[0].Kind);
			Assert.Equal(SslTokenKind.Number, stream[3].Kind);
			Assert.Equal(SslTokenKind.String, stream[7].Kind);
			Assert.Equal(SslTokenKind.Punctuation, stream[2].Kind);
		}

		[Fact]
		public void Tokenize_SkipsLineAndBlockComments_AndCountsTheirLines()
		{
			String text = Ssl.Crlf("a // comment x", "/* b", "c */ d", "e");

			SslTokenStream stream = SslTokenizer.Tokenize(text);

			Assert.Equal(new[] { "a", "d", "e" }, Texts(stream));
			Assert.Equal(1, stream[0].Line);
			Assert.Equal(3, stream[1].Line);
			Assert.Equal(4, stream[2].Line);
		}

		[Fact]
		public void Tokenize_KeywordsInsideStringsAndCommentsAreNotTokens()
		{
			SslTokenStream stream = SslTokenizer.Tokenize("x := \"begin end // not a comment\"; // begin\r\n/* end */ y;");

			Assert.Equal(new[] { "x", ":=", "\"begin end // not a comment\"", ";", "y", ";" }, Texts(stream));
		}

		[Fact]
		public void Tokenize_AStringEndsAtTheEndOfTheLine()
		{
			SslTokenStream stream = SslTokenizer.Tokenize("a \"unclosed\r\nb");

			Assert.Equal(new[] { "a", "\"unclosed", "b" }, Texts(stream));
			Assert.Equal(2, stream[2].Line);
		}

		[Theory]
		[InlineData("\r\n")]
		[InlineData("\n")]
		[InlineData("\r")]
		public void Tokenize_CountsLinesTheSameWayForEveryLineBreakStyle(String newLine)
		{
			SslTokenStream stream = SslTokenizer.Tokenize("a" + newLine + "b" + newLine + newLine + "c");

			Assert.Equal(new[] { 1, 2, 4 }, Enumerable.Range(0, stream.Count).Select(i => stream[i].Line).ToArray());
		}

		[Fact]
		public void Tokenize_ADirectiveMarksItsTokensUpToTheEndOfTheLogicalLine()
		{
			String text = Ssl.Crlf("#define MAX(a, b) \\", "    ((a) > (b))", "procedure foo;");

			SslTokenStream stream = SslTokenizer.Tokenize(text);

			Assert.Equal(SslTokenKind.Directive, stream[0].Kind);
			Assert.Equal("define", stream.GetDirectiveName(0));
			Int32 procedure = Enumerable.Range(0, stream.Count).First(i => stream.IsKeyword(i, "procedure"));
			for (Int32 i = 0; i < procedure; i++)
			{
				Assert.True(stream[i].InDirective, stream.GetText(i));
			}

			Assert.False(stream[procedure].InDirective);
			Assert.Equal(3, stream[procedure].Line);
		}

		[Fact]
		public void Tokenize_AHashInTheMiddleOfALineIsPunctuation()
		{
			SslTokenStream stream = SslTokenizer.Tokenize("a # b");

			Assert.Equal(SslTokenKind.Punctuation, stream[1].Kind);
			Assert.False(stream[1].InDirective);
		}

		[Fact]
		public void Tokenize_ADirectiveIndentedWithSpacesIsRecognized()
		{
			SslTokenStream stream = SslTokenizer.Tokenize("    #include \"a.h\"\r\nx");

			Assert.Equal("include", stream.GetDirectiveName(0));
			Assert.True(stream[1].InDirective);
			Assert.False(stream[2].InDirective);
		}

		[Fact]
		public void Tokenize_BlockCommentInsideADirectiveDoesNotEndIt()
		{
			SslTokenStream stream = SslTokenizer.Tokenize("#define X 1 /* a\r\nb */ + 2\r\nz");

			Assert.True(stream[Texts(stream).IndexOf("2")].InDirective);
			Assert.False(stream[Texts(stream).IndexOf("z")].InDirective);
		}

		[Fact]
		public void FindTokenAt_FindsTheTokenThatContainsAnOffset()
		{
			SslTokenStream stream = SslTokenizer.Tokenize("abc  def");

			Assert.Equal(0, stream.FindTokenAt(0));
			Assert.Equal(0, stream.FindTokenAt(2));
			Assert.Equal(1, stream.FindTokenAt(3));
			Assert.Equal(1, stream.FindTokenAt(5));
			Assert.Equal(2, stream.FindTokenAt(8));
			Assert.Equal(1, stream.FindFirstTokenFrom(1));
		}

		[Fact]
		public void GetColumn_IsOneBased()
		{
			SslTokenStream stream = SslTokenizer.Tokenize("ab\r\n  cd");

			Assert.Equal(1, stream.GetColumn(0));
			Assert.Equal(3, stream.GetColumn(stream[1].Offset));
		}

		[Fact]
		public void Tokenize_AnEmptyTextHasNoTokens()
		{
			Assert.Equal(0, SslTokenizer.Tokenize(String.Empty).Count);
		}
	}
}