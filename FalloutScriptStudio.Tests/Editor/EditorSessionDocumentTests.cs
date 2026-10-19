namespace FalloutScriptStudio.Tests.Editor
{
	using System;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Editor;
	using FalloutScriptStudio.Editor.Rendering;
	using FalloutScriptStudio.Tests.Editor.Support;
	using Xunit;
	using static FalloutScriptStudio.Tests.Editor.EditorSessionTestScripts;

	[Collection("Test collection for FalloutScriptStudio.Tests.Editor.EditorSessionTests")]
	public sealed class EditorSessionDocumentTests
	{
		[Fact]
		public void TextChange_MarksDocumentModifiedAndRequestsAParse()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument("procedure start begin\r\nend\r\n");
				using EditorSession session = env.StartSession(document);

				document.TextDocument.Insert(0, "// hi\r\n");

				Assert.True(document.IsModified);
				Assert.True(document.NeedsParse);
				Assert.Contains(env.Recorder.Calls, call => call.StartsWith(nameof(IWorkspace.RequestParseAsync), StringComparison.Ordinal));
			});
		}

		[Fact]
		public void Undo_BackToTheOriginalText_ClearsTheModifiedFlag()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument("abc", "C:\\scripts\\a.ssl", false);
				document.TextDocument.UndoStack.MarkAsOriginalFile();
				using EditorSession session = env.StartSession(document);

				document.TextDocument.Insert(0, "x");
				Assert.True(document.IsModified);
				document.TextDocument.UndoStack.Undo();

				Assert.False(document.IsModified);
			});
		}

		[Fact]
		public void ParserErrors_AreUnderlinedOnTheirLine()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(Script);
				using EditorSession session = env.StartSession(document);

				document.ParserErrors = new[]
				{
					new BuildError(BuildErrorType.Error, "Unknown symbol", "test.ssl", 11),
					new BuildError(BuildErrorType.Warning, "Unused", "TEST.SSL", 12, 4, 5),
					new BuildError(BuildErrorType.Error, "Other file", "other.h", 3),
					new BuildError(BuildErrorType.Message, "Note", "test.ssl", 2),
				};

				Assert.Equal(2, session.Diagnostics.Renderer.Count);
				Int32 offset = document.TextDocument.GetLineByNumber(11).Offset + 5;
				DiagnosticMarker marker = Assert.Single(session.Diagnostics.GetMarkersAt(offset));
				Assert.Equal("Unknown symbol", marker.Message);
				Assert.Equal(MarkerSeverity.Error, marker.Severity);
			});
		}

		[Fact]
		public void ParserErrors_HiddenWhenTheViewOptionIsOff()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(Script);
				using EditorSession session = env.StartSession(document);
				document.ParserErrors = new[] { new BuildError(BuildErrorType.Error, "x", "test.ssl", 11) };

				env.Services.ViewOptions.HighlightParseErrors = false;
				UiThread.Flush();

				Assert.Equal(0, session.Diagnostics.Renderer.Count);
			});
		}

		[Fact]
		public void NewParseResult_UpdatesCurrentProcedureAndProcedureHighlighting()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument("procedure start begin\r\nend\r\n", "C:\\scripts\\test.ssl", false);
				using EditorSession session = env.StartSession(document);
				session.Context.TextArea.Caret.Line = 1;
				Assert.Null(session.Navigation.CurrentProcedure);

				document.ProgramInfo = new InternalParser().Parse(document.TextDocument.Text, document.FilePath);
				session.Context.TextArea.Caret.Offset = document.TextDocument.GetLineByNumber(2).Offset + 1;
				session.Navigation.UpdateCurrentProcedure();

				Assert.Equal("start", session.Navigation.CurrentProcedure?.NormalizedName);
			});
		}
	}
}