namespace FalloutScriptStudio.Core.Tests.TextTools
{
	using System;
	using FalloutScriptStudio.Core.TextTools;
	using Xunit;

	public class WhitespaceToolsTests
	{
		[Fact]
		public void DecreaseIndent_RemovesOneLevelFromSelectedLines()
		{
			String text = "begin\r\n        a;\r\n        b;\r\nend";

			var edits = WhitespaceTools.DecreaseIndent(text, new TextSpan(7, 22), 4);

			Assert.Equal("begin\r\n    a;\r\n    b;\r\nend", TextEditApplier.Apply(text, edits));
		}

		[Fact]
		public void DecreaseIndent_KeepsRelativeIndentation()
		{
			String text = "    a;\r\n        b;\r\n    c;";

			var edits = WhitespaceTools.DecreaseIndent(text, new TextSpan(0, text.Length), 4);

			Assert.Equal("a;\r\n    b;\r\nc;", TextEditApplier.Apply(text, edits));
		}

		[Fact]
		public void DecreaseIndent_NeverRemovesMoreThanTheSmallestIndent()
		{
			String text = "  a;\r\n        b;";

			var edits = WhitespaceTools.DecreaseIndent(text, new TextSpan(0, text.Length), 4);

			Assert.Equal("a;\r\n      b;", TextEditApplier.Apply(text, edits));
		}

		[Fact]
		public void DecreaseIndent_AlignsToTabStop()
		{
			String text = "      a;";

			var edits = WhitespaceTools.DecreaseIndent(text, new TextSpan(0, 0), 4);

			Assert.Equal("    a;", TextEditApplier.Apply(text, edits));
		}

		[Fact]
		public void DecreaseIndent_RemovesOneTabFromTabIndentedLines()
		{
			String text = "\t\ta;\r\n\t\tb;";

			var edits = WhitespaceTools.DecreaseIndent(text, new TextSpan(0, text.Length), 4);

			Assert.Equal("\ta;\r\n\tb;", TextEditApplier.Apply(text, edits));
		}

		[Fact]
		public void DecreaseIndent_SkipsBlankLinesAndNoIndent()
		{
			Assert.Empty(WhitespaceTools.DecreaseIndent("a;", new TextSpan(0, 0), 4));
			Assert.Empty(WhitespaceTools.DecreaseIndent("   ", new TextSpan(0, 0), 4));
			String text = "    a;\r\n\r\n    b;";
			Assert.Equal("a;\r\n\r\nb;", TextEditApplier.Apply(text, WhitespaceTools.DecreaseIndent(text, new TextSpan(0, text.Length), 4)));
		}

		[Fact]
		public void AlignToFirstLine_PullsDeeperLinesToTheFirstIndent()
		{
			String text = "  a;\r\n      b;\r\n   c;\r\n  d;";

			var edits = WhitespaceTools.AlignToFirstLine(text, new TextSpan(0, text.Length));

			Assert.Equal("  a;\r\n  b;\r\n  c;\r\n  d;", TextEditApplier.Apply(text, edits));
		}

		[Fact]
		public void AlignToFirstLine_WithoutSelection_DoesNothing()
		{
			Assert.Empty(WhitespaceTools.AlignToFirstLine("  a;\r\n      b;", new TextSpan(1, 0)));
		}

		[Fact]
		public void ConvertTabsToSpaces_WholeText()
		{
			var edits = WhitespaceTools.ConvertTabsToSpaces("\ta;\r\n\t\tb;\tc", default, 4);

			Assert.Equal("    a;\r\n        b;    c", TextEditApplier.Apply("\ta;\r\n\t\tb;\tc", edits));
		}

		[Fact]
		public void ConvertTabsToSpaces_Selection()
		{
			String text = "\ta\r\n\tb";

			var edits = WhitespaceTools.ConvertTabsToSpaces(text, new TextSpan(0, 3), 2);

			Assert.Equal("  a\r\n\tb", TextEditApplier.Apply(text, edits));
		}

		[Fact]
		public void ConvertSpacesToTabs_ReplacesRunsOfTabSize()
		{
			var edits = WhitespaceTools.ConvertSpacesToTabs("        a;", default, 4);

			Assert.Equal("\t\ta;", TextEditApplier.Apply("        a;", edits));
		}

		[Fact]
		public void ConvertLeadingTabsToSpaces_LeavesInnerTabs()
		{
			String text = "\ta;\tb\r\n\t\tc;";

			var edits = WhitespaceTools.ConvertLeadingTabsToSpaces(text, default, 4);

			Assert.Equal("    a;\tb\r\n        c;", TextEditApplier.Apply(text, edits));
		}

		[Fact]
		public void ConvertLeadingSpacesToTabs_ConvertsWholeIndentLevels()
		{
			String text = "        a;\r\n      b;\r\nc;";

			var edits = WhitespaceTools.ConvertLeadingSpacesToTabs(text, default, 4);

			Assert.Equal("\t\ta;\r\n\t  b;\r\nc;", TextEditApplier.Apply(text, edits));
		}

		[Fact]
		public void TrimTrailingWhitespace_TrimsEveryLineIncludingTheLast()
		{
			String text = "a;   \r\nb;\t\r\n\r\nc;  ";

			Assert.Equal("a;\r\nb;\r\n\r\nc;", WhitespaceTools.TrimTrailingWhitespace(text));
		}

		[Fact]
		public void TrimTrailingWhitespace_Selection_OnlyTouchesSelectedLines()
		{
			String text = "a; \r\nb; \r\nc; ";

			var edits = WhitespaceTools.TrimTrailingWhitespace(text, new TextSpan(5, 2));

			Assert.Equal("a; \r\nb;\r\nc; ", TextEditApplier.Apply(text, edits));
		}
	}
}
