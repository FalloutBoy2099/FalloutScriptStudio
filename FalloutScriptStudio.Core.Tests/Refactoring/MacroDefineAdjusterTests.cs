namespace FalloutScriptStudio.Core.Tests.Refactoring
{
	using System;
	using FalloutScriptStudio.Core.Refactoring;
	using FalloutScriptStudio.Core.TextTools;
	using Xunit;

	public class MacroDefineAdjusterTests
	{
		private static String Adjust(String text, String token, Int32 line, Int32 diff)
		{
			TextEdit? edit = MacroDefineAdjuster.CreateEdit(text, token, line, diff);
			return edit is null ? text : TextEditApplier.Apply(text, new[] { edit });
		}

		[Theory]
		[InlineData("#define FOO    1", 2, "#define FOO  1")]
		[InlineData("#define FOO  1", 5, "#define FOO 1")]
		[InlineData("#define FOO 1", 3, "#define FOO 1")]
		[InlineData("#define FOO\t\t1", 1, "#define FOO\t1")]
		[InlineData("  #  define FOO   1", 2, "  #  define FOO 1")]
		public void LongerName_RemovesSpacesButKeepsOne(String line, Int32 diff, String expected)
		{
			Assert.Equal(expected, Adjust(line, "FOO", 1, diff));
		}

		[Theory]
		[InlineData("#define FOO 1", -2, "#define FOO   1")]
		[InlineData("#define FOO(a) (a)", -1, "#define FOO(a)  (a)")]
		[InlineData("#define FOO(a)(a)", -3, "#define FOO(a)   (a)")]
		public void ShorterName_InsertsSpaces(String line, Int32 diff, String expected)
		{
			Assert.Equal(expected, Adjust(line, "FOO", 1, diff));
		}

		[Fact]
		public void NoValue_NothingToAdjust()
		{
			Assert.Equal("#define FOO", Adjust("#define FOO", "FOO", 1, -2));
			Assert.Equal("#define FOO  ", Adjust("#define FOO  ", "FOO", 1, -2));
		}

		[Fact]
		public void NotTheDefinitionLine_ReturnsNull()
		{
			String text = TestScripts_Lines("x := FOO;", "#define FOOBAR 1", "#define BAR FOO");

			Assert.Null(MacroDefineAdjuster.CreateEdit(text, "FOO", 1, -1));
			Assert.Null(MacroDefineAdjuster.CreateEdit(text, "FOO", 2, -1));
			Assert.Null(MacroDefineAdjuster.CreateEdit(text, "FOO", 3, -1));
			Assert.Null(MacroDefineAdjuster.CreateEdit(text, "FOO", 99, -1));
			Assert.Null(MacroDefineAdjuster.CreateEdit(text, "FOO", 0, -1));
		}

		[Fact]
		public void ZeroDifference_ReturnsNull()
		{
			Assert.Null(MacroDefineAdjuster.CreateEdit("#define FOO 1", "FOO", 1, 0));
		}

		private static String TestScripts_Lines(params String[] lines)
		{
			return String.Join("\r\n", lines);
		}
	}
}