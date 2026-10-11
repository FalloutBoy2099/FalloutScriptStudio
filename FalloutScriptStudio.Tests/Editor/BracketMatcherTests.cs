namespace FalloutScriptStudio.Tests.Editor
{
	using System;
	using FalloutScriptStudio.Editor.Text;
	using ICSharpCode.AvalonEdit.Document;
	using Xunit;

	public sealed class BracketMatcherTests
	{
		private static BracketPair? Find(String text, Int32 caret)
		{
			return BracketMatcher.FindPair(new StringTextSource(text), LexicalMap.Build(text), caret);
		}

		[Fact]
		public void FindPair_CaretAfterOpeningBracket_FindsClosing()
		{
			BracketPair? pair = Find("f(a, (b))", 2);

			Assert.Equal(new BracketPair(1, 8), pair);
		}

		[Fact]
		public void FindPair_CaretAfterClosingBracket_FindsOpening()
		{
			BracketPair? pair = Find("f(a, (b))", 9);

			Assert.Equal(new BracketPair(1, 8), pair);
		}

		[Fact]
		public void FindPair_InnerPair_MatchesNesting()
		{
			Assert.Equal(new BracketPair(5, 7), Find("f(a, (b))", 7));
		}

		[Fact]
		public void FindPair_IgnoresBracketsInStringsAndComments()
		{
			String text = "f(\")\" /* ) */ )";

			Assert.Equal(new BracketPair(1, text.Length - 1), Find(text, 2));
		}

		[Fact]
		public void FindPair_Unmatched_ReturnsNull()
		{
			Assert.Null(Find("f(a", 2));
		}

		[Fact]
		public void FindPair_NoBracketNearCaret_ReturnsNull()
		{
			Assert.Null(Find("abc (d)", 2));
		}

		[Fact]
		public void FindPair_SquareBrackets_AreMatchedSeparatelyFromRound()
		{
			Assert.Equal(new BracketPair(1, 7), Find("a[(1)+2]", 2));
		}

		[Fact]
		public void FindPair_BracketInsideLineComment_ReturnsNull()
		{
			Assert.Null(Find("// (x)", 4));
		}
	}
}
