namespace FalloutScriptStudio.Tests.Editor
{
	using System;
	using FalloutScriptStudio.Editor.Text;
	using ICSharpCode.AvalonEdit.Document;
	using Xunit;

	public sealed class SslIndentationTests
	{
		private const String Unit = "   ";

		[Theory]
		[InlineData("procedure start begin", true)]
		[InlineData("   if (x) then begin", true)]
		[InlineData("   if (x) then", true)]
		[InlineData("   else", true)]
		[InlineData("   while (x) do", true)]
		[InlineData("   end else begin", true)]
		[InlineData("   end else", false)]
		[InlineData("   end", false)]
		[InlineData("   foo := 1;", false)]
		[InlineData("   foo := 1; // then begin", false)]
		[InlineData("   BEGIN", true)]
		[InlineData("   suspend_begin", false)]
		public void OpensBlock_RecognisesBlockOpeners(String line, Boolean expected)
		{
			Assert.Equal(expected, SslIndentationRules.OpensBlock(line));
		}

		[Fact]
		public void GetNextLineIndentation_AfterBegin_AddsOneLevel()
		{
			Assert.Equal("      ", SslIndentationRules.GetNextLineIndentation("   if (a) then begin", Unit));
		}

		[Fact]
		public void GetNextLineIndentation_PlainLine_KeepsIndentation()
		{
			Assert.Equal("\t", SslIndentationRules.GetNextLineIndentation("\tfoo;", Unit));
		}

		[Fact]
		public void GetNextLineIndentation_WhitespaceOnlyPreviousLine_CopiesIt()
		{
			Assert.Equal("      ", SslIndentationRules.GetNextLineIndentation("      ", Unit));
		}

		[Fact]
		public void FindBlockOpenerIndentation_SkipsNestedBlocks()
		{
			String[] lines =
			{
				"procedure start begin",
				"   if (a) then begin",
				"      foo;",
				"   end",
				"   bar;",
				"   end",
			};

			Assert.Equal(String.Empty, SslIndentationRules.FindBlockOpenerIndentation(i => lines[i], 5));
		}

		[Fact]
		public void FindBlockOpenerIndentation_EndElseBegin_BelongsToTheOpeningIf()
		{
			String[] lines =
			{
				"   if (a) then begin",
				"      foo;",
				"   end else begin",
				"      bar;",
				"         end",
			};

			Assert.Equal("   ", SslIndentationRules.FindBlockOpenerIndentation(i => lines[i], 4));
		}

		[Fact]
		public void FindBlockOpenerIndentation_NoOpener_ReturnsNull()
		{
			Assert.Null(SslIndentationRules.FindBlockOpenerIndentation(i => "foo;", 3));
		}

		[Fact]
		public void Strategy_NewLineAfterBegin_IsIndentedDeeper()
		{
			TextDocument document = new TextDocument("procedure x begin\r\n\r\nend");
			SslIndentationStrategy strategy = new SslIndentationStrategy(() => Unit);

			strategy.IndentLine(document, document.GetLineByNumber(2));

			Assert.Equal("procedure x begin\r\n   \r\nend", document.Text);
		}

		[Fact]
		public void Strategy_NewLineBeforeEnd_KeepsOpenerIndentation()
		{
			TextDocument document = new TextDocument("procedure x begin\r\nend");
			SslIndentationStrategy strategy = new SslIndentationStrategy(() => Unit);

			strategy.IndentLine(document, document.GetLineByNumber(2));

			Assert.Equal("procedure x begin\r\nend", document.Text);
		}

		[Fact]
		public void Strategy_AlignClosingEnd_MovesEndToItsBegin()
		{
			TextDocument document = new TextDocument("   if (a) then begin\r\n      foo;\r\n      end");
			SslIndentationStrategy strategy = new SslIndentationStrategy(() => Unit);

			Boolean changed = strategy.AlignClosingEnd(document, document.GetLineByNumber(3));

			Assert.True(changed);
			Assert.Equal("   if (a) then begin\r\n      foo;\r\n   end", document.Text);
		}

		[Fact]
		public void Strategy_AlignClosingEnd_LineWithMoreCode_IsLeftAlone()
		{
			TextDocument document = new TextDocument("begin\r\n   endless");
			SslIndentationStrategy strategy = new SslIndentationStrategy(() => Unit);

			Assert.False(strategy.AlignClosingEnd(document, document.GetLineByNumber(2)));
		}
	}
}
