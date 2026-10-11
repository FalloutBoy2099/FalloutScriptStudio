namespace FalloutScriptStudio.Core.Tests.TextTools
{
	using System;
	using FalloutScriptStudio.Core.TextTools;
	using Xunit;

	public class IncludeInserterTests
	{
		private readonly IncludeInserter _inserter = new IncludeInserter();

		[Fact]
		public void CreateInclude_CaretOutsideRegion_GoesAfterTheLastInclude()
		{
			String text = TestScripts.DialogueScript();

			IncludePasteResult result = this._inserter.CreateInclude(text, "..\\headers\\sfall.h", 20);

			String[] lines = TextEditApplier.Apply(text, new[] { result.Edit }).Split("\r\n");
			Assert.Equal("#include \"define.h\"", lines[1]);
			Assert.Equal("#include \"..\\headers\\sfall.h\"", lines[2]);
			Assert.Equal("#define NAME SCRIPT_TEST", lines[3]);
			Assert.Equal(3, result.Line);
		}

		[Fact]
		public void CreateInclude_CaretInsideRegion_GoesAtTheCaretLine()
		{
			String text = TestScripts.DialogueScript();

			IncludePasteResult result = this._inserter.CreateInclude(text, "x.h", 6);

			String[] lines = TextEditApplier.Apply(text, new[] { result.Edit }).Split("\r\n");
			Assert.Equal("#include \"x.h\"", lines[5]);
			Assert.Equal("procedure talk_p_proc;", lines[6]);
		}

		[Fact]
		public void CreateInclude_IncludeOnFirstLine_IsFound()
		{
			String text = TestScripts.Crlf("#include \"a.h\"", "", "procedure start begin", "end");

			IncludePasteResult result = this._inserter.CreateInclude(text, "b.h", 4);

			Assert.StartsWith("#include \"a.h\"\r\n#include \"b.h\"\r\n", TextEditApplier.Apply(text, new[] { result.Edit }));
		}

		[Fact]
		public void CreateInclude_NoIncludeYet_GoesToTheStartOfTheDeclarationRegion()
		{
			String text = TestScripts.Crlf("// header", "#define A 1", "", "procedure start begin", "end");

			IncludePasteResult result = this._inserter.CreateInclude(text, "b.h", 5);

			String[] lines = TextEditApplier.Apply(text, new[] { result.Edit }).Split("\r\n");
			Assert.Equal("// header", lines[0]);
			Assert.Equal("#include \"b.h\"", lines[1]);
		}

		[Fact]
		public void CreateInclude_ScriptWithoutProcedures_AppendsWhenNothingElse()
		{
			IncludePasteResult result = this._inserter.CreateInclude(String.Empty, "a.h", 1);

			Assert.Equal("#include \"a.h\"\r\n", TextEditApplier.Apply(String.Empty, new[] { result.Edit }));
		}

		[Fact]
		public void CreateInclude_UsesTheNewLineOfTheScript()
		{
			String text = "#include \"a.h\"\n\nprocedure start begin\nend";

			IncludePasteResult result = this._inserter.CreateInclude(text, "b.h", 4);

			Assert.StartsWith("#include \"a.h\"\n#include \"b.h\"\n", TextEditApplier.Apply(text, new[] { result.Edit }));
		}
	}
}
