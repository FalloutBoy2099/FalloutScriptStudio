namespace FalloutScriptStudio.Tests.Editor
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using FalloutScriptStudio.Core.TextTools;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Editor;
	using FalloutScriptStudio.Tests.Editor.Support;
	using ICSharpCode.AvalonEdit.Folding;
	using Xunit;
	using static FalloutScriptStudio.Tests.Editor.EditorSessionTestScripts;

	[Collection("Test collection for FalloutScriptStudio.Tests.Editor.EditorSessionTests")]
	public sealed class EditorSessionNavigationTests
	{
		[Fact]
		public void Outline_CollapsesTheOtherProceduresAndExpandsThemAgain()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(Script);
				using EditorSession session = env.StartSession(document);
				session.Context.TextArea.Caret.Offset = document.TextDocument.GetLineByNumber(11).Offset;

				session.Commands.Execute(EditorCommand.ToggleOutline);
				List<FoldingSection> sections = session.Folding.ProcedureSections.ToList();
				Assert.False(sections.Single(s => s.Title?.Trim() == "START").IsFolded);
				Assert.True(sections.Single(s => s.Title?.Trim() == "OTHER").IsFolded);

				session.Commands.Execute(EditorCommand.ToggleOutline);
				Assert.All(session.Folding.ProcedureSections, s => Assert.False(s.IsFolded));
			});
		}

		[Fact]
		public void GoTo_RevealsFoldedCodeAndSelectsText()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(Script);
				using EditorSession session = env.StartSession(document);
				FoldingSection other = session.Folding.ProcedureSections.Single(s => s.Title?.Trim() == "OTHER");
				other.IsFolded = true;

				session.Navigation.GoTo(16, 4, 5);

				Assert.False(other.IsFolded);
				Assert.Equal(16, session.Context.TextArea.Caret.Line);
				Assert.Equal("count", session.Context.Editor.SelectedText);
			});
		}

		[Fact]
		public void Navigation_BackAndForwardFollowTheJumps()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(Script);
				using EditorSession session = env.StartSession(document);
				IEditorSurface surface = new EditorSurface(session);

				surface.GoTo(5);
				surface.GoTo(15);
				Assert.True(surface.CanNavigateBack);

				surface.NavigateBack();
				Assert.Equal(5, surface.CaretLine);
				Assert.True(surface.CanNavigateForward);

				surface.NavigateForward();
				Assert.Equal(15, surface.CaretLine);
				Assert.False(surface.CanNavigateForward);
			});
		}

		[Fact]
		public void GoToDefinition_OfAProcedureCall_JumpsToItsBody()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel script = env.CreateDocument(Script);
				using EditorSession session = env.StartSession(script);
				IEditorSurface surface = new EditorSurface(session);
				surface.CaretOffset = script.TextDocument.GetLineByNumber(12).Offset + 5;

				surface.Execute(EditorCommand.GoToDefinition);

				Assert.Equal(15, surface.CaretLine);
				Assert.Equal("other", surface.SelectedText);
			});
		}

		[Fact]
		public void FindReferences_WithoutReferences_ReportsInTheStatusBar()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel script = CreateScriptWithVariable(env);
				using EditorSession session = env.StartSession(script);
				IEditorSurface surface = new EditorSurface(session);
				surface.CaretOffset = script.TextDocument.GetLineByNumber(11).Offset + 5;

				surface.Execute(EditorCommand.FindReferences);

				Assert.Contains(env.Recorder.Calls, call => call.StartsWith(nameof(IWorkspace.ReportStatus), StringComparison.Ordinal));
			});
		}

		[Fact]
		public void OpenInclude_CallsTheWorkspaceWithThePathAsWritten()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel script = env.CreateDocument(Script);
				using EditorSession session = env.StartSession(script);
				IEditorSurface surface = new EditorSurface(session);
				surface.CaretOffset = 3;

				surface.Execute(EditorCommand.OpenInclude);

				Assert.Contains(env.Recorder.Calls, call => call.Contains("..\\headers\\define.h", StringComparison.Ordinal));
			});
		}

		[Fact]
		public void HighlightWord_MarksAllOccurrencesAndTogglesOff()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel script = env.CreateDocument(Script);
				using EditorSession session = env.StartSession(script);
				IEditorSurface surface = new EditorSurface(session);
				surface.CaretOffset = script.TextDocument.GetLineByNumber(11).Offset + 5;

				surface.Execute(EditorCommand.HighlightWord);
				Assert.Equal("count", session.Occurrences.HighlightedText);
				Assert.Equal(3, session.Occurrences.OccurrenceCount);

				surface.CaretOffset = script.TextDocument.GetLineByNumber(11).Offset + 5;
				surface.Execute(EditorCommand.HighlightWord);
				Assert.Null(session.Occurrences.HighlightedText);
			});
		}

		[Fact]
		public void SearchMatches_AreMarkedAndCleared()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel script = env.CreateDocument(Script);
				using EditorSession session = env.StartSession(script);
				IEditorSurface surface = new EditorSurface(session);

				surface.SetSearchMatches(new[] { new TextSpan(0, 4), new TextSpan(10, 3) });
				Assert.Equal(2, session.Occurrences.MatchCount);

				surface.ClearSearchMatches();
				Assert.Equal(0, session.Occurrences.MatchCount);
			});
		}
	}
}
