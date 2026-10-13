namespace FalloutScriptStudio.Core.Tests.Analysis
{
	using System;
	using System.Linq;
	using FalloutScriptStudio.Core.Analysis;
	using Xunit;

	public class SslScriptModelTests
	{
		private static readonly String Script = Ssl.Crlf(
			"#include \"define.h\"",
			"#define MAX(a, b) ((a) > (b) ? (a) : (b))",
			"procedure start;",
			"procedure helper(variable a, variable b := 2);",
			"import procedure other;",
			"variable counter;",
			"variable total := 0, limit := MAX(1, 2);",
			"export variable shared := 5;",
			"import variable remote;",
			"",
			"procedure start() begin",
			"\tvariable local1, local2 := 3;",
			"\tif (counter > 0) then begin",
			"\t\tvariable inner;",
			"\t\tinner = 1;",
			"\tend else begin",
			"\t\tcounter = 0;",
			"\tend",
			"end",
			"",
			"procedure helper(variable a, variable b := 2) begin",
			"\treturn a + b;",
			"end");

		[Fact]
		public void Analyze_FindsTheProceduresThatHaveABody()
		{
			SslScriptModel model = SslScriptModel.Analyze(Script);

			Assert.Equal(new[] { "start", "helper" }, model.Procedures.Select(p => p.Name).ToArray());
			Assert.All(model.Procedures, p => Assert.True(p.IsClosed));
		}

		[Fact]
		public void Analyze_FindsEveryKindOfDeclaration_InTextOrder_AndIgnoresTheParametersOfForwardDeclarations()
		{
			SslScriptModel model = SslScriptModel.Analyze(Script);

			(String Name, VariableDeclarationKind Kind, String? Procedure)[] found = model.Declarations.Select(d => (d.Name, d.Kind, d.ProcedureName)).ToArray();

			Assert.Equal(
				new (String, VariableDeclarationKind, String?)[]
				{
					("counter", VariableDeclarationKind.Global, null),
					("total", VariableDeclarationKind.Global, null),
					("limit", VariableDeclarationKind.Global, null),
					("shared", VariableDeclarationKind.Export, null),
					("remote", VariableDeclarationKind.Import, null),
					("local1", VariableDeclarationKind.Local, "start"),
					("local2", VariableDeclarationKind.Local, "start"),
					("inner", VariableDeclarationKind.Local, "start"),
					("a", VariableDeclarationKind.Parameter, "helper"),
					("b", VariableDeclarationKind.Parameter, "helper"),
				},
				found);
		}

		[Fact]
		public void Analyze_GivesTheLineAndColumnOfEachName()
		{
			SslScriptModel model = SslScriptModel.Analyze(Script);

			VariableDeclaration local2 = model.Declarations.Single(d => d.Name == "local2");
			Assert.Equal(12, local2.Line);
			Assert.Equal(19, local2.Column);
			Assert.Equal(Script.IndexOf("local2", StringComparison.Ordinal), local2.Offset);
		}

		[Fact]
		public void Analyze_ADeclarationKnowsItsInitializer()
		{
			SslScriptModel model = SslScriptModel.Analyze(Script);

			Assert.False(model.Declarations.Single(d => d.Name == "counter").HasInitializer);
			VariableDeclaration total = model.Declarations.Single(d => d.Name == "total");
			Assert.True(total.HasInitializer);
			Assert.False(total.InitializerHasSideEffects);
			Assert.Equal("total := 0", Script.Substring(total.ItemStart, total.ItemEnd - total.ItemStart));
			VariableDeclaration limit = model.Declarations.Single(d => d.Name == "limit");
			Assert.True(limit.InitializerHasSideEffects);
			Assert.Equal("limit := MAX(1, 2)", Script.Substring(limit.ItemStart, limit.ItemEnd - limit.ItemStart));
		}

		[Fact]
		public void Analyze_ADeclarationStatementGroupsItsVariables()
		{
			SslScriptModel model = SslScriptModel.Analyze(Script);

			DeclarationStatement statement = model.Declarations.Single(d => d.Name == "local1").Statement;
			Assert.Equal(new[] { "local1", "local2" }, statement.Items.Select(i => i.Name).ToArray());
			Assert.True(statement.IsTerminated);
			Assert.Equal("variable local1, local2 := 3;", Script.Substring(statement.Offset, statement.End - statement.Offset));
		}

		[Fact]
		public void Analyze_AModifierBelongsToTheStatement()
		{
			SslScriptModel model = SslScriptModel.Analyze(Script);

			DeclarationStatement statement = model.Declarations.Single(d => d.Name == "shared").Statement;
			Assert.Equal("export variable shared := 5;", Script.Substring(statement.Offset, statement.End - statement.Offset));
		}

		[Fact]
		public void Analyze_ParametersAreNotInsideBraces()
		{
			SslScriptModel model = SslScriptModel.Analyze(Script);

			Assert.All(model.Declarations.Where(d => d.Kind == VariableDeclarationKind.Parameter), d => Assert.True(d.IsInParentheses));
			Assert.All(model.Declarations.Where(d => d.Kind == VariableDeclarationKind.Local), d => Assert.False(d.IsInParentheses));
		}

		[Fact]
		public void Analyze_AProcedureWithoutEndIsClosedByTheNextProcedure()
		{
			String text = Ssl.Crlf(
				"procedure first begin",
				"\tvariable a;",
				"\tif (1) then begin",
				"procedure second begin",
				"\tvariable b;",
				"end");

			SslScriptModel model = SslScriptModel.Analyze(text);

			Assert.Equal(new[] { "first", "second" }, model.Procedures.Select(p => p.Name).ToArray());
			Assert.False(model.Procedures[0].IsClosed);
			Assert.True(model.Procedures[1].IsClosed);
			Assert.Equal("second", model.Declarations.Single(d => d.Name == "b").ProcedureName);
		}

		[Fact]
		public void Analyze_AMacroThatMentionsBeginDoesNotChangeTheBody()
		{
			String text = Ssl.Crlf(
				"#define OPEN begin",
				"procedure a begin",
				"\tvariable x;",
				"end",
				"procedure b begin",
				"\tvariable y;",
				"end");

			SslScriptModel model = SslScriptModel.Analyze(text);

			Assert.Equal(new[] { "a", "b" }, model.Procedures.Select(p => p.Name).ToArray());
			Assert.All(model.Procedures, p => Assert.True(p.IsClosed));
		}

		[Fact]
		public void Analyze_ForeachVariablesAreDeclarationsInsideParentheses()
		{
			String text = Ssl.Crlf(
				"procedure a begin",
				"\tvariable arr = [1, 2];",
				"\tforeach (variable item in arr) begin",
				"\t\tdebug_msg(item);",
				"\tend",
				"end");

			SslScriptModel model = SslScriptModel.Analyze(text);

			VariableDeclaration item = model.Declarations.Single(d => d.Name == "item");
			Assert.True(item.IsInParentheses);
			Assert.Equal(VariableDeclarationKind.Local, item.Kind);
			Assert.True(model.Declarations.Single(d => d.Name == "arr").HasInitializer);
		}

		[Fact]
		public void Analyze_SupportsPlainEqualsInInitializers_AndArrayLengths()
		{
			String text = Ssl.Crlf("variable weapon = get_active_weapon(attacker);", "variable table[10];", "variable after;");

			SslScriptModel model = SslScriptModel.Analyze(text);

			Assert.Equal(new[] { "weapon", "table", "after" }, model.Declarations.Select(d => d.Name).ToArray());
			Assert.True(model.Declarations[0].InitializerHasSideEffects);
			Assert.Equal("table[10]", text.Substring(model.Declarations[1].ItemStart, model.Declarations[1].ItemEnd - model.Declarations[1].ItemStart));
		}

		[Fact]
		public void Analyze_FindProcedureAt_FindsTheProcedureOfAToken()
		{
			SslScriptModel model = SslScriptModel.Analyze(Script);
			Int32 inner = Enumerable.Range(0, model.Tokens.Count).First(i => model.Tokens.GetText(i) == "inner");

			Assert.Equal("start", model.FindProcedureAt(inner)?.Name);
			Assert.Null(model.FindProcedureAt(0));
		}

		[Fact]
		public void Analyze_NeverThrowsOnBrokenText()
		{
			String[] broken =
			{
				"procedure", "procedure foo(", "procedure foo(variable", "variable", "variable ;", "import", "export variable", "procedure foo begin variable",
				"begin end end end", "procedure a begin end end", "variable a := (", "variable a := [", "procedure a(variable b := ( begin",
			};
			foreach (String text in broken)
			{
				SslScriptModel model = SslScriptModel.Analyze(text);
				Assert.NotNull(model);
			}
		}
	}
}
