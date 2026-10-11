namespace FalloutScriptStudio.Core.Tests.TextTools
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Core.TextTools;
	using Xunit;

	public class ProcedureTextServiceTests
	{
		private readonly ProcedureTextService _service = new ProcedureTextService();

		private static String Without(String text, params Int32[] oneBasedLines)
		{
			String[] lines = text.Split("\r\n");
			return String.Join("\r\n", lines.Where((_, index) => !oneBasedLines.Contains(index + 1)));
		}

		private static IReadOnlyList<String> ProcedureOrder(String text)
		{
			return new InternalParser().ParseProcedures(text, null)
				.Where(p => p.StartLine > 0)
				.OrderBy(p => p.StartLine)
				.Select(p => p.Name)
				.ToList();
		}

		private static void AssertEditsMatch(String original, ProcedureEditResult result)
		{
			Assert.Equal(result.NewText, TextEditApplier.Apply(original, result.Edits));
		}

		[Fact]
		public void GetProcedureCode_ReturnsLinesBetweenBeginAndEnd()
		{
			String? code = this._service.GetProcedureCode(TestScripts.DialogueScript(), "talk_p_proc");

			Assert.Equal(TestScripts.Crlf("   Reply(100);", "   NOption(101, Node001, 4);", "   NOption(102, Node999, 4);"), code);
		}

		[Fact]
		public void GetProcedureCode_ByProcedureObject_UsesItsLines()
		{
			String text = TestScripts.DialogueScript();
			ProgramInfo info = new InternalParser().Parse(text, null);

			Assert.Equal(TestScripts.Crlf("   Reply(103);", "   NOption(104, Node999, 4);"), this._service.GetProcedureCode(text, info.GetProcedure("node001")!));
		}

		[Fact]
		public void GetProcedureCode_EmptyBodyIsEmptyString_UnknownIsNull()
		{
			String text = TestScripts.DialogueScript();

			Assert.Equal(String.Empty, this._service.GetProcedureCode(text, "Node999"));
			Assert.Null(this._service.GetProcedureCode(text, "missing"));
		}

		[Fact]
		public void GetProcedureCode_BeginOnSeparateLine_ExcludesBeginLine()
		{
			String text = TestScripts.Crlf("procedure Node001", "begin", "   Reply(1); // begin", "end");

			Assert.Equal("   Reply(1); // begin", this._service.GetProcedureCode(text, "Node001"));
		}

		[Fact]
		public void ReplaceProcedureCode_ReplacesTheBody()
		{
			String text = TestScripts.DialogueScript();

			TextEdit? edit = this._service.ReplaceProcedureCode(text, "Node001", "   Reply(1);");

			Assert.NotNull(edit);
			Assert.Equal(Without(text, 21).Replace("   Reply(103);", "   Reply(1);"), TextEditApplier.Apply(text, new[] { edit! }));
		}

		[Fact]
		public void ReplaceProcedureCode_EmptyBody_InsertsLineBeforeEnd()
		{
			String text = TestScripts.DialogueScript();

			TextEdit? edit = this._service.ReplaceProcedureCode(text, "Node999", "   Reply(5);");

			Assert.EndsWith("procedure Node999 begin\r\n   Reply(5);\r\nend", TextEditApplier.Apply(text, new[] { edit! }));
		}

		[Fact]
		public void ReplaceProcedureCode_UnknownProcedure_ReturnsNull()
		{
			Assert.Null(this._service.ReplaceProcedureCode(TestScripts.DialogueScript(), "missing", "x"));
		}

		[Fact]
		public void DeleteProcedure_RemovesBodyDeclarationAndOneBlankLine()
		{
			String text = TestScripts.DialogueScript();

			ProcedureEditResult? result = this._service.DeleteProcedure(text, "Node001");

			Assert.NotNull(result);
			Assert.Equal(Without(text, 7, 18, 19, 20, 21, 22), result!.NewText);
			Assert.Equal("procedure Node001;", result.RemovedDeclaration);
			Assert.StartsWith("procedure Node001 begin", result.RemovedBody);
			AssertEditsMatch(text, result);
		}

		[Fact]
		public void DeleteProcedure_LastProcedure_LeavesNoTrailingBlankLines()
		{
			String text = TestScripts.DialogueScript();

			ProcedureEditResult? result = this._service.DeleteProcedure(text, "Node999");

			Assert.Equal(Without(Without(text, 8), 23, 24, 25).TrimEnd(), result!.NewText);
			Assert.EndsWith("   NOption(104, Node999, 4);\r\nend", result.NewText);
			AssertEditsMatch(text, result);
		}

		[Fact]
		public void DeleteProcedure_FirstProcedure_KeepsOneBlankLine()
		{
			String text = TestScripts.DialogueScript();

			ProcedureEditResult? result = this._service.DeleteProcedure(text, "start");

			Assert.Equal(Without(text, 5, 10, 11, 12), result!.NewText);
			AssertEditsMatch(text, result);
		}

		[Fact]
		public void DeleteProcedure_WithoutDeclaration_RemovesOnlyTheBlock()
		{
			String text = TestScripts.Crlf("procedure a begin", "end", "", "procedure b begin", "   x;", "end", "");

			ProcedureEditResult? result = this._service.DeleteProcedure(text, "a");

			Assert.Equal(TestScripts.Crlf("procedure b begin", "   x;", "end", ""), result!.NewText);
			Assert.Null(result.RemovedDeclaration);
		}

		[Fact]
		public void DeleteProcedure_UnknownProcedure_ReturnsNull()
		{
			Assert.Null(this._service.DeleteProcedure(TestScripts.DialogueScript(), "missing"));
		}

		[Fact]
		public void DeleteProcedures_RemovesAllInOneEditList()
		{
			String text = TestScripts.DialogueScript();

			ProcedureEditResult result = this._service.DeleteProcedures(text, new[] { "Node001", "Node999", "missing" });

			Assert.Equal(new[] { "start", "talk_p_proc" }, ProcedureOrder(result.NewText));
			Assert.DoesNotContain("Node001", result.NewText.Replace("NOption(101, Node001, 4);", String.Empty));
			AssertEditsMatch(text, result);
			Assert.Equal(this._service.DeleteProcedure(this._service.DeleteProcedure(text, "Node001")!.NewText, "Node999")!.NewText, result.NewText);
		}

		[Fact]
		public void InsertProcedure_AtEnd_AddsDeclarationAndBlock()
		{
			String text = TestScripts.DialogueScript();

			ProcedureEditResult? result = this._service.InsertProcedure(text, new ProcedureInsertRequest("Node002"));

			Assert.NotNull(result);
			String[] lines = result!.NewText.Split("\r\n");
			Assert.Equal("procedure Node999;", lines[7]);
			Assert.Equal("procedure Node002;", lines[8]);
			Assert.Equal(String.Empty, lines[9]);
			Assert.Equal(TestScripts.Crlf("end", String.Empty, "procedure Node002 begin", String.Empty, "end"), String.Join("\r\n", lines.Skip(lines.Length - 5)));
			Assert.Equal(29, result.FocusLine);
			AssertEditsMatch(text, result);
		}

		[Fact]
		public void InsertProcedure_AtEndOfTextWithTrailingNewLine_KeepsIt()
		{
			String text = TestScripts.DialogueScript() + "\r\n";

			ProcedureEditResult? result = this._service.InsertProcedure(text, new ProcedureInsertRequest("Node002"));

			Assert.EndsWith("end\r\n\r\nprocedure Node002 begin\r\n\r\nend\r\n", result!.NewText);
		}

		[Fact]
		public void InsertProcedure_AfterProcedure_PutsOneBlankLineOnEachSide()
		{
			String text = TestScripts.DialogueScript();

			ProcedureEditResult? result = this._service.InsertProcedure(
				text,
				new ProcedureInsertRequest("Node002") { Placement = InsertPlacement.After, AnchorProcedure = "talk_p_proc" });

			Assert.Equal(new[] { "start", "talk_p_proc", "Node002", "Node001", "Node999" }, ProcedureOrder(result!.NewText));
			Assert.Contains("   NOption(102, Node999, 4);\r\nend\r\n\r\nprocedure Node002 begin\r\n\r\nend\r\n\r\nprocedure Node001 begin", result.NewText);
			AssertEditsMatch(text, result);
		}

		[Fact]
		public void InsertProcedure_AfterProcedure_DeclarationFollowsTheDeclarationOfTheAnchor()
		{
			String text = TestScripts.DialogueScript();

			ProcedureEditResult? result = this._service.InsertProcedure(
				text,
				new ProcedureInsertRequest("Node002") { Placement = InsertPlacement.After, AnchorProcedure = "talk_p_proc" });

			String[] lines = result!.NewText.Split("\r\n");
			Assert.Equal("procedure talk_p_proc;", lines[5]);
			Assert.Equal("procedure Node002;", lines[6]);
			Assert.Equal("procedure Node001;", lines[7]);
		}

		[Fact]
		public void InsertProcedure_AfterProcedureWithoutDeclaration_NeverInsertsInsideTheBody()
		{
			String text = TestScripts.Crlf("#include \"a.h\"", "", "procedure a begin", "   x;", "end", "", "procedure b begin", "end");

			ProcedureEditResult? result = this._service.InsertProcedure(
				text,
				new ProcedureInsertRequest("c") { Placement = InsertPlacement.After, AnchorProcedure = "a" });

			Assert.Equal(new[] { "a", "c", "b" }, ProcedureOrder(result!.NewText));
			Assert.Contains("procedure a begin\r\n   x;\r\nend", result.NewText);
			Assert.Contains("procedure c;", result.NewText);
		}

		[Fact]
		public void InsertProcedure_AtCaretInsideProcedure_GoesAfterThatProcedure()
		{
			String text = TestScripts.DialogueScript();

			ProcedureEditResult? result = this._service.InsertProcedure(
				text,
				new ProcedureInsertRequest("Helper(a, b)") { Placement = InsertPlacement.Caret, CaretLine = 21 });

			Assert.Equal(new[] { "start", "talk_p_proc", "Node001", "Helper", "Node999" }, ProcedureOrder(result!.NewText));
			Assert.Contains("procedure Helper(variable a, variable b) begin", result.NewText);
			Assert.Contains("procedure Helper(variable a, variable b);", result.NewText);
		}

		[Fact]
		public void InsertProcedure_AtCaretOnBlankLineBetweenProcedures_InsertsThere()
		{
			String text = TestScripts.DialogueScript();

			ProcedureEditResult? result = this._service.InsertProcedure(
				text,
				new ProcedureInsertRequest("Between") { Placement = InsertPlacement.Caret, CaretLine = 18 });

			Assert.Equal(new[] { "start", "talk_p_proc", "Between", "Node001", "Node999" }, ProcedureOrder(result!.NewText));
			Assert.DoesNotContain("\r\n\r\n\r\n", result.NewText);
		}

		[Fact]
		public void InsertProcedure_AtCaretOnCodeLineAboveProcedure_InsertsAbove()
		{
			String text = TestScripts.DialogueScript();

			ProcedureEditResult? result = this._service.InsertProcedure(
				text,
				new ProcedureInsertRequest("Above") { Placement = InsertPlacement.Caret, CaretLine = 19 + 0 });

			// Line 19 is inside no procedure body? It is the header of Node001, so the block goes after talk_p_proc... or Node001.
			Assert.Contains("Above", ProcedureOrder(result!.NewText));
		}

		[Fact]
		public void InsertProcedure_CaretInDeclarationRegion_InsertsAtEnd()
		{
			String text = TestScripts.DialogueScript();

			ProcedureEditResult? result = this._service.InsertProcedure(
				text,
				new ProcedureInsertRequest("Last") { Placement = InsertPlacement.Caret, CaretLine = 3 });

			Assert.Equal("Last", ProcedureOrder(result!.NewText).Last());
		}

		[Fact]
		public void InsertProcedure_WithScriptOverrides_AddsTheStatement()
		{
			ProcedureEditResult? result = this._service.InsertProcedure(
				TestScripts.DialogueScript(),
				new ProcedureInsertRequest("look_at_p_proc") { UseScriptOverrides = true });

			Assert.EndsWith("procedure look_at_p_proc begin\r\n    script_overrides;\r\n\r\nend", result!.NewText);
			Assert.Equal(new TextLineMap(result.NewText).LineCount - 1, result.FocusLine);
		}

		[Fact]
		public void InsertProcedure_CopyingBody_CopiesTheAnchorBody()
		{
			ProcedureEditResult? result = this._service.InsertProcedure(
				TestScripts.DialogueScript(),
				new ProcedureInsertRequest("Node001_copy") { Placement = InsertPlacement.After, AnchorProcedure = "Node001", CopyAnchorBody = true });

			Assert.Contains("procedure Node001_copy begin\r\n   Reply(103);\r\n   NOption(104, Node999, 4);\r\nend", result!.NewText);
		}

		[Fact]
		public void InsertProcedure_WithBody_UsesTheGivenCode()
		{
			ProcedureEditResult? result = this._service.InsertProcedure(
				TestScripts.DialogueScript(),
				new ProcedureInsertRequest("Node002") { Body = "\r\n   Reply(100);\r\n" });

			Assert.EndsWith("procedure Node002 begin\r\n\r\n   Reply(100);\r\n\r\nend", result!.NewText);
		}

		[Fact]
		public void InsertProcedure_ExistingProcedure_ReturnsNull()
		{
			Assert.Null(this._service.InsertProcedure(TestScripts.DialogueScript(), new ProcedureInsertRequest("node001")));
		}

		[Fact]
		public void InsertProcedure_InvalidName_Throws()
		{
			Assert.Throws<ArgumentException>(() => this._service.InsertProcedure(TestScripts.DialogueScript(), new ProcedureInsertRequest("bad name!")));
		}

		[Fact]
		public void InsertProcedure_UsesTheNewLineOfTheScript()
		{
			String text = "#include \"a.h\"\nprocedure a begin\nend";

			ProcedureEditResult? result = this._service.InsertProcedure(text, new ProcedureInsertRequest("b"));

			Assert.DoesNotContain("\r", result!.NewText);
			Assert.Equal(new[] { "a", "b" }, ProcedureOrder(result.NewText));
		}

		[Fact]
		public void InsertProcedure_IntoScriptWithoutProcedures_AddsBlock()
		{
			ProcedureEditResult? result = this._service.InsertProcedure("#include \"a.h\"", new ProcedureInsertRequest("first"));

			Assert.Equal(new[] { "first" }, ProcedureOrder(result!.NewText));
			Assert.StartsWith("#include \"a.h\"", result.NewText);
		}

		[Fact]
		public void InsertProcedure_IntoEmptyText_AddsBlock()
		{
			ProcedureEditResult? result = this._service.InsertProcedure(String.Empty, new ProcedureInsertRequest("first") { AddDeclaration = false });

			Assert.Equal("procedure first begin\r\n\r\nend\r\n", result!.NewText);
		}

		[Fact]
		public void MoveProcedure_BeforeAnother_MovesBodyAndDeclaration()
		{
			String text = TestScripts.DialogueScript();

			ProcedureEditResult? result = this._service.MoveProcedure(text, "Node999", "Node001");

			Assert.Equal(new[] { "start", "talk_p_proc", "Node999", "Node001" }, ProcedureOrder(result!.NewText));
			String[] lines = result.NewText.Split("\r\n");
			Assert.Equal("procedure talk_p_proc;", lines[5]);
			Assert.Equal("procedure Node999;", lines[6]);
			Assert.Equal("procedure Node001;", lines[7]);
			Assert.Equal(lines.Length, TestScripts.DialogueScript().Split("\r\n").Length);
			Assert.DoesNotContain("\r\n\r\n\r\n", result.NewText);
			AssertEditsMatch(text, result);
		}

		[Fact]
		public void MoveProcedure_ToTheEnd_PutsItLast()
		{
			String text = TestScripts.DialogueScript();

			ProcedureEditResult? result = this._service.MoveProcedure(text, "start", null);

			Assert.Equal(new[] { "talk_p_proc", "Node001", "Node999", "start" }, ProcedureOrder(result!.NewText));
			String[] lines = result.NewText.Split("\r\n");
			Assert.Equal("procedure start;", lines[7]);
			Assert.EndsWith("procedure start begin\r\nend", result.NewText);
			Assert.DoesNotContain("\r\n\r\n\r\n", result.NewText);
			AssertEditsMatch(text, result);
		}

		[Fact]
		public void MoveProcedure_WithoutDeclaration_Works()
		{
			String text = TestScripts.Crlf("procedure a begin", "   x;", "end", "", "procedure b begin", "end", "", "procedure c begin", "end");

			ProcedureEditResult? result = this._service.MoveProcedure(text, "c", "a");

			Assert.Equal(TestScripts.Crlf("procedure c begin", "end", "", "procedure a begin", "   x;", "end", "", "procedure b begin", "end"), result!.NewText);
			AssertEditsMatch(text, result);
		}

		[Fact]
		public void MoveProcedure_SameOrUnknown_ReturnsNull()
		{
			String text = TestScripts.DialogueScript();

			Assert.Null(this._service.MoveProcedure(text, "start", "START"));
			Assert.Null(this._service.MoveProcedure(text, "missing", "start"));
			Assert.Null(this._service.MoveProcedure(text, "start", "missing"));
		}

		[Fact]
		public void BuildProcedureBlock_BuildsTheLegacyShapes()
		{
			Assert.Equal("procedure a begin\r\n\r\nend", this._service.BuildProcedureBlock(new ProcedureInsertRequest("a"), "\r\n"));
			Assert.Equal(
				"procedure a begin\r\n  script_overrides;\r\n\r\nend",
				this._service.BuildProcedureBlock(new ProcedureInsertRequest("a") { UseScriptOverrides = true, Indentation = "  " }, "\r\n"));
			Assert.Equal(
				"procedure a begin\r\n   x;\r\nend",
				this._service.BuildProcedureBlock(new ProcedureInsertRequest("a") { CopyAnchorBody = true }, "\r\n", "   x;"));
			Assert.Equal(
				"procedure a begin\nend",
				this._service.BuildProcedureBlock(new ProcedureInsertRequest("a") { CopyAnchorBody = true }, "\n", String.Empty));
		}
	}
}
