namespace FalloutScriptStudio.Core.Tests.TextTools
{
	using System;
	using FalloutScriptStudio.Core.TextTools;
	using Xunit;

	public class ProcedureNameParserTests
	{
		[Theory]
		[InlineData("Node001", "Node001", "Node001")]
		[InlineData("  Helper  ", "Helper", "Helper")]
		[InlineData("Helper()", "Helper", "Helper")]
		[InlineData("Helper(a, b)", "Helper", "Helper(variable a, variable b)")]
		[InlineData("Helper(variable a,variable b)", "Helper", "Helper(variable a, variable b)")]
		[InlineData("Helper( VARIABLE a )", "Helper", "Helper(variable a)")]
		[InlineData("Helper(a,,b)", "Helper", "Helper(variable a, variable b)")]
		public void Parse_AcceptsNamesWithArguments(String input, String name, String declaration)
		{
			ParsedProcedureName result = ProcedureNameParser.Parse(input, true);

			Assert.True(result.IsValid);
			Assert.Equal(name, result.Name);
			Assert.Equal(declaration, result.Declaration);
			Assert.Null(result.Error);
		}

		[Theory]
		[InlineData("")]
		[InlineData("   ")]
		[InlineData("bad name!")]
		[InlineData("a-b")]
		[InlineData("Foo(a")]
		[InlineData("Foo(a))")]
		[InlineData("Foo((a)")]
		[InlineData("Foo(a-b)")]
		[InlineData("begin")]
		[InlineData("Procedure")]
		[InlineData("(a)")]
		public void Parse_RejectsInvalidInput(String input)
		{
			ParsedProcedureName result = ProcedureNameParser.Parse(input, true);

			Assert.False(result.IsValid);
			Assert.NotNull(result.Error);
		}

		[Fact]
		public void Parse_ArgumentsNotAllowed_RejectsThem()
		{
			Assert.False(ProcedureNameParser.Parse("Foo(a)", false).IsValid);
			Assert.True(ProcedureNameParser.Parse("Foo", false).IsValid);
		}

		[Theory]
		[InlineData("Node009", "Node010")]
		[InlineData("Node999", "Node1000")]
		[InlineData("Node", "Node")]
		[InlineData("a1b2", "a1b3")]
		[InlineData("x00", "x01")]
		public void IncrementTrailingNumber_KeepsWidth(String name, String expected)
		{
			Assert.Equal(expected, ProcedureNameParser.IncrementTrailingNumber(name));
		}
	}
}
