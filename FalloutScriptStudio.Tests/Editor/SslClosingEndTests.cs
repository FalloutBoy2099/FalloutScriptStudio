namespace FalloutScriptStudio.Tests.Editor
{
	using System;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Editor;
	using FalloutScriptStudio.Editor.Text;
	using FalloutScriptStudio.Tests.Editor.Support;
	using ICSharpCode.AvalonEdit.Document;
	using Xunit;

	/// <summary>The "end" that Enter adds after a <c>begin</c> line (legacy <c>DefaultFormattingStrategy</c>).</summary>
	public sealed class SslClosingEndTests
	{
		private const String Unit = "   ";

		/// <summary>Simulates Enter: breaks the line at the end of the first line and lets the strategy indent the new line.</summary>
		private static TextDocument PressEnter(String text, Int32 lineNumber, Func<Int32, Boolean>? isFoldStart = null, String newLine = "\r\n")
		{
			TextDocument document = new TextDocument(text);
			DocumentLine begin = document.GetLineByNumber(lineNumber);
			document.Insert(begin.EndOffset, newLine);
			SslIndentationStrategy strategy = new SslIndentationStrategy(() => Unit) { IsFoldStart = isFoldStart };
			strategy.IndentLine(document, document.GetLineByNumber(lineNumber + 1));
			return document;
		}

		[Fact]
		public void BeginAtTheEndOfTheDocument_AddsEndBelow()
		{
			TextDocument document = PressEnter("procedure start begin", 1);

			Assert.Equal("procedure start begin\r\n   \r\nend", document.Text);
		}

		[Fact]
		public void BeginLine_EndKeepsTheIndentationOfTheBeginLine()
		{
			TextDocument document = PressEnter("   while (a) do begin", 1);

			Assert.Equal("   while (a) do begin\r\n      \r\n   end", document.Text);
		}

		[Fact]
		public void BeginFollowedByAnEmptyLine_InsertsEndAboveIt()
		{
			TextDocument document = PressEnter("procedure x begin\r\n\r\nprocedure y begin", 1);

			Assert.Equal("procedure x begin\r\n   \r\nend\r\n\r\nprocedure y begin", document.Text);
		}

		[Fact]
		public void BeginFollowedByCode_DoesNotAddEnd()
		{
			TextDocument document = PressEnter("procedure x begin\r\n   foo;\r\nend", 1);

			Assert.Equal("procedure x begin\r\n   \r\n   foo;\r\nend", document.Text);
		}

		[Fact]
		public void BeginFollowedByOnlyWhitespace_CountsAsEmpty()
		{
			TextDocument document = PressEnter("x begin\r\n  \r\nlast", 1);

			Assert.Equal("x begin\r\n   \r\nend\r\n  \r\nlast", document.Text);
		}

		[Fact]
		public void ProcedureThatStartsAFold_DoesNotGetASecondEnd()
		{
			TextDocument document = PressEnter("procedure start begin", 1, line => line == 1);

			Assert.Equal("procedure start begin\r\n   ", document.Text);
		}

		[Fact]
		public void ProcedureWithoutFold_GetsAnEnd()
		{
			TextDocument document = PressEnter("procedure start begin", 1, _ => false);

			Assert.Equal("procedure start begin\r\n   \r\nend", document.Text);
		}

		[Fact]
		public void LineThatDoesNotEndWithBegin_DoesNotGetAnEnd()
		{
			Assert.Equal("if (a) then\r\n   ", PressEnter("if (a) then", 1).Text);
			Assert.Equal("begin_of_day;\r\n", PressEnter("begin_of_day;", 1).Text);
			Assert.Equal("x := 1; // then begin\r\n", PressEnter("x := 1; // then begin", 1).Text);
		}

		[Fact]
		public void BareBeginLine_GetsAnEnd()
		{
			TextDocument document = PressEnter("procedure start\r\nbegin", 2);

			Assert.Equal("procedure start\r\nbegin\r\n   \r\nend", document.Text);
		}

		[Fact]
		public void IfBeginAtTheEndOfTheDocument_LeavesAnExtraEmptyLine()
		{
			TextDocument document = PressEnter("   if (a) then begin", 1);

			Assert.Equal("   if (a) then begin\r\n      \r\n   end\r\n", document.Text);
		}

		[Fact]
		public void IfBeginFollowedByEmptyLineAndCode_InsertsOnlyEnd()
		{
			TextDocument document = PressEnter("if (a) then begin\r\n\r\nfoo;", 1);

			Assert.Equal("if (a) then begin\r\n   \r\nend\r\n\r\nfoo;", document.Text);
		}

		[Fact]
		public void IfBeginWhoseSuccessorEndsWithEnd_AddsElseBegin()
		{
			TextDocument document = PressEnter("procedure p begin\r\nif (a) then begin\r\n\r\nx := 1 end", 2);

			Assert.Equal("procedure p begin\r\nif (a) then begin\r\n   \r\nend\r\nelse begin\r\n\r\nx := 1 end", document.Text);
		}

		[Fact]
		public void IfBeginWhoseBlockAlreadyHasItsEnd_DoesNotAddASecondEnd()
		{
			TextDocument document = PressEnter("if (a) then begin\r\n\r\n   foo;\r\nend", 1);

			Assert.Equal("if (a) then begin\r\n   \r\n\r\n   foo;\r\nend", document.Text);
		}

		[Fact]
		public void WhileBeginInsideAClosedProcedure_WhoseBlockHasItsEnd_DoesNotAddAnEnd()
		{
			TextDocument document = PressEnter("procedure p begin\r\n   while (a) do begin\r\n\r\n      foo;\r\n   end\r\nend", 2);

			Assert.Equal("procedure p begin\r\n   while (a) do begin\r\n      \r\n\r\n      foo;\r\n   end\r\nend", document.Text);
		}

		[Fact]
		public void NestedBeginsWithAllTheirEnds_DoNotAddAnEnd()
		{
			TextDocument document = PressEnter(
				"procedure p begin\r\n   if (a) then begin\r\n      if (b) then begin\r\n\r\n      end\r\n   end\r\nend",
				2);

			Assert.Equal(
				"procedure p begin\r\n   if (a) then begin\r\n      \r\n      if (b) then begin\r\n\r\n      end\r\n   end\r\nend",
				document.Text);
		}

		[Fact]
		public void NewBeginInsideAProcedureWhoseEndFollows_StillGetsItsOwnEnd()
		{
			TextDocument document = PressEnter("procedure p begin\r\n   if (a) then begin\r\n\r\n   foo;\r\nend", 2);

			Assert.Equal("procedure p begin\r\n   if (a) then begin\r\n      \r\n   end\r\n\r\n   foo;\r\nend", document.Text);
		}

		[Fact]
		public void LoneBeginWhoseBlockAlreadyHasItsEnd_DoesNotAddASecondEnd()
		{
			TextDocument document = PressEnter("procedure start\r\nbegin\r\n\r\n   foo;\r\nend", 2);

			Assert.Equal("procedure start\r\nbegin\r\n   \r\n\r\n   foo;\r\nend", document.Text);
		}

		[Fact]
		public void ProcedureHeaderOnTwoLinesWithItsEnd_DoesNotAddASecondEnd()
		{
			TextDocument document = PressEnter("procedure foo(variable a,\r\n   variable b) begin\r\n\r\n   foo;\r\nend", 2);

			Assert.Equal("procedure foo(variable a,\r\n   variable b) begin\r\n      \r\n\r\n   foo;\r\nend", document.Text);
		}

		[Fact]
		public void ProcedureHeaderOnTwoLinesWithoutEnd_GetsAnEnd()
		{
			TextDocument document = PressEnter("procedure foo(variable a,\r\n   variable b) begin", 2);

			Assert.Equal("procedure foo(variable a,\r\n   variable b) begin\r\n      \r\n   end", document.Text);
		}

		[Fact]
		public void ExistingEndOnTheLastLineWithoutNewline_DoesNotAddASecondEnd()
		{
			TextDocument document = PressEnter("x begin\r\n\r\nend", 1);

			Assert.Equal("x begin\r\n   \r\n\r\nend", document.Text);
		}

		[Fact]
		public void LfDocument_WithoutEnd_AddsAnEndWithLfBreaks()
		{
			TextDocument document = PressEnter("procedure x begin\n\nlast", 1, newLine: "\n");

			Assert.Equal("procedure x begin\n   \nend\n\nlast", document.Text);
		}

		[Fact]
		public void LfDocument_WithItsEnd_DoesNotAddASecondEnd()
		{
			TextDocument document = PressEnter("procedure x begin\n\n   foo;\nend", 1, newLine: "\n");

			Assert.Equal("procedure x begin\n   \n\n   foo;\nend", document.Text);
		}

		[Fact]
		public void EndInCommentsAndStrings_DoesNotCountAsTheClosingEnd()
		{
			TextDocument document = PressEnter("procedure x begin\r\n\r\n// end\r\n/* end */\r\nfoo(\"end\");", 1);

			Assert.Equal("procedure x begin\r\n   \r\nend\r\n\r\n// end\r\n/* end */\r\nfoo(\"end\");", document.Text);
		}

		[Fact]
		public void IsBlockClosed_RequiresABalancedText()
		{
			Assert.True(SslIndentationRules.IsBlockClosed("x begin\r\nend", 7));
			Assert.False(SslIndentationRules.IsBlockClosed("x begin\r\nfoo;", 7));
			Assert.False(SslIndentationRules.IsBlockClosed("procedure p begin\r\n if a then begin\r\nend", 35));
		}

		[Fact]
		public void EnterKeyInTheEditor_AfterBeginWithAnExistingEnd_AddsNothingAndUndoesInOneStep()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				const String Source = "procedure start begin\r\n   if (a) then begin\r\n\r\n      foo;\r\n   end\r\nend";
				DocumentViewModel document = env.CreateDocument(Source);
				using EditorSession session = env.StartSession(document);
				session.Context.TextArea.Caret.Offset = document.TextDocument.GetLineByNumber(2).EndOffset;

				TypingSimulator.Type(session, "\n");

				Assert.Equal("procedure start begin\r\n   if (a) then begin\r\n      \r\n\r\n      foo;\r\n   end\r\nend", document.TextDocument.Text);
				document.TextDocument.UndoStack.Undo();
				Assert.Equal(Source, document.TextDocument.Text);
			});
		}

		[Fact]
		public void EnterKeyInTheEditor_AddedEndIsUndoneInOneStep()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument("procedure start begin");
				using EditorSession session = env.StartSession(document);
				session.Context.TextArea.Caret.Offset = document.TextDocument.TextLength;

				TypingSimulator.Type(session, "\n");
				Assert.Equal("procedure start begin\r\n   \r\nend", document.TextDocument.Text);
				document.TextDocument.UndoStack.Undo();

				Assert.Equal("procedure start begin", document.TextDocument.Text);
			});
		}

		[Fact]
		public void IndentLines_NeverAddsEnd()
		{
			TextDocument document = new TextDocument("procedure x begin\r\n");
			SslIndentationStrategy strategy = new SslIndentationStrategy(() => Unit);

			strategy.IndentLines(document, 2, 2);

			Assert.Equal("procedure x begin\r\n   ", document.Text);
		}

		[Fact]
		public void EnterKeyInTheEditor_AddsEndAndKeepsTheCaretOnTheNewLine()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument("procedure start begin");
				using EditorSession session = env.StartSession(document);
				session.Context.TextArea.Caret.Offset = document.TextDocument.TextLength;

				TypingSimulator.Type(session, "\n");

				Assert.Equal("procedure start begin\r\n   \r\nend", document.TextDocument.Text);
				Assert.Equal(2, session.Context.TextArea.Caret.Line);
				Assert.Equal(4, session.Context.TextArea.Caret.Column);
			});
		}

		[Fact]
		public void EnterKeyInTheEditor_ProcedureWithAFold_DoesNotAddEnd()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument("procedure start;\r\n\r\nprocedure start begin\r\n\r\n   foo;\r\nend\r\n");
				using EditorSession session = env.StartSession(document);
				Int32 headerEnd = document.TextDocument.GetLineByNumber(3).EndOffset;
				Assert.True(session.Folding.IsFoldStart(3));
				session.Context.TextArea.Caret.Offset = headerEnd;

				TypingSimulator.Type(session, "\n");

				Assert.Equal("procedure start;\r\n\r\nprocedure start begin\r\n   \r\n\r\n   foo;\r\nend\r\n", document.TextDocument.Text);
			});
		}
	}
}