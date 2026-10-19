namespace FalloutScriptStudio.Tests.Editor
{
	using System;
	using System.Collections.Generic;
	using FalloutScriptStudio.Core.TextTools;
	using FalloutScriptStudio.Editor.Text;
	using ICSharpCode.AvalonEdit.Document;
	using Xunit;

	public sealed class DocumentEditsTests
	{
		[Fact]
		public void Apply_MultipleEdits_AreOneUndoStep()
		{
			TextDocument document = new TextDocument("a\r\nb\r\nc");
			document.UndoStack.ClearAll();

			Int32 count = DocumentEdits.Apply(document, new[] { TextEdit.Insert(0, "// "), TextEdit.Insert(3, "// ") });

			Assert.Equal(2, count);
			Assert.Equal("// a\r\n// b\r\nc", document.Text);
			document.UndoStack.Undo();
			Assert.Equal("a\r\nb\r\nc", document.Text);
		}

		[Fact]
		public void Apply_NoEdits_LeavesTheDocumentAlone()
		{
			TextDocument document = new TextDocument("abc");

			Assert.Equal(0, DocumentEdits.Apply(document, Array.Empty<TextEdit>()));
			Assert.False(document.UndoStack.CanUndo);
		}

		[Fact]
		public void ApplyLineWise_OnlyChangedLinesAreReplaced()
		{
			TextDocument document = new TextDocument("a:=1;\r\nkeep\r\nb:=2;");
			List<Int32> changedOffsets = [];
			document.Changing += (_, e) => changedOffsets.Add(e.Offset);

			DocumentEdits.ApplyLineWise(document, new TextEdit(0, document.TextLength, "a := 1;\r\nkeep\r\nb := 2;"));

			Assert.Equal("a := 1;\r\nkeep\r\nb := 2;", document.Text);
			Assert.Equal(2, changedOffsets.Count);
			Assert.DoesNotContain(changedOffsets, offset => offset > 5 && offset < 13);
		}

		[Fact]
		public void ApplyLineWise_DifferentLineCount_FallsBackToOneReplacement()
		{
			TextDocument document = new TextDocument("a\r\nb");

			DocumentEdits.ApplyLineWise(document, new TextEdit(0, 4, "a\r\nb\r\nc"));

			Assert.Equal("a\r\nb\r\nc", document.Text);
		}

		[Fact]
		public void IncreaseIndent_SkipsBlankLines()
		{
			String text = "a\r\n\r\nb";

			String result = TextEditApplier.Apply(text, DocumentEdits.IncreaseIndent(text, new TextSpan(0, text.Length), "   "));

			Assert.Equal("   a\r\n\r\n   b", result);
		}

		[Fact]
		public void ToggleLineComment_CommentsThenUncomments()
		{
			String text = "a\r\n   b";
			TextSpan all = new TextSpan(0, text.Length);

			String commented = TextEditApplier.Apply(text, DocumentEdits.ToggleLineComment(text, all));
			Assert.Equal("//a\r\n   //b", commented);

			String restored = TextEditApplier.Apply(commented, DocumentEdits.ToggleLineComment(commented, new TextSpan(0, commented.Length)));
			Assert.Equal(text, restored);
		}

		[Fact]
		public void ToggleLineComment_MixedLines_CommentsTheRest()
		{
			String text = "//a\r\nb";

			String result = TextEditApplier.Apply(text, DocumentEdits.ToggleLineComment(text, new TextSpan(0, text.Length)));

			Assert.Equal("//a\r\n//b", result);
		}
	}
}