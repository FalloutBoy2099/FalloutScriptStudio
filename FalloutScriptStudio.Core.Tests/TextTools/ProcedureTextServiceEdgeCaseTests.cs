namespace FalloutScriptStudio.Core.Tests.TextTools
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Core.TextTools;
	using Xunit;

	public class ProcedureTextServiceEdgeCaseTests
	{
		private readonly ProcedureTextService _service = new ProcedureTextService();

		[Fact]
		public void GetProcedureCodes_ReadsSeveralBodiesWithOneCall()
		{
			IReadOnlyDictionary<String, String> codes = this._service.GetProcedureCodes(TestScripts.DialogueScript(), new[] { "node001", "Node999", "start", "missing" });

			Assert.Equal(TestScripts.Crlf("   Reply(103);", "   NOption(104, Node999, 4);"), codes["NODE001"]);
			Assert.Equal(String.Empty, codes["Node999"]);
			Assert.Equal(String.Empty, codes["start"]);
			Assert.False(codes.ContainsKey("missing"));
			Assert.Empty(this._service.GetProcedureCodes(TestScripts.DialogueScript(), Array.Empty<String>()));
		}

		[Fact]
		public void GetProcedureCodes_AgreesWithGetProcedureCode()
		{
			String text = TestScripts.DialogueScript();

			foreach (String name in new[] { "start", "talk_p_proc", "Node001", "Node999" })
			{
				Assert.Equal(this._service.GetProcedureCode(text, name), this._service.GetProcedureCodes(text, new[] { name })[name]);
			}
		}

		[Fact]
		public void InsertProcedure_AtCaretOnACommentAboveAProcedure_GoesInFrontOfTheComment()
		{
			String text = TestScripts.Crlf("#include \"a.h\"", "", "procedure a;", "procedure b;", "", "procedure a begin", "end", "", "// the second one", "procedure b begin", "end");

			ProcedureEditResult? result = this._service.InsertProcedure(
				text,
				new ProcedureInsertRequest("mid") { Placement = InsertPlacement.Caret, CaretLine = 9 });

			String[] lines = result!.NewText.Split("\r\n");
			Assert.Equal("procedure a;", lines[2]);
			Assert.Equal("procedure mid;", lines[3]);
			Assert.Equal("procedure b;", lines[4]);
			Assert.Equal(
				TestScripts.Crlf("end", "", "procedure mid begin", "", "end", "", "// the second one", "procedure b begin"),
				String.Join("\r\n", lines.Skip(7).Take(8)));
		}

		[Fact]
		public void InsertProcedure_AfterAProcedureThatIsFollowedDirectlyByAnother_AddsTheBlankLines()
		{
			String text = TestScripts.Crlf("procedure a begin", "end", "procedure b begin", "end");

			ProcedureEditResult? result = this._service.InsertProcedure(
				text,
				new ProcedureInsertRequest("c") { Placement = InsertPlacement.After, AnchorProcedure = "a", AddDeclaration = false });

			Assert.Equal(TestScripts.Crlf("procedure a begin", "end", "", "procedure c begin", "", "end", "", "procedure b begin", "end"), result!.NewText);
		}

		[Fact]
		public void InsertProcedure_WithoutDeclaration_LeavesTheDeclarationsAlone()
		{
			ProcedureEditResult? result = this._service.InsertProcedure(
				TestScripts.DialogueScript(),
				new ProcedureInsertRequest("Quiet") { AddDeclaration = false });

			Assert.DoesNotContain("procedure Quiet;", result!.NewText);
			Assert.Single(result.Edits);
		}

		[Fact]
		public void DeleteProcedure_BetweenProceduresWithoutBlankLines_RemovesJustTheBlock()
		{
			String text = TestScripts.Crlf("procedure a begin", "end", "procedure b begin", "end", "procedure c begin", "end");

			ProcedureEditResult? result = this._service.DeleteProcedure(text, "b");

			Assert.Equal(TestScripts.Crlf("procedure a begin", "end", "procedure c begin", "end"), result!.NewText);
		}

		[Fact]
		public void DeleteProcedure_OnlyProcedure_LeavesTheHeader()
		{
			String text = TestScripts.Crlf("#include \"a.h\"", "", "procedure a begin", "end");

			ProcedureEditResult? result = this._service.DeleteProcedure(text, "a");

			Assert.Equal("#include \"a.h\"", result!.NewText);
		}

		[Fact]
		public void DeleteProcedure_UsesTheNewLineOfTheScript()
		{
			String text = "procedure a begin\nend\n\nprocedure b begin\nend\n";

			ProcedureEditResult? result = this._service.DeleteProcedure(text, "a");

			Assert.Equal("procedure b begin\nend\n", result!.NewText);
		}

		[Fact]
		public void MoveProcedure_UpAndDown_AreInverse()
		{
			String text = TestScripts.DialogueScript();

			ProcedureEditResult moved = this._service.MoveProcedure(text, "Node999", "Node001")!;
			ProcedureEditResult back = this._service.MoveProcedure(moved.NewText, "Node999", null)!;

			Assert.Equal(text, back.NewText);
		}

		[Fact]
		public void MoveProcedure_Result_IsAValidEditList()
		{
			String text = TestScripts.DialogueScript();

			ProcedureEditResult result = this._service.MoveProcedure(text, "talk_p_proc", "start")!;

			Assert.Equal(result.NewText, TextEditApplier.Apply(text, result.Edits));
			Assert.Equal(new[] { "talk_p_proc", "start", "Node001", "Node999" }, new InternalParser().ParseProcedures(result.NewText, null).Where(p => p.StartLine > 0).OrderBy(p => p.StartLine).Select(p => p.Name).ToArray());
			Assert.True(result.FocusLine > 0);
		}
	}
}
