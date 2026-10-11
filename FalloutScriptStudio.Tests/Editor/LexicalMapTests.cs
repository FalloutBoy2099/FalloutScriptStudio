namespace FalloutScriptStudio.Tests.Editor
{
	using System;
	using FalloutScriptStudio.Editor.Text;
	using Xunit;

	public sealed class LexicalMapTests
	{
		[Fact]
		public void Build_LineComment_CoversTextUpToLineEnd()
		{
			LexicalMap map = LexicalMap.Build("a := 1; // note \"x\"\r\nb := 2;");

			Assert.Equal(LexicalKind.Code, map.GetKindAt(0));
			Assert.Equal(LexicalKind.LineComment, map.GetKindAt(8));
			Assert.Equal(LexicalKind.LineComment, map.GetKindAt(15));
			Assert.Equal(LexicalKind.Code, map.GetKindAt(21));
		}

		[Fact]
		public void Build_BlockComment_SpansLines()
		{
			String text = "x /* one\r\ntwo */ y";
			LexicalMap map = LexicalMap.Build(text);

			Assert.Equal(LexicalKind.BlockComment, map.GetKindAt(text.IndexOf("two", StringComparison.Ordinal)));
			Assert.Equal(LexicalKind.Code, map.GetKindAt(text.IndexOf('y')));
		}

		[Fact]
		public void Build_StringWithEscapedQuote_StaysOneString()
		{
			String text = "s := \"a\\\"b\"; // c";
			LexicalMap map = LexicalMap.Build(text);

			Assert.Equal(LexicalKind.String, map.GetKindAt(text.IndexOf('b')));
			Assert.Equal(LexicalKind.Code, map.GetKindAt(text.IndexOf(';')));
			Assert.Equal(LexicalKind.LineComment, map.GetKindAt(text.IndexOf('c')));
		}

		[Fact]
		public void Build_CommentMarkerInsideString_IsNotAComment()
		{
			LexicalMap map = LexicalMap.Build("s := \"http://x\";");

			Assert.Equal(LexicalKind.String, map.GetKindAt(12));
			Assert.Single(map.Spans);
		}

		[Fact]
		public void Build_UnterminatedStringEndsAtLineEnd()
		{
			String text = "a := \"abc\r\nb := 1;";
			LexicalMap map = LexicalMap.Build(text);

			Assert.Equal(LexicalKind.String, map.GetKindAt(7));
			Assert.Equal(LexicalKind.Code, map.GetKindAt(text.LastIndexOf('b')));
			Assert.False(map.Spans[0].IsClosed);
		}

		[Fact]
		public void Build_Braces_AreAKindOfTheirOwn()
		{
			String text = "if ({condition}) then";
			LexicalMap map = LexicalMap.Build(text);

			Assert.Equal(LexicalKind.Braces, map.GetKindAt(text.IndexOf('c')));
			Assert.Equal(LexicalKind.Code, map.GetKindAt(text.IndexOf("then", StringComparison.Ordinal)));
		}

		[Theory]
		[InlineData("// abc", 6, LexicalKind.LineComment)]
		[InlineData("// abc", 0, LexicalKind.Code)]
		[InlineData("x \"ab\" y", 3, LexicalKind.String)]
		[InlineData("x \"ab\" y", 6, LexicalKind.Code)]
		[InlineData("x \"ab", 5, LexicalKind.String)]
		[InlineData("/* a */ b", 7, LexicalKind.Code)]
		[InlineData("/* a */ b", 6, LexicalKind.BlockComment)]
		public void GetKindAtCaret_UsesInsertionPointSemantics(String text, Int32 caret, LexicalKind expected)
		{
			Assert.Equal(expected, LexicalMap.Build(text).GetKindAtCaret(caret));
		}

		[Fact]
		public void IsCodeAtCaret_OfEmptyText_IsTrue()
		{
			Assert.True(LexicalMap.Build(String.Empty).IsCodeAtCaret(0));
		}
	}
}
