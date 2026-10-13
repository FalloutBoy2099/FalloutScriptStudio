namespace FalloutScriptStudio.Core.Tests.Analysis
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using FalloutScriptStudio.Core.Analysis;
	using Xunit;

	public class UnusedVariableFinderTests
	{
		private static IReadOnlyList<UnusedVariable> Find(String text, UnusedVariableOptions? options = null)
		{
			return UnusedVariableFinder.Find(text, options);
		}

		private static String[] Names(IEnumerable<UnusedVariable> variables)
		{
			return variables.Select(v => v.Name).ToArray();
		}

		[Fact]
		public void Find_ReportsALocalThatIsNeverNamed()
		{
			String text = Ssl.Crlf(
				"procedure start begin",
				"\tvariable used, unused;",
				"\tused = 1;",
				"\tdebug_msg(used);",
				"end");

			IReadOnlyList<UnusedVariable> result = Find(text);

			UnusedVariable variable = Assert.Single(result);
			Assert.Equal("unused", variable.Name);
			Assert.Equal(UnusedVariableStatus.Unused, variable.Status);
			Assert.Equal(VariableDeclarationKind.Local, variable.Kind);
			Assert.Equal("start", variable.ProcedureName);
			Assert.Equal(2, variable.Line);
			Assert.Equal(17, variable.Column);
			Assert.Equal(text.IndexOf("unused;", StringComparison.Ordinal), variable.Offset);
			Assert.Equal(6, variable.Length);
			Assert.True(variable.CanRemove);
		}

		[Theory]
		[InlineData("x := 1;")]
		[InlineData("x = 1;")]
		public void Find_AVariableThatIsOnlyAssignedIsWriteOnly_WhateverTheAssignmentOperator(String assignment)
		{
			String text = Ssl.Crlf("procedure start begin", "\tvariable x;", "\t" + assignment, "end");

			UnusedVariable variable = Assert.Single(Find(text));

			Assert.Equal(UnusedVariableStatus.WriteOnly, variable.Status);
			Assert.False(variable.CanRemove);
		}

		[Fact]
		public void Find_WriteOnlyVariablesCanBeLeftOut()
		{
			String text = Ssl.Crlf("procedure start begin", "\tvariable x;", "\tx := 1;", "end");

			Assert.Empty(Find(text, new UnusedVariableOptions { IncludeWriteOnly = false }));
		}

		[Theory]
		[InlineData("x += 1;")]
		[InlineData("x++;")]
		[InlineData("foo(x);")]
		[InlineData("y = x;")]
		[InlineData("x[0] := 1;")]
		[InlineData("x[0] = 1;")]
		[InlineData("if (x == 1) then begin end")]
		[InlineData("y = (x = 1);")]
		public void Find_AnyOtherNamingIsAUse(String statement)
		{
			String text = Ssl.Crlf("procedure start begin", "\tvariable x, y;", "\t" + statement, "\tdebug_msg(y);", "end");

			Assert.DoesNotContain("x", Names(Find(text)));
		}

		[Fact]
		public void Find_ReadAfterAnAssignmentIsAUse()
		{
			String text = Ssl.Crlf("procedure start begin", "\tvariable x;", "\tx := 1;", "\tdebug_msg(x);", "end");

			Assert.Empty(Find(text));
		}

		[Fact]
		public void Find_IsCaseInsensitive()
		{
			String text = Ssl.Crlf("procedure start begin", "\tvariable Counter;", "\tdebug_msg(COUNTER);", "end");

			Assert.Empty(Find(text));
		}

		[Fact]
		public void Find_NamesInCommentsAndStringsAreNotUses()
		{
			String text = Ssl.Crlf(
				"procedure start begin",
				"\tvariable ghost;",
				"\t// ghost is gone",
				"\t/* ghost */",
				"\tdebug_msg(\"ghost\");",
				"end");

			Assert.Equal(new[] { "ghost" }, Names(Find(text)));
		}

		[Fact]
		public void Find_ALocalIsOnlyUsedInItsOwnProcedure()
		{
			String text = Ssl.Crlf(
				"procedure a begin",
				"\tvariable i;",
				"\ti = 1;",
				"\tdebug_msg(i);",
				"end",
				"procedure b begin",
				"\tvariable i;",
				"end");

			UnusedVariable variable = Assert.Single(Find(text));
			Assert.Equal("b", variable.ProcedureName);
		}

		[Fact]
		public void Find_ALocalHidesAScriptVariableOfTheSameName()
		{
			String text = Ssl.Crlf(
				"variable value;",
				"procedure a begin",
				"\tvariable value;",
				"\tvalue = 1;",
				"\tdebug_msg(value);",
				"end");

			UnusedVariable variable = Assert.Single(Find(text));
			Assert.Equal(VariableDeclarationKind.Global, variable.Kind);
		}

		[Fact]
		public void Find_AScriptVariableUsedInAnyProcedureIsUsed()
		{
			String text = Ssl.Crlf(
				"variable shared;",
				"procedure a begin",
				"\tshared = 1;",
				"end",
				"procedure b begin",
				"\tdebug_msg(shared);",
				"end");

			Assert.Empty(Find(text));
		}

		[Fact]
		public void Find_ScriptVariablesCanBeLeftOut_AndImportedOrExportedOnesAreNeverReported()
		{
			String text = Ssl.Crlf("variable a;", "import variable b;", "export variable c;", "procedure start begin", "end");

			Assert.Equal(new[] { "a" }, Names(Find(text)));
			Assert.Empty(Find(text, new UnusedVariableOptions { IncludeGlobals = false }));
		}

		[Fact]
		public void Find_ParametersAreOnlyReportedOnRequest()
		{
			String text = Ssl.Crlf("procedure helper(variable a, variable b) begin", "\treturn a;", "end");

			Assert.Empty(Find(text));
			UnusedVariable variable = Assert.Single(Find(text, new UnusedVariableOptions { IncludeParameters = true }));
			Assert.Equal("b", variable.Name);
			Assert.Equal(VariableDeclarationKind.Parameter, variable.Kind);
			Assert.False(variable.CanRemove);
		}

		[Fact]
		public void Find_ForwardDeclarationsDoNotMakeUnusedParameters()
		{
			String text = Ssl.Crlf("procedure helper(variable a);", "procedure helper(variable a) begin", "\treturn a;", "end");

			Assert.Empty(Find(text, new UnusedVariableOptions { IncludeParameters = true }));
		}

		[Fact]
		public void Find_AMacroBodyThatMentionsTheNameCountsAsAUse()
		{
			String text = Ssl.Crlf("#define BUMP counter += 1", "procedure start begin", "\tvariable counter;", "\tBUMP;", "end");

			Assert.Empty(Find(text));
		}

		[Fact]
		public void Find_WordsOfTheIncludedMacrosCountAsUses()
		{
			String text = Ssl.Crlf("procedure start begin", "\tvariable counter, other;", "\tHEADER_MACRO;", "end");
			HashSet<String> words = new HashSet<String>(StringComparer.OrdinalIgnoreCase) { "COUNTER" };

			UnusedVariable variable = Assert.Single(Find(text, new UnusedVariableOptions { IsMacroWord = words.Contains }));

			Assert.Equal("other", variable.Name);
		}

		[Fact]
		public void Find_LoopVariablesInParenthesesAreReportedButCannotBeRemoved()
		{
			String text = Ssl.Crlf(
				"procedure start begin",
				"\tvariable arr = [1];",
				"\tforeach (variable item in arr) begin",
				"\tend",
				"end");

			UnusedVariable variable = Assert.Single(Find(text));

			Assert.Equal("item", variable.Name);
			Assert.False(variable.CanRemove);
		}

		[Fact]
		public void Find_AnInitializerThatCallsSomethingCannotBeRemoved()
		{
			String text = Ssl.Crlf(
				"procedure start begin",
				"\tvariable plain = 5;",
				"\tvariable names = \"a\" + \"b\";",
				"\tvariable object = dude_obj;",
				"\tvariable called = random(1, 5);",
				"\tvariable incremented = counter++;",
				"end");

			IReadOnlyList<UnusedVariable> result = Find(text);

			Assert.Equal(new[] { "plain", "names", "object", "called", "incremented" }, Names(result));
			Assert.Equal(new[] { true, true, true, false, false }, result.Select(v => v.CanRemove).ToArray());
		}

		[Fact]
		public void Find_ModernScriptWithAssignmentsByEqualsSign()
		{
			String text = Ssl.Crlf(
				"procedure get_active_weapon(variable attacker) begin",
				"    variable item;",
				"    if (attacker == dude_obj and active_hand == LEFT_HAND) then begin",
				"        item = critter_inven_obj2(attacker, INVEN_TYPE_LEFT_HAND);",
				"    end else begin",
				"        item = critter_inven_obj2(attacker, INVEN_TYPE_RIGHT_HAND);",
				"    end",
				"    if (item and is_weapon(item)) then begin",
				"        return item;",
				"    end",
				"    return 0;",
				"end");

			Assert.Empty(Find(text, new UnusedVariableOptions { IncludeParameters = true }));
		}

		[Fact]
		public void Find_WorksTheSameWithLineFeedOnlyText()
		{
			String crlf = Ssl.Crlf("procedure start begin", "\tvariable unused;", "end");

			UnusedVariable crlfResult = Assert.Single(Find(crlf));
			UnusedVariable lfResult = Assert.Single(Find(Ssl.ToLf(crlf)));

			Assert.Equal(crlfResult.Line, lfResult.Line);
			Assert.Equal(crlfResult.Column, lfResult.Column);
		}

		[Fact]
		public void FindAt_FindsTheVariableUnderTheCaret()
		{
			String text = Ssl.Crlf("procedure start begin", "\tvariable a, b;", "\tdebug_msg(a);", "end");
			Int32 offset = text.IndexOf("b;", StringComparison.Ordinal);

			Assert.Equal("b", UnusedVariableFinder.FindAt(text, offset)?.Name);
			Assert.Equal("b", UnusedVariableFinder.FindAt(text, offset + 1)?.Name);
			Assert.Null(UnusedVariableFinder.FindAt(text, text.IndexOf("a,", StringComparison.Ordinal)));
		}

		[Fact]
		public void Find_ReturnsTheVariablesInTextOrder()
		{
			String text = Ssl.Crlf("variable g;", "procedure a begin", "\tvariable x;", "end", "procedure b begin", "\tvariable y;", "end");

			Assert.Equal(new[] { "g", "x", "y" }, Names(Find(text)));
		}

		[Fact]
		public void Find_NeverThrowsOnBrokenText()
		{
			String[] broken = { "procedure a begin variable x", "variable", "procedure begin", "procedure a(variable x) begin x = ", "end end", String.Empty };

			foreach (String text in broken)
			{
				Assert.NotNull(Find(text));
			}
		}
	}
}
