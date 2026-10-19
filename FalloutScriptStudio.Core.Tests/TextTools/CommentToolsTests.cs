namespace FalloutScriptStudio.Core.Tests.TextTools
{
	using System;
	using FalloutScriptStudio.Core.TextTools;
	using Xunit;

	public class CommentToolsTests
	{
		[Fact]
		public void CommentLines_InsertsMarkerAtFirstNonBlankCharacter()
		{
			String text = "   a;\r\n\tb;\r\nc;";

			var edits = CommentTools.CommentLines(text, new TextSpan(0, text.Length));

			Assert.Equal("   //a;\r\n\t//b;\r\n//c;", TextEditApplier.Apply(text, edits));
		}

		[Fact]
		public void CommentLines_SkipsAlreadyCommentedLines()
		{
			String text = "// a;\r\nb;";

			var edits = CommentTools.CommentLines(text, new TextSpan(0, text.Length));

			Assert.Equal("// a;\r\n//b;", TextEditApplier.Apply(text, edits));
		}

		[Fact]
		public void CommentLines_WithoutSelection_CommentsCaretLine()
		{
			String text = "a;\r\nb;\r\nc;";

			var edits = CommentTools.CommentLines(text, new TextSpan(5, 0));

			Assert.Equal("a;\r\n//b;\r\nc;", TextEditApplier.Apply(text, edits));
		}

		[Fact]
		public void CommentLines_SelectionEndingAtLineStart_ExcludesThatLine()
		{
			String text = "a;\r\nb;\r\nc;";

			var edits = CommentTools.CommentLines(text, new TextSpan(0, 8));

			Assert.Equal("//a;\r\n//b;\r\nc;", TextEditApplier.Apply(text, edits));
		}

		[Fact]
		public void UncommentLines_RemovesFirstMarkerOfCommentedLines()
		{
			String text = "  //a; // note\r\nb;\r\n//c;";

			var edits = CommentTools.UncommentLines(text, new TextSpan(0, text.Length));

			Assert.Equal("  a; // note\r\nb;\r\nc;", TextEditApplier.Apply(text, edits));
		}

		[Fact]
		public void ToggleBlockComment_WrapsSelection()
		{
			String text = "a; b; c;";

			var edits = CommentTools.ToggleBlockComment(text, new TextSpan(3, 2));

			Assert.Equal("a; /*b;*/ c;", TextEditApplier.Apply(text, edits));
		}

		[Fact]
		public void ToggleBlockComment_SelectionInsideComment_RemovesIt()
		{
			String text = "a; /*b;*/ c;";

			var edits = CommentTools.ToggleBlockComment(text, new TextSpan(6, 2));

			Assert.Equal("a; b; c;", TextEditApplier.Apply(text, edits));
		}

		[Fact]
		public void ToggleBlockComment_SelectionContainingComment_RemovesIt()
		{
			String text = "a; /*b;*/ c;";

			var edits = CommentTools.ToggleBlockComment(text, new TextSpan(0, text.Length));

			Assert.Equal("a; b; c;", TextEditApplier.Apply(text, edits));
		}

		[Fact]
		public void ToggleBlockComment_CaretInsideComment_RemovesIt()
		{
			String text = "/* x */";

			Assert.Equal(" x ", TextEditApplier.Apply(text, CommentTools.ToggleBlockComment(text, new TextSpan(3, 0))));
		}

		[Fact]
		public void ToggleBlockComment_CaretOutsideComment_DoesNothing()
		{
			Assert.Empty(CommentTools.ToggleBlockComment("a; /* x */ b;", new TextSpan(1, 0)));
		}

		[Fact]
		public void ToggleBlockComment_SelectionAfterClosedComment_WrapsAgain()
		{
			String text = "/* x */ y; z;";

			var edits = CommentTools.ToggleBlockComment(text, new TextSpan(8, 2));

			Assert.Equal("/* x */ /*y;*/ z;", TextEditApplier.Apply(text, edits));
		}
	}
}