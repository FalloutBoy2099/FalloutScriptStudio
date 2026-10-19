namespace FalloutScriptStudio.Tests.Editor
{
	using System;
	using FalloutScriptStudio.Core.TextTools;
	using FalloutScriptStudio.Editor.Text;
	using ICSharpCode.AvalonEdit.Document;
	using Xunit;

	public sealed class PlaceholderAndOccurrenceTests
	{
		private static Boolean Find(String text, Int32 offset, out Int32 start, out Int32 length)
		{
			return PlaceholderFinder.TryFind(LexicalMap.Build(text), new StringTextSource(text), offset, out start, out length);
		}

		[Fact]
		public void PlaceholderFinder_InsideBraces_ReturnsWholeBlock()
		{
			Assert.True(Find("if ({condition}) then", 8, out Int32 start, out Int32 length));
			Assert.Equal(4, start);
			Assert.Equal(11, length);
		}

		[Fact]
		public void PlaceholderFinder_AtTheBraces_Works()
		{
			Assert.True(Find("if ({a}) then", 4, out Int32 start, out _));
			Assert.Equal(4, start);
			Assert.True(Find("if ({a}) then", 6, out start, out _));
			Assert.Equal(4, start);
		}

		[Theory]
		[InlineData("x := {1, 2};", 7)]
		[InlineData("x := {\"a\": 1};", 8)]
		[InlineData("x := {};", 6)]
		[InlineData("x := {abc", 7)]
		[InlineData("x := abc;", 7)]
		public void PlaceholderFinder_NotATemplate_ReturnsFalse(String text, Int32 offset)
		{
			Assert.False(Find(text, offset, out _, out _));
		}

		[Fact]
		public void OccurrenceFinder_WholeWord_SkipsLongerIdentifiers()
		{
			var found = OccurrenceFinder.Find("foo foobar _foo foo;", "foo", true);

			Assert.Equal(new[] { new TextSpan(0, 3), new TextSpan(16, 3) }, found);
		}

		[Fact]
		public void OccurrenceFinder_Substring_FindsEverything()
		{
			Assert.Equal(3, OccurrenceFinder.Find("foo foobar _foo", "foo", false).Count);
		}

		[Fact]
		public void OccurrenceFinder_IsCaseSensitive()
		{
			Assert.Single(OccurrenceFinder.Find("Foo foo", "foo", true));
		}

		[Theory]
		[InlineData("", false)]
		[InlineData("+", false)]
		[InlineData("()", false)]
		[InlineData("a", true)]
		[InlineData("(a", true)]
		[InlineData("hello", true)]
		[InlineData(":=", false)]
		public void OccurrenceFinder_IsHighlightable_FollowsTheLegacyRule(String text, Boolean expected)
		{
			Assert.Equal(expected, OccurrenceFinder.IsHighlightable(text));
		}
	}
}