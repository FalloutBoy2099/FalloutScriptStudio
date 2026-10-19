namespace FalloutScriptStudio.Core.Tests.Parsing
{
	using System;
	using FalloutScriptStudio.Core.Parsing;
	using Xunit;

	public class CommentTrackerTests
	{
		[Theory]
		[InlineData("")]
		[InlineData("x")]
		[InlineData("// procedure foo begin")]
		public void ShouldSkip_SkipsShortAndLineCommentLines(String text)
		{
			var tracker = new CommentTracker();
			String line = text;

			Assert.True(tracker.ShouldSkip(ref line));
		}

		[Fact]
		public void ShouldSkip_RemovesBlockCommentInsideLine()
		{
			var tracker = new CommentTracker();
			String line = "call foo(/* the argument */ 1);";

			Boolean skipped = tracker.ShouldSkip(ref line);

			Assert.False(skipped);
			Assert.Equal("call foo(  1);", line);
			Assert.False(tracker.IsInsideBlockComment);
		}

		[Fact]
		public void ShouldSkip_TracksMultiLineComment()
		{
			var tracker = new CommentTracker();
			String first = "x := 1; /* start of comment";
			String middle = "procedure hidden begin";
			String last = "end of comment */ y := 2;";

			Assert.True(tracker.ShouldSkip(ref first));
			Assert.Equal("x := 1; ", first);
			Assert.True(tracker.IsInsideBlockComment);

			Assert.True(tracker.ShouldSkip(ref middle));
			Assert.Equal(String.Empty, middle);

			Assert.False(tracker.ShouldSkip(ref last));
			Assert.Equal("y := 2;", last);
			Assert.False(tracker.IsInsideBlockComment);
		}
	}
}