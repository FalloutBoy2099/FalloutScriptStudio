namespace FalloutScriptStudio.Core.Tests.Snippets
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using FalloutScriptStudio.Core.Snippets;
	using FalloutScriptStudio.Core.TextTools;
	using Xunit;

	/// <summary>The expansion of a snippet body: tab stops, mirrors, nesting, variables, indentation, line breaks, the selection and malformed bodies.</summary>
	public sealed class SnippetExpanderTests
	{
		private static SnippetContext Context(String indentUnit = "   ", String lineIndent = "", String newLine = "\r\n")
		{
			return new SnippetContext { IndentUnit = indentUnit, LineIndent = lineIndent, NewLine = newLine, Now = new DateTime(2026, 10, 13, 14, 5, 9) };
		}

		private static String Text(SnippetExpansion expansion, TextSpan span)
		{
			return expansion.Text.Substring(span.Offset, span.Length);
		}

		[Fact]
		public void PlainText_IsInsertedAsItIs_AndTheCaretEndsAtTheEnd()
		{
			SnippetExpansion expansion = SnippetExpander.Expand("variable x;", Context());

			Assert.Equal("variable x;", expansion.Text);
			Assert.Empty(expansion.Stops);
			Assert.Equal(11, expansion.FinalOffset);
		}

		[Fact]
		public void TheTabStops_AreVisitedInTheOrderOfTheirNumbers_NotTheOrderOfTheText()
		{
			SnippetExpansion expansion = SnippetExpander.Expand("a(${2:second}, ${1:first}, ${3});$0", Context());

			Assert.Equal("a(second, first, );", expansion.Text);
			Assert.Equal(new[] { 1, 2, 3 }, expansion.Stops.Select(stop => stop.Index).ToArray());
			Assert.Equal("first", expansion.Stops[0].DefaultText);
			Assert.Equal("second", Text(expansion, expansion.Stops[1].Primary));
			Assert.True(expansion.Stops[2].Primary.IsEmpty);
			Assert.Equal(expansion.Text.Length, expansion.FinalOffset);
		}

		[Theory]
		[InlineData("x$1y", "xy")]
		[InlineData("x${1}y", "xy")]
		[InlineData("x${1:def}y", "xdefy")]
		public void TheThreeFormsOfATabStop(String body, String text)
		{
			SnippetExpansion expansion = SnippetExpander.Expand(body, Context());

			Assert.Equal(text, expansion.Text);
			Assert.Equal(1, expansion.Stops.Single().Index);
			Assert.Equal(1, expansion.Stops.Single().Primary.Offset);
		}

		[Fact]
		public void TheFinalPosition_IsWhereDollarZeroIs()
		{
			SnippetExpansion expansion = SnippetExpander.Expand("if ($1) then begin\n\t$0\nend", Context());

			Assert.Equal("if () then begin\r\n   \r\nend", expansion.Text);
			Assert.Equal(expansion.Text.IndexOf("\r\n   \r\n", StringComparison.Ordinal) + 5, expansion.FinalOffset);
		}

		[Fact]
		public void AMirror_RepeatsTheTextOfTheFirstOccurrence_AndIsListedAfterIt()
		{
			SnippetExpansion expansion = SnippetExpander.Expand("procedure ${1:name};\nprocedure $1 begin\nend // $1", Context());

			Assert.Equal("procedure name;\r\nprocedure name begin\r\nend // name", expansion.Text);
			SnippetStop stop = Assert.Single(expansion.Stops);
			Assert.Equal(3, stop.Occurrences.Count);
			Assert.Equal("procedure ".Length, stop.Primary.Offset);
			Assert.All(stop.Occurrences, occurrence => Assert.Equal("name", Text(expansion, occurrence)));
			Assert.Equal(stop.Occurrences.OrderBy(o => o.Offset).ToArray(), stop.Occurrences.ToArray());
		}

		[Fact]
		public void TheOccurrenceWithTheDefault_IsTheOneTheUserEdits_EvenWhenAMirrorComesFirst()
		{
			SnippetExpansion expansion = SnippetExpander.Expand("$1 = ${1:value}", Context());

			Assert.Equal("value = value", expansion.Text);
			SnippetStop stop = Assert.Single(expansion.Stops);
			Assert.Equal(8, stop.Primary.Offset);
			Assert.Equal(new[] { 8, 0 }, stop.Occurrences.Select(occurrence => occurrence.Offset).ToArray());
		}

		[Fact]
		public void APlaceholderCanHoldOtherPlaceholders()
		{
			SnippetExpansion expansion = SnippetExpander.Expand("call ${1:foo(${2:a}, ${3:b})};", Context());

			Assert.Equal("call foo(a, b);", expansion.Text);
			Assert.Equal("foo(a, b)", Text(expansion, expansion.Stops[0].Primary));
			Assert.Equal("a", Text(expansion, expansion.Stops[1].Primary));
			Assert.Equal("b", Text(expansion, expansion.Stops[2].Primary));
			Assert.True(expansion.Stops[1].Primary.Offset >= expansion.Stops[0].Primary.Offset);
			Assert.True(expansion.Stops[1].Primary.End <= expansion.Stops[0].Primary.End);
		}

		[Fact]
		public void AMirrorOfAPlaceholderThatHoldsOthers_GetsTheirMirrorsToo()
		{
			SnippetExpansion expansion = SnippetExpander.Expand("${1:f(${2:x})} $1", Context());

			Assert.Equal("f(x) f(x)", expansion.Text);
			Assert.Equal(2, expansion.Stops[0].Occurrences.Count);
			Assert.Equal(2, expansion.Stops[1].Occurrences.Count);
			Assert.Equal(2, expansion.Stops[1].Primary.Offset);
		}

		[Fact]
		public void APlaceholderThatContainsItself_DoesNotRecurseForEver()
		{
			SnippetExpansion expansion = SnippetExpander.Expand("${1:a$1b}", Context());

			Assert.Equal("ab", expansion.Text);
		}

		[Fact]
		public void TheLinesAfterTheFirst_StartWithTheIndentationOfTheLineOfTheInsertion()
		{
			SnippetExpansion expansion = SnippetExpander.Expand("if (x) then begin\n\tfoo;\n\tif (y) then begin\n\t\tbar;\n\tend\nend", Context("   ", "      "));

			Assert.Equal("if (x) then begin\r\n         foo;\r\n         if (y) then begin\r\n            bar;\r\n         end\r\n      end", expansion.Text);
		}

		[Fact]
		public void ATabAtTheStartOfALine_IsOneLevelOfTheIndentationOfTheDocument()
		{
			Assert.Equal("a\r\n\tb\r\n\t\tc", SnippetExpander.Expand("a\n\tb\n\t\tc", Context("\t")).Text);
			Assert.Equal("a\r\n  b\r\n    c", SnippetExpander.Expand("a\n\tb\n\t\tc", Context("  ")).Text);
			Assert.Equal("a\r\n x\r\n   y", SnippetExpander.Expand("a\n x\n   y", Context("\t")).Text);
		}

		[Fact]
		public void ATabInsideALine_IsKept()
		{
			Assert.Equal("a\tb", SnippetExpander.Expand("a\tb", Context("   ")).Text);
		}

		[Theory]
		[InlineData("\r\n")]
		[InlineData("\n")]
		public void TheLineBreaksAreTheOnesOfTheDocument(String newLine)
		{
			SnippetExpansion expansion = SnippetExpander.Expand("a\nb\r\nc\rd", Context(newLine: newLine));

			Assert.Equal(String.Join(newLine, "a", "b", "c", "d"), expansion.Text);
		}

		[Fact]
		public void TheStopsFollowTheLineBreaksAndIndentationInserted()
		{
			SnippetExpansion expansion = SnippetExpander.Expand("a\n\t${1:x}\n\t${2:y}", Context("\t", "\t\t", "\n"));

			Assert.Equal("a\n\t\t\t" + "x\n\t\t\ty", expansion.Text);
			Assert.Equal("x", Text(expansion, expansion.Stops[0].Primary));
			Assert.Equal("y", Text(expansion, expansion.Stops[1].Primary));
		}

		[Fact]
		public void TheVariablesGetTheirValues()
		{
			SnippetContext context = Context() with { CurrentProcedure = "start", FileName = "gl_g_test.ssl", ScriptName = "gl_g_test" };

			SnippetExpansion expansion = SnippetExpander.Expand("// $FILE_NAME $SCRIPT_NAME ${CURRENT_PROCEDURE} $DATE $TIME", context);

			Assert.Equal("// gl_g_test.ssl gl_g_test start 2026-10-13 14:05:09", expansion.Text);
		}

		[Fact]
		public void AVariableWithoutAValue_UsesItsDefault()
		{
			Assert.Equal("none", SnippetExpander.Expand("${CURRENT_PROCEDURE:none}", Context()).Text);
			Assert.Equal("start", SnippetExpander.Expand("${CURRENT_PROCEDURE:none}", Context() with { CurrentProcedure = "start" }).Text);
		}

		[Fact]
		public void TheSelection_IsInsertedWhereTheVariableIs_AndKeepsItsIndentationRelativeToTheOtherLines()
		{
			SnippetContext context = Context("   ", "   ") with { Selection = "x := 1;\r\nif (a) then\r\n   y := 2;" };

			SnippetExpansion expansion = SnippetExpander.Expand("if (${1:cond}) then begin\n\t$SELECTION\nend", context);

			Assert.Equal("if (cond) then begin\r\n      x := 1;\r\n      if (a) then\r\n         y := 2;\r\n   end", expansion.Text);
		}

		[Fact]
		public void TheSelection_IsDedented_WhenItWasIndentedInTheDocument()
		{
			SnippetContext context = Context() with { Selection = "      x := 1;\r\n        y := 2;" };

			SnippetExpansion expansion = SnippetExpander.Expand("begin\n\t$SELECTION\nend", context);

			Assert.Equal("begin\r\n   x := 1;\r\n     y := 2;\r\nend", expansion.Text);
		}

		[Fact]
		public void WithoutASelection_TheVariableIsEmpty_OrGivesItsDefault()
		{
			Assert.Equal("begin\r\n   \r\nend", SnippetExpander.Expand("begin\n\t$SELECTION\nend", Context()).Text);
			SnippetExpansion expansion = SnippetExpander.Expand("begin\n\t${SELECTION:${1:code}}\nend", Context());
			Assert.Equal("begin\r\n   code\r\nend", expansion.Text);
			Assert.Equal("code", expansion.Stops.Single().DefaultText);
		}

		[Fact]
		public void ABlankLineOfTheSelection_StaysEmpty()
		{
			SnippetContext context = Context("   ", "  ") with { Selection = "a;\r\n\r\nb;" };

			Assert.Equal("{\r\n     a;\r\n\r\n     b;\r\n  }", SnippetExpander.Expand("{\n\t$SELECTION\n}", context).Text);
		}

		[Theory]
		[InlineData(@"cost: \$5", "cost: $5")]
		[InlineData(@"a \} b", "a } b")]
		[InlineData(@"a \\ b", @"a \ b")]
		[InlineData(@"path \n here", @"path \n here")]
		[InlineData(@"${1:a \} b}", "a } b")]
		[InlineData("a $ b", "a $ b")]
		[InlineData("end$", "end$")]
		public void Escapes_AndADollarThatStartsNothing_AreText(String body, String text)
		{
			Assert.Equal(text, SnippetExpander.Expand(body, Context()).Text);
		}

		[Fact]
		public void PlainBracesInsideAPlaceholder_DoNotEndIt()
		{
			SnippetExpansion expansion = SnippetExpander.Expand("x := ${1:{1: 2}};", Context());

			Assert.Equal("x := {1: 2};", expansion.Text);
			Assert.Equal("{1: 2}", expansion.Stops.Single().DefaultText);
		}

		[Theory]
		[InlineData("${1:abc", "${1:abc", SnippetProblemKind.UnterminatedPlaceholder)]
		[InlineData("${", "${", SnippetProblemKind.UnterminatedPlaceholder)]
		[InlineData("a ${ b", "a ${ b", SnippetProblemKind.InvalidPlaceholder)]
		[InlineData("a ${1 b}", "a ${1 b}", SnippetProblemKind.InvalidPlaceholder)]
		[InlineData("$UNKNOWN_THING here", "$UNKNOWN_THING here", SnippetProblemKind.UnknownVariable)]
		[InlineData("${nope} here", "${nope} here", SnippetProblemKind.UnknownVariable)]
		public void AMalformedBody_StillExpands_TheMistakenPartIsText_AndTheProblemIsListed(String body, String text, SnippetProblemKind kind)
		{
			SnippetTemplate template = SnippetTemplate.Parse(body);
			SnippetExpansion expansion = SnippetExpander.Expand(template, Context());

			Assert.Equal(text, expansion.Text);
			Assert.Contains(template.Problems, problem => problem.Kind == kind);
		}

		[Fact]
		public void AMalformedPlaceholderInsideAGoodOne_DoesNotSpoilTheGoodOne()
		{
			SnippetTemplate template = SnippetTemplate.Parse("${1:a ${ b} c}");
			SnippetExpansion expansion = SnippetExpander.Expand(template, Context());

			Assert.Equal("a ${ b} c", expansion.Text);
			Assert.Single(expansion.Stops);
			Assert.NotEmpty(template.Problems);
		}

		[Fact]
		public void AnEmptyBody_IsAProblem()
		{
			Assert.Contains(SnippetTemplate.Parse("  \n ").Problems, problem => problem.Kind == SnippetProblemKind.EmptyBody);
			Assert.Empty(SnippetTemplate.Parse("x := 1;").Problems);
		}

		[Fact]
		public void ABodyThatUsesTheSelection_IsFoundByTheTemplate()
		{
			Assert.True(SnippetTemplate.Parse("if (x) then begin\n\t$SELECTION\nend").UsesSelection);
			Assert.True(SnippetTemplate.Parse("${1:${SELECTION}}").UsesSelection);
			Assert.False(SnippetTemplate.Parse("if (x) then begin\n\t$0\nend").UsesSelection);
		}

		[Fact]
		public void TheNumbersOfTheStopsAreNotLimitedToNine()
		{
			SnippetExpansion expansion = SnippetExpander.Expand("$10 $2 ${11:x}", Context());

			Assert.Equal(new[] { 2, 10, 11 }, expansion.Stops.Select(stop => stop.Index).ToArray());
		}

		[Fact]
		public void ExpandingTwice_GivesTheSameResult()
		{
			SnippetContext context = Context("\t", "\t");
			String body = "procedure ${1:name}(${2:variable a}) begin\n\t${3:// body}\n\treturn ${4:0};\nend$0";

			Assert.Equal(SnippetExpander.Expand(body, context), SnippetExpander.Expand(body, context) with { }, new ExpansionComparer());
		}

		private sealed class ExpansionComparer : IEqualityComparer<SnippetExpansion>
		{
			public Boolean Equals(SnippetExpansion? x, SnippetExpansion? y)
			{
				return x is not null && y is not null && x.Text == y.Text && x.FinalOffset == y.FinalOffset && x.Stops.Select(s => s.Index + ":" + String.Join(",", s.Occurrences)).SequenceEqual(y.Stops.Select(s => s.Index + ":" + String.Join(",", s.Occurrences)));
			}

			public Int32 GetHashCode(SnippetExpansion obj)
			{
				return obj.Text.GetHashCode(StringComparison.Ordinal);
			}
		}
	}
}