namespace FalloutScriptStudio.Tests.Editor
{
	using System;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Editor;
	using FalloutScriptStudio.Editor.Rendering;
	using FalloutScriptStudio.Tests.Editor.Support;
	using Xunit;
	using static FalloutScriptStudio.Tests.Editor.SplitViewTestSupport;

	/// <summary>The commands of the split view act on the pane that has the focus.</summary>
	[Collection("Test collection for FalloutScriptStudio.Tests.Editor.SplitViewTests")]
	public sealed class SplitViewFocusTests
	{
		[Fact]
		public void ActivePane_FollowsTheKeyboardFocus_AndTheSurfaceActsOnIt()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(Script);
				using EditorSession session = env.StartSession(document);
				EditorSurface surface = new EditorSurface(session);
				EditorPane secondary = Split(session);
				session.Context.TextArea.Caret.Offset = 0;
				secondary.Context.TextArea.Caret.Offset = document.TextDocument.GetLineByNumber(8).Offset + 3;

				Assert.Same(session.Primary, session.ActivePane);
				Assert.Equal(1, surface.CaretLine);

				GiveFocus(secondary);

				Assert.Same(secondary, session.ActivePane);
				Assert.Equal(8, surface.CaretLine);
				Assert.Equal(4, surface.CaretColumn);

				GiveFocus(session.Primary);
				Assert.Same(session.Primary, session.ActivePane);
				Assert.Equal(1, surface.CaretLine);
			});
		}

		[Fact]
		public void SurfaceCommands_ActOnTheFocusedPane()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument("abc def\r\nxyz uvw\r\n");
				using EditorSession session = env.StartSession(document);
				EditorSurface surface = new EditorSurface(session);
				EditorPane secondary = Split(session);
				session.Context.TextArea.Caret.Offset = 3;
				secondary.Context.TextArea.Caret.Offset = document.TextDocument.GetLineByNumber(2).Offset + 3;
				session.Activate(secondary);

				surface.Execute(EditorCommand.DeleteToLineEnd);

				Assert.Equal("abc def\r\nxyz\r\n", document.TextDocument.Text);

				session.Activate(session.Primary);
				surface.Execute(EditorCommand.DeleteToLineEnd);

				Assert.Equal("abc\r\nxyz\r\n", document.TextDocument.Text);
			});
		}

		[Fact]
		public void SurfaceSelection_BelongsToTheFocusedPane()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument("abc def\r\nxyz uvw\r\n");
				using EditorSession session = env.StartSession(document);
				EditorSurface surface = new EditorSurface(session);
				EditorPane secondary = Split(session);
				session.Activate(secondary);

				surface.SelectAll();

				Assert.Equal(document.TextDocument.TextLength, secondary.Context.Editor.SelectionLength);
				Assert.Equal(0, session.Context.Editor.SelectionLength);
				Assert.Equal(document.TextDocument.Text, surface.SelectedText);

				surface.ReplaceSelection("new");
				Assert.Equal("new", document.TextDocument.Text);
			});
		}

		[Fact]
		public void SurfaceCaretMoved_IsRaisedForBothPanes()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(Script);
				using EditorSession session = env.StartSession(document);
				EditorSurface surface = new EditorSurface(session);
				EditorPane secondary = Split(session);
				Int32 raised = 0;
				surface.CaretMoved += (_, _) => raised++;

				session.Context.TextArea.Caret.Offset = 5;
				Int32 afterPrimary = raised;
				secondary.Context.TextArea.Caret.Offset = 9;

				Assert.True(afterPrimary > 0);
				Assert.True(raised > afterPrimary);
			});
		}

		[Fact]
		public void SurfaceGoTo_MovesTheCaretOfTheFocusedPane()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(Script);
				using EditorSession session = env.StartSession(document);
				EditorSurface surface = new EditorSurface(session);
				EditorPane secondary = Split(session);
				session.Context.TextArea.Caret.Offset = 0;
				session.Activate(secondary);

				surface.GoTo(7, 4);

				Assert.Equal(7, secondary.Context.TextArea.Caret.Line);
				Assert.Equal(1, session.Context.TextArea.Caret.Line);
			});
		}

		[Fact]
		public void HideSecondary_DetachesEverythingFromTheSecondEditor()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(Script);
				using EditorSession session = env.StartSession(document);
				EditorPane secondary = Split(session);
				session.Activate(secondary);

				session.HideSecondary();

				Assert.False(session.IsSplit);
				Assert.Null(session.Secondary);
				Assert.Same(session.Primary, session.ActivePane);
				Assert.Empty(session.Context.LinkedEditors);
				Assert.False(document.ViewState.IsSplit);
				Assert.DoesNotContain(session.Diagnostics.Renderer, secondary.Context.TextView.BackgroundRenderers);
				Assert.DoesNotContain(secondary.Context.TextView.LineTransformers, t => t is HighlightColorizer);
				Assert.NotSame(document.TextDocument, secondary.Context.Editor.Document);
				Assert.Equal(0, secondary.Folding.Count);
			});
		}

		[Fact]
		public void StopSession_KeepsTheSplitInTheViewStateAndStoresThePositionOfTheSecondPane()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(Script);
				EditorSession session = env.StartSession(document);
				EditorPane secondary = Split(session);
				secondary.Context.TextArea.Caret.Offset = 12;

				session.Dispose();

				Assert.True(document.ViewState.IsSplit);
				Assert.Equal(12, document.ViewState.SecondaryCaretOffset);
			});
		}
	}
}
