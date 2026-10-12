namespace FalloutScriptStudio.Tests.Editor
{
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Editor;
	using FalloutScriptStudio.Tests.Editor.Support;
	using Xunit;
	using static FalloutScriptStudio.Tests.Editor.EditorSessionTestScripts;

	[Collection("Test collection for FalloutScriptStudio.Tests.Editor.EditorSessionTests")]
	public sealed class EditorSessionCommandTests
	{
		[Fact]
		public void Commands_CommentAndUncommentTheSelectedLines()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument("a;\r\nb;\r\nc;");
				using EditorSession session = env.StartSession(document);
				IEditorSurface surface = new EditorSurface(session);
				surface.Select(0, 6);

				surface.Execute(EditorCommand.CommentSelection);
				Assert.Equal("//a;\r\n//b;\r\nc;", document.TextDocument.Text);

				surface.Execute(EditorCommand.UncommentSelection);
				Assert.Equal("a;\r\nb;\r\nc;", document.TextDocument.Text);
			});
		}

		[Fact]
		public void Commands_CaseAndHexConversionWorkOnTheWordAtTheCaret()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument("x := value + 255;");
				using EditorSession session = env.StartSession(document);
				IEditorSurface surface = new EditorSurface(session);

				surface.CaretOffset = 7;
				surface.Execute(EditorCommand.UpperCase);
				Assert.Equal("x := VALUE + 255;", document.TextDocument.Text);

				surface.CaretOffset = 15;
				Assert.True(surface.CanExecute(EditorCommand.ConvertHexDec));
				surface.Execute(EditorCommand.ConvertHexDec);
				Assert.Equal("x := VALUE + 0xFF;", document.TextDocument.Text);
				Assert.Equal("0xFF", surface.SelectedText);
			});
		}

		[Fact]
		public void Commands_FormatSelectionAndDocument()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument("a:=1;\r\nb:=2;");
				using EditorSession session = env.StartSession(document);
				IEditorSurface surface = new EditorSurface(session);

				surface.CaretOffset = 2;
				surface.Execute(EditorCommand.FormatSelection);
				Assert.Equal("a := 1;\r\nb:=2;", document.TextDocument.Text);

				surface.Execute(EditorCommand.FormatDocument);
				Assert.Equal("a := 1;\r\nb := 2;", document.TextDocument.Text);
			});
		}

		[Fact]
		public void CanExecute_DependsOnDocumentKindAndSymbols()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel script = CreateScriptWithVariable(env);
				using EditorSession session = env.StartSession(script);
				IEditorSurface surface = new EditorSurface(session);

				surface.CaretOffset = script.TextDocument.GetLineByNumber(12).Offset + 5;
				Assert.True(surface.CanExecute(EditorCommand.GoToDeclaration));
				Assert.True(surface.CanExecute(EditorCommand.GoToDefinition));
				Assert.False(surface.CanExecute(EditorCommand.FindReferences));

				surface.CaretOffset = script.TextDocument.GetLineByNumber(11).Offset + 5;
				Assert.True(surface.CanExecute(EditorCommand.FindReferences));
				Assert.False(surface.CanExecute(EditorCommand.GoToDefinition));
				Assert.True(surface.CanExecute(EditorCommand.RenameSymbol));

				surface.CaretOffset = script.TextDocument.GetLineByNumber(2).Offset;
				Assert.False(surface.CanExecute(EditorCommand.GoToDefinition));
				surface.CaretOffset = 3;
				Assert.True(surface.CanExecute(EditorCommand.OpenInclude));

				DocumentViewModel message = env.CreateDocument("{100}{}{Hi}", "C:\\text\\a.msg");
				using EditorSession messageSession = env.StartSession(message);
				IEditorSurface messageSurface = new EditorSurface(messageSession);
				Assert.False(messageSurface.CanExecute(EditorCommand.FormatDocument));
				Assert.False(messageSurface.CanExecute(EditorCommand.CommentSelection));
			});
		}

		[Fact]
		public void Surface_InsertTextAndReplaceSelection_AreSingleUndoableOperations()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument("abcd");
				using EditorSession session = env.StartSession(document);
				IEditorSurface surface = new EditorSurface(session);

				surface.CaretOffset = 2;
				surface.InsertText("XY");
				Assert.Equal("abXYcd", document.TextDocument.Text);
				Assert.Equal(4, surface.CaretOffset);

				surface.Select(0, 2);
				surface.ReplaceSelection("Q");
				Assert.Equal("QXYcd", document.TextDocument.Text);

				surface.Undo();
				Assert.Equal("abXYcd", document.TextDocument.Text);
				surface.Undo();
				Assert.Equal("abcd", document.TextDocument.Text);
				Assert.True(surface.CanRedo);
			});
		}
	}
}
