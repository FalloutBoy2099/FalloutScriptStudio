namespace FalloutScriptStudio.Tests.Editor
{
	using System;
	using FalloutScriptStudio.Editor.Text;
	using ICSharpCode.AvalonEdit.Document;
	using Xunit;

	public sealed class WordFinderTests
	{
		private static ITextSource Source(String text)
		{
			return new StringTextSource(text);
		}

		[Fact]
		public void GetWordAt_InsideIdentifier_ReturnsWholeWord()
		{
			Assert.Equal("my_proc1", WordFinder.GetWordAt(Source("call my_proc1(x);"), 8));
		}

		[Fact]
		public void GetWordAt_OnPunctuation_ReturnsEmpty()
		{
			Assert.Equal(String.Empty, WordFinder.GetWordAt(Source("call my_proc1(x);"), 13));
		}

		[Theory]
		[InlineData("foo bar", 3, "foo")]
		[InlineData("foo bar", 4, "bar")]
		[InlineData("foo bar", 7, "bar")]
		[InlineData("foo ", 4, "")]
		public void GetWordNearCaret_PrefersTheWordRightOfTheCaret(String text, Int32 caret, String expected)
		{
			Assert.Equal(expected, WordFinder.GetWordNearCaret(Source(text), caret));
		}

		[Fact]
		public void FindWordStart_ReturnsStartOfIdentifier()
		{
			Assert.Equal(3, WordFinder.FindWordStart(Source("if abc_1"), 7));
		}

		[Fact]
		public void FindWordStart_OutsideIdentifier_ReturnsOffset()
		{
			Assert.Equal(2, WordFinder.FindWordStart(Source("if abc_1"), 2));
		}
	}
}