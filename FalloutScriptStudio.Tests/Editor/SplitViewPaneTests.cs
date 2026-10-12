namespace FalloutScriptStudio.Tests.Editor
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Editor;
	using FalloutScriptStudio.Editor.Rendering;
	using FalloutScriptStudio.Editor.Tips;
	using FalloutScriptStudio.Tests.Editor.Support;
	using Xunit;
	using static FalloutScriptStudio.Tests.Editor.SplitViewTestSupport;

	/// <summary>The second pane of the split view behaves like the first one.</summary>
	[Collection("Test collection for FalloutScriptStudio.Tests.Editor.SplitViewTests")]
	public sealed class SplitViewPaneTests
	{
		[Fact]
		public void ShowSecondary_ShowsTheSameDocumentWithTheSameHighlightingAndOptions()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(Script);
				using EditorSession session = env.StartSession(document);

				EditorPane secondary = Split(session);

				Assert.True(session.IsSplit);
				Assert.Same(document.TextDocument, secondary.Context.Editor.Document);
				Assert.Same(session.Highlighting.Definition, secondary.Context.Editor.SyntaxHighlighting);
				Assert.Equal(session.Context.Editor.FontSize, secondary.Context.Editor.FontSize);
				Assert.Equal(session.Context.Editor.Options.IndentationSize, secondary.Context.Editor.Options.IndentationSize);
				Assert.Same(session.Indentation, secondary.Context.TextArea.IndentationStrategy);
				Assert.True(document.ViewState.IsSplit);
			});
		}

		[Fact]
		public void ShowSecondary_RestoresTheStoredCaret_WithoutCreatingAHistoryEntry()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(Script);
				document.ViewState.SecondaryCaretOffset = document.TextDocument.GetLineByNumber(12).Offset + 3;
				document.ViewState.SecondaryVerticalOffset = 0;
				using EditorSession session = env.StartSession(document);

				EditorPane secondary = Split(session);

				Assert.Equal(document.ViewState.SecondaryCaretOffset, secondary.Context.TextArea.Caret.Offset);
				Assert.False(document.ViewState.History.CanGoBack);
			});
		}

		[Fact]
		public void ShowSecondary_WithoutAStoredPosition_StartsWhereThePrimaryPaneIs()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(Script);
				using EditorSession session = env.StartSession(document);
				session.Context.TextArea.Caret.Offset = 20;

				EditorPane secondary = Split(session);

				Assert.Equal(20, secondary.Context.TextArea.Caret.Offset);
			});
		}

		[Fact]
		public void Secondary_HasItsOwnFoldingThatStartsLikeThePrimary()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(Script);
				using EditorSession session = env.StartSession(document);
				session.Folding.ProcedureSections.Last().IsFolded = true;

				EditorPane secondary = Split(session);

				Assert.True(secondary.Folding.IsEnabled);
				Assert.Equal(session.Folding.Count, secondary.Folding.Count);
				Assert.True(secondary.Folding.ProcedureSections.Last().IsFolded);
				Assert.False(secondary.Folding.ProcedureSections.First().IsFolded);

				secondary.Folding.ProcedureSections.First().IsFolded = true;
				Assert.False(session.Folding.ProcedureSections.First().IsFolded);
			});
		}

		[Fact]
		public void Secondary_FoldingFollowsANewParseResult()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(Script);
				using EditorSession session = env.StartSession(document);
				EditorPane secondary = Split(session);
				Int32 before = secondary.Folding.Count;

				document.TextDocument.Insert(document.TextDocument.TextLength, "\r\nprocedure third begin\r\n   a;\r\nend\r\n");
				document.ProgramInfo = new FalloutScriptStudio.Core.Parsing.InternalParser().Parse(document.TextDocument.Text, document.FilePath);

				Assert.Equal(before + 1, secondary.Folding.Count);
				Assert.Equal(session.Folding.Count, secondary.Folding.Count);
			});
		}

		[Fact]
		public void Secondary_AutoPairsBracketsAndEditsTheSharedDocument()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument("x := foo");
				using EditorSession session = env.StartSession(document);
				EditorPane secondary = Split(session);
				secondary.Context.TextArea.Caret.Offset = 8;

				TypingSimulator.Type(secondary.Context.TextArea, "(");

				Assert.Equal("x := foo()", document.TextDocument.Text);
				Assert.Equal(9, secondary.Context.TextArea.Caret.Offset);
				Assert.True(document.IsModified);
			});
		}

		[Fact]
		public void Secondary_ShowsTheDiagnosticsOfTheDocument()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(Script);
				document.ParserErrors = new List<BuildError> { new BuildError(BuildErrorType.Error, "Boom", document.FilePath!, 7) };
				using EditorSession session = env.StartSession(document);
				EditorPane secondary = Split(session);

				Assert.Contains(session.Diagnostics.Renderer, secondary.Context.TextView.BackgroundRenderers);
				Int32 offset = document.TextDocument.GetLineByNumber(7).Offset + 4;
				TipInfo tip = Assert.Single(secondary.Tips.BuildTips(offset), t => t.Kind == TipKind.Error);
				Assert.Equal(TipKind.Error, tip.Kind);
				Assert.Equal("Boom", tip.Body);

				document.ParserErrors = new List<BuildError>();
				Assert.Equal(0, session.Diagnostics.Renderer.Count);
			});
		}

		[Fact]
		public void Secondary_ShowsTheOccurrenceMarksAndTheBracketPair()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(Script);
				using EditorSession session = env.StartSession(document);
				EditorPane secondary = Split(session);
				Int32 call = document.TextDocument.Text.IndexOf("foo(", StringComparison.Ordinal);

				secondary.Context.TextArea.Caret.Offset = call + 4;
				Assert.NotNull(secondary.Brackets.Pair);
				Assert.Null(session.Brackets.Pair);

				secondary.Context.TextArea.Caret.Offset = call + 1;
				secondary.Commands.Execute(EditorCommand.HighlightWord);
				Assert.Equal("foo", session.Occurrences.HighlightedText);
				Assert.Equal(1, session.Occurrences.OccurrenceCount);
				Assert.Contains(secondary.Context.TextView.LineTransformers, t => t is HighlightColorizer);
			});
		}

		[Fact]
		public void Secondary_SelectsPlaceholdersLikeThePrimary()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument("x := foo({cond});");
				using EditorSession session = env.StartSession(document);
				EditorPane secondary = Split(session);

				secondary.Context.TextArea.Caret.Offset = 11;

				Assert.Equal("{cond}", secondary.Context.Editor.SelectedText);
				Assert.Equal(String.Empty, session.Context.Editor.SelectedText);
			});
		}

		[Fact]
		public void Secondary_ExpandsKeywordBlocksWithTab()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument("procedure start begin\r\n   if\r\nend\r\n");
				using EditorSession session = env.StartSession(document);
				EditorPane secondary = Split(session);
				secondary.Context.TextArea.Caret.Offset = document.TextDocument.Text.IndexOf("if", StringComparison.Ordinal) + 2;

				Boolean expanded = secondary.Input.TryCompleteBlock();

				Assert.True(expanded);
				Assert.Equal("{condition}", secondary.Context.Editor.SelectedText);
			});
		}

		[Fact]
		public void Secondary_ClosesBlocksWithEndAfterEnter()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument("procedure start begin");
				using EditorSession session = env.StartSession(document);
				EditorPane secondary = Split(session);
				secondary.Context.TextArea.Caret.Offset = document.TextDocument.TextLength;

				TypingSimulator.Type(secondary.Context.TextArea, "\n");

				Assert.Equal("procedure start begin\r\n   \r\nend", document.TextDocument.Text);
				Assert.Equal(2, secondary.Context.TextArea.Caret.Line);
			});
		}

		[Fact]
		public void Secondary_OpensTheCompletionList()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument("procedure other;\r\n\r\nprocedure other begin\r\n   ot\r\nend\r\n");
				using EditorSession session = env.StartSession(document);
				EditorPane secondary = Split(session);
				secondary.Context.TextArea.Caret.Offset = document.TextDocument.Text.IndexOf("   ot", StringComparison.Ordinal) + 5;

				Boolean opened = secondary.Completion.Show(true);

				Assert.True(opened);
				Assert.True(secondary.Completion.IsOpen);
				Assert.False(session.Completion.IsOpen);
				secondary.Completion.Close();
			});
		}
	}
}
