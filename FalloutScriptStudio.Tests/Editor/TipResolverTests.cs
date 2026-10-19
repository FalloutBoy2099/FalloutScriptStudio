namespace FalloutScriptStudio.Tests.Editor
{
	using System;
	using System.Collections.Generic;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Editor.Tips;
	using Xunit;

	public sealed class TipResolverTests
	{
		private static readonly OpcodeCatalog Opcodes = new OpcodeCatalog(new[]
		{
			"void display_msg(string msg) | Shows a message on the screen. || Second paragraph.",
			"int random(int min, int max) | Returns a random number.",
		});

		private static ProgramInfo CreateInfo(String text)
		{
			return new InternalParser().Parse(text, "C:\\scripts\\test.ssl");
		}

		[Fact]
		public void ResolveWord_Opcode_UsesFirstLineAsTitle()
		{
			TipInfo? tip = TipResolver.ResolveWord("display_msg", 3, null, null, Opcodes, null);

			Assert.NotNull(tip);
			Assert.Equal(TipKind.Opcode, tip.Kind);
			Assert.StartsWith("void display_msg", tip.Title, StringComparison.Ordinal);
			Assert.Contains("Shows a message", tip.Body, StringComparison.Ordinal);
		}

		[Fact]
		public void ResolveWord_OpcodeLookupIsCaseInsensitive()
		{
			Assert.NotNull(TipResolver.ResolveWord("DISPLAY_MSG", 1, null, null, Opcodes, null));
		}

		[Fact]
		public void ResolveWord_Procedure_DescribesTheSignature()
		{
			ProgramInfo info = CreateInfo("procedure foo;\r\n\r\nprocedure foo begin\r\nend\r\n");

			TipInfo? tip = TipResolver.ResolveWord("Foo", 1, "C:\\scripts\\test.ssl", info, Opcodes, null);

			Assert.NotNull(tip);
			Assert.Equal(TipKind.Procedure, tip.Kind);
			Assert.Contains("foo", tip.Title, StringComparison.OrdinalIgnoreCase);
		}

		[Fact]
		public void ResolveWord_Macro_ShowsDefineAndBody()
		{
			ProgramInfo info = new ProgramInfo(
				Array.Empty<ScriptProcedure>(),
				Array.Empty<ScriptVariable>(),
				new Dictionary<String, ScriptMacro> { ["MAX_HP"] = new ScriptMacro("MAX_HP", "MAX_HP", "100", "C:\\scripts\\test.ssl", 1, null) });

			TipInfo? tip = TipResolver.ResolveWord("MAX_HP", 4, "C:\\scripts\\test.ssl", info, Opcodes, null);

			Assert.NotNull(tip);
			Assert.Equal(TipKind.Macro, tip.Kind);
			Assert.StartsWith("Define: MAX_HP", tip.Title, StringComparison.Ordinal);
			Assert.Contains("100", tip.Body, StringComparison.Ordinal);
		}

		[Fact]
		public void ResolveWord_MessageId_ReturnsMessageText()
		{
			TipInfo? tip = TipResolver.ResolveWord("105", 5, null, null, Opcodes, id => id == 105 ? "Hello there" : null);

			Assert.NotNull(tip);
			Assert.Equal(TipKind.Message, tip.Kind);
			Assert.Equal("Message 105", tip.Title);
			Assert.Equal("\"Hello there\"", tip.Body);
		}

		[Fact]
		public void ResolveWord_UnknownMessageId_FallsThrough()
		{
			Assert.Null(TipResolver.ResolveWord("999", 5, null, null, Opcodes, _ => null));
		}

		[Fact]
		public void ResolveWord_Unknown_ReturnsNull()
		{
			Assert.Null(TipResolver.ResolveWord("nothing_here", 1, null, CreateInfo("procedure start begin\r\nend\r\n"), Opcodes, null));
		}

		[Fact]
		public void ResolveInclude_ExistingFile_ShowsResolvedPath()
		{
			IncludeResolver resolver = new IncludeResolver(null, null);

			TipInfo? tip = TipResolver.ResolveInclude("#include \"..\\headers\\define.h\"", resolver, "C:\\scripts\\sub", _ => true);

			Assert.NotNull(tip);
			Assert.Equal(TipKind.Include, tip.Kind);
			Assert.EndsWith("define.h", tip.Body, StringComparison.OrdinalIgnoreCase);
		}

		[Fact]
		public void ResolveInclude_MissingFile_SaysSo()
		{
			IncludeResolver resolver = new IncludeResolver(null, null);

			TipInfo? tip = TipResolver.ResolveInclude("#include \"x.h\"", resolver, "C:\\scripts", _ => false);

			Assert.NotNull(tip);
			Assert.StartsWith("File not found", tip.Body, StringComparison.Ordinal);
		}

		[Fact]
		public void ResolveInclude_NotAnIncludeLine_ReturnsNull()
		{
			Assert.Null(TipResolver.ResolveInclude("foo(\"x.h\");", new IncludeResolver(null, null), null, _ => true));
		}

		[Fact]
		public void ResolveCodeTip_Opcode_IsTheSignatureLine()
		{
			TipInfo? tip = TipResolver.ResolveCodeTip("random", null, Opcodes);

			Assert.NotNull(tip);
			Assert.StartsWith("int random(int min, int max)", tip.Title, StringComparison.Ordinal);
			Assert.Equal(String.Empty, tip.Body);
		}

		[Theory]
		[InlineData("if", true)]
		[InlineData("foreach", true)]
		[InlineData("else", false)]
		[InlineData("foo", false)]
		public void IsBlockKeyword_ListsTheTabTemplates(String word, Boolean expected)
		{
			Assert.Equal(expected, TipResolver.IsBlockKeyword(word));
		}

		[Fact]
		public void TipInfo_FromDescription_SplitsAtTheFirstLine()
		{
			TipInfo tip = TipInfo.FromDescription("title  \r\nline 1\nline 2\n", TipKind.Hint);

			Assert.Equal("title", tip.Title);
			Assert.Equal("line 1\nline 2", tip.Body);
		}
	}
}