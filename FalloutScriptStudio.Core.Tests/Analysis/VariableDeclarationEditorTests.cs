namespace FalloutScriptStudio.Core.Tests.Analysis
{
	using System;
	using System.Linq;
	using FalloutScriptStudio.Core.Analysis;
	using FalloutScriptStudio.Core.TextTools;
	using Xunit;

	public class VariableDeclarationEditorTests
	{
		private static String Remove(String text, String name)
		{
			UnusedVariable variable = UnusedVariableFinder.Find(text).Single(v => v.Name == name);
			TextEdit? edit = VariableDeclarationEditor.CreateRemoval(text, variable);
			Assert.NotNull(edit);
			return TextEditApplier.Apply(text, new[] { edit! });
		}

		[Fact]
		public void CreateRemoval_RemovesTheWholeLineOfAStatementThatDeclaresOnlyThatVariable()
		{
			String text = Ssl.Crlf("procedure start begin", "\tvariable unused;", "\tdebug_msg(1);", "end");

			Assert.Equal(Ssl.Crlf("procedure start begin", "\tdebug_msg(1);", "end"), Remove(text, "unused"));
		}

		[Fact]
		public void CreateRemoval_KeepsTheLineBreakStyle()
		{
			String text = Ssl.Lf("procedure start begin", "\tvariable unused;", "\tdebug_msg(1);", "end");

			Assert.Equal(Ssl.Lf("procedure start begin", "\tdebug_msg(1);", "end"), Remove(text, "unused"));
		}

		[Theory]
		[InlineData("a", "variable b, c;")]
		[InlineData("b", "variable a, c;")]
		[InlineData("c", "variable a, b;")]
		public void CreateRemoval_RemovesOneVariableOfAList(String name, String expected)
		{
			String text = Ssl.Crlf("procedure start begin", "\tvariable a, b, c;", "\tdebug_msg(" + String.Join(") + debug_msg(", new[] { "a", "b", "c" }.Where(n => n != name)) + ");", "end");

			String result = Remove(text, name);

			Assert.Contains("\t" + expected + "\r\n", result, StringComparison.Ordinal);
		}

		[Fact]
		public void CreateRemoval_RemovesTheInitializerWithTheVariable()
		{
			String text = Ssl.Crlf("procedure start begin", "\tvariable keep := 1, drop := [1, 2], tail;", "\tdebug_msg(keep + tail);", "end");

			Assert.Equal(Ssl.Crlf("procedure start begin", "\tvariable keep := 1, tail;", "\tdebug_msg(keep + tail);", "end"), Remove(text, "drop"));
		}

		[Fact]
		public void CreateRemoval_RemovesTheFirstVariableWithItsInitializer()
		{
			String text = Ssl.Crlf("procedure start begin", "\tvariable drop = 3, keep;", "\tdebug_msg(keep);", "end");

			Assert.Equal(Ssl.Crlf("procedure start begin", "\tvariable keep;", "\tdebug_msg(keep);", "end"), Remove(text, "drop"));
		}

		[Fact]
		public void CreateRemoval_ATrailingLineCommentGoesWithTheLine()
		{
			String text = Ssl.Crlf("procedure start begin", "\tvariable unused; // temporary", "\tdebug_msg(1);", "end");

			Assert.Equal(Ssl.Crlf("procedure start begin", "\tdebug_msg(1);", "end"), Remove(text, "unused"));
		}

		[Fact]
		public void CreateRemoval_CodeOnTheSameLineStays()
		{
			String text = Ssl.Crlf("procedure start begin variable unused; debug_msg(1);", "end");

			Assert.Equal(Ssl.Crlf("procedure start begin debug_msg(1);", "end"), Remove(text, "unused"));
		}

		[Fact]
		public void CreateRemoval_CodeBeforeTheStatementStays()
		{
			String text = Ssl.Crlf("procedure start begin", "\tdebug_msg(1); variable unused;", "end");

			Assert.Equal(Ssl.Crlf("procedure start begin", "\tdebug_msg(1);", "end"), Remove(text, "unused"));
		}

		[Fact]
		public void CreateRemoval_ALastLineWithoutLineBreakIsRemoved()
		{
			String text = "variable unused;";

			Assert.Equal(String.Empty, Remove(text, "unused"));
		}

		[Fact]
		public void CreateRemoval_ScriptVariablesAreRemovedToo()
		{
			String text = Ssl.Crlf("variable unused;", "variable used;", "procedure start begin", "\tdebug_msg(used);", "end");

			Assert.Equal(Ssl.Crlf("variable used;", "procedure start begin", "\tdebug_msg(used);", "end"), Remove(text, "unused"));
		}

		[Fact]
		public void CreateRemoval_ReturnsNothingForAVariableThatCannotBeRemoved()
		{
			String text = Ssl.Crlf("procedure start begin", "\tvariable x = random(1, 2);", "end");
			UnusedVariable variable = UnusedVariableFinder.Find(text).Single();

			Assert.Null(VariableDeclarationEditor.CreateRemoval(text, variable));
		}

		[Fact]
		public void CreateRemoval_ReturnsNothingWhenTheTextChanged()
		{
			String text = Ssl.Crlf("procedure start begin", "\tvariable unused;", "end");
			UnusedVariable variable = UnusedVariableFinder.Find(text).Single();

			Assert.Null(VariableDeclarationEditor.CreateRemoval("// moved\r\n" + text.Replace("unused", "other", StringComparison.Ordinal), variable));
		}

		[Fact]
		public void CreateRemoval_AMultiLineListKeepsItsShape()
		{
			String text = Ssl.Crlf("procedure start begin", "\tvariable a,", "\t\tb,", "\t\tc;", "\tdebug_msg(a + c);", "end");

			Assert.Equal(Ssl.Crlf("procedure start begin", "\tvariable a,", "\t\tc;", "\tdebug_msg(a + c);", "end"), Remove(text, "b"));
		}

		[Fact]
		public void CreateRemoval_AppliedTwiceFromTheBottomRemovesBothVariables()
		{
			String text = Ssl.Crlf("procedure start begin", "\tvariable one;", "\tvariable two;", "end");
			System.Collections.Generic.List<TextEdit> edits = UnusedVariableFinder.Find(text)
				.Select(v => VariableDeclarationEditor.CreateRemoval(text, v)!)
				.ToList();

			Assert.Equal(Ssl.Crlf("procedure start begin", "end"), TextEditApplier.Apply(text, edits));
		}
	}
}
