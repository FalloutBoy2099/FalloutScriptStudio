namespace FalloutScriptStudio.Tests.Editor
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Editor.Completion;
	using ICSharpCode.AvalonEdit.Document;
	using Xunit;

	public sealed class CompletionSourceTests
	{
		private static readonly OpcodeCatalog Opcodes = new OpcodeCatalog(new[]
		{
			"void display_msg(string msg) | Shows a message.",
			"void display_stats() | Shows the stats.",
			"int random(int min, int max) | A random number.",
		});

		[Fact]
		public void GetPrefix_ReturnsTheIdentifierBeforeTheCaret()
		{
			String prefix = CompletionSource.GetPrefix(new StringTextSource("x := disp"), 9, out Int32 start);

			Assert.Equal("disp", prefix);
			Assert.Equal(5, start);
		}

		[Fact]
		public void GetPrefix_AfterPunctuation_IsEmpty()
		{
			Assert.Equal(String.Empty, CompletionSource.GetPrefix(new StringTextSource("foo("), 4, out _));
		}

		[Fact]
		public void GetItems_ShorterThanTwoCharacters_ReturnsNothing()
		{
			Assert.Empty(CompletionSource.GetItems("d", null, Opcodes));
		}

		[Fact]
		public void GetItems_ExplicitRequest_AcceptsOneCharacter()
		{
			Assert.Equal(2, CompletionSource.GetItems("d", null, Opcodes, 1).Count);
		}

		[Fact]
		public void GetItems_WithoutParseResult_ListsOpcodes()
		{
			IReadOnlyList<AutosuggestItem> items = CompletionSource.GetItems("disp", null, Opcodes);

			Assert.Equal(new[] { "display_msg", "display_stats" }, items.Select(i => i.Name).ToArray());
			Assert.All(items, item => Assert.Equal(AutosuggestKind.Opcode, item.Kind));
		}

		[Fact]
		public void GetItems_WithParseResult_AddsProceduresAndMacros()
		{
			ProgramInfo info = new InternalParser().Parse("procedure display_all begin\r\nend\r\n", "C:\\scripts\\t.ssl")
				.WithScriptMacros(new Dictionary<String, ScriptMacro> { ["DISPLAY_LIMIT"] = new ScriptMacro("DISPLAY_LIMIT", "DISPLAY_LIMIT", "5", "C:\\scripts\\t.ssl", 1, null) });

			IReadOnlyList<AutosuggestItem> items = CompletionSource.GetItems("disp", info, Opcodes);

			Assert.Contains(items, i => i.Kind == AutosuggestKind.Procedure && i.Name == "display_all");
			Assert.Contains(items, i => i.Kind == AutosuggestKind.Macro && i.Name.StartsWith("DISPLAY_LIMIT", StringComparison.Ordinal));
			Assert.Contains(items, i => i.Kind == AutosuggestKind.Opcode);
		}

		[Fact]
		public void GetItems_ExactSingleMatch_ReturnsNothing()
		{
			Assert.Empty(CompletionSource.GetItems("random", null, Opcodes));
		}

		[Fact]
		public void GetItems_NumberPrefix_ReturnsNothing()
		{
			Assert.Empty(CompletionSource.GetItems("12", null, Opcodes));
		}
	}
}