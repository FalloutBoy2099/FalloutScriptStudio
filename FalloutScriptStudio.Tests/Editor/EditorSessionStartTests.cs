namespace FalloutScriptStudio.Tests.Editor
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Editor;
	using FalloutScriptStudio.Tests.Editor.Support;
	using ICSharpCode.AvalonEdit.Folding;
	using Xunit;
	using static FalloutScriptStudio.Tests.Editor.EditorSessionTestScripts;

	[Collection("Test collection for FalloutScriptStudio.Tests.Editor.EditorSessionTests")]
	public sealed class EditorSessionStartTests
	{
		[Fact]
		public void Start_ScriptWithProcedures_CreatesFoldingsAndAttachesTheSurface()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(Script);
				using EditorSession session = env.StartSession(document);

				Assert.True(session.Folding.IsEnabled);
				Assert.True(session.Folding.Count >= 3);
				Assert.NotNull(session.Highlighting.Definition);
				Assert.True(document.ViewState.HasBeenShown);
			});
		}

		[Fact]
		public void Start_MessageDocument_HasNoFolding()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument("{100}{}{Hello}\r\n", "C:\\text\\test.msg");
				using EditorSession session = env.StartSession(document);

				Assert.False(session.Folding.IsEnabled);
				Assert.False(session.Context.Editor.ShowLineNumbers);
				Assert.False(session.Context.Editor.Options.ShowColumnRuler);
				Assert.Null(session.Context.TextArea.IndentationStrategy);
				Assert.Equal("Verdana", session.Context.Editor.FontFamily.Source);
			});
		}

		[Fact]
		public void ViewState_SurvivesRecreationOfTheSession()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(Script);
				Int32 caret = document.TextDocument.GetLineByNumber(11).Offset + 3;
				IReadOnlyList<String> folded;

				using (EditorSession first = env.StartSession(document))
				{
					first.Context.TextArea.Caret.Offset = caret;
					FoldingSection section = first.Folding.ProcedureSections.Last();
					section.IsFolded = true;
					folded = new[] { section.Title ?? String.Empty };
					first.Stop();
				}

				using EditorSession second = env.StartSession(document);

				Assert.Equal(caret, second.Context.TextArea.Caret.Offset);
				Assert.Equal(folded[0], second.Folding.ProcedureSections.Last().Title);
				Assert.True(second.Folding.ProcedureSections.Last().IsFolded);
				Assert.False(second.Folding.ProcedureSections.First().IsFolded);
			});
		}

		[Fact]
		public void CollapsedProcedures_AreRememberedInTheStoreAndRestored()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				env.CollapsedStore.SetCollapsed("test.ssl", "OTHER", true);
				DocumentViewModel document = env.CreateDocument(Script);
				using EditorSession session = env.StartSession(document);

				FoldingSection other = session.Folding.ProcedureSections.Single(s => s.Title?.Trim() == "OTHER");
				FoldingSection start = session.Folding.ProcedureSections.Single(s => s.Title?.Trim() == "START");
				Assert.True(other.IsFolded);
				Assert.False(start.IsFolded);

				start.IsFolded = true;
				other.IsFolded = false;
				session.CommitViewState();

				Assert.True(env.CollapsedStore.IsCollapsed("test.ssl", "START"));
				Assert.False(env.CollapsedStore.IsCollapsed("test.ssl", "OTHER"));
			});
		}

		[Fact]
		public void Surface_PendingNavigationFromTheViewStateIsPerformedOnStart()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel script = env.CreateDocument(Script);
				script.ViewState.RequestGoTo(15, 11, 5);

				using EditorSession session = env.StartSession(script);

				Assert.Equal(15, session.Context.TextArea.Caret.Line);
				Assert.Equal("other", session.Context.Editor.SelectedText);
				Assert.Equal(0, script.ViewState.PendingLine);
			});
		}

		[Fact]
		public void ThemeChange_RebuildsTheHighlightingWithTheNewColors()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment(false);
				DocumentViewModel script = env.CreateDocument(Script);
				using EditorSession session = env.StartSession(script);
				Object? before = session.Highlighting.Definition;
				System.Windows.Media.Brush background = session.Context.Editor.Background;

				env.Theme.Apply(FalloutScriptStudio.Services.AppTheme.Dark);
				UiThread.Flush();

				Assert.NotSame(before, session.Highlighting.Definition);
				Assert.NotSame(background, session.Context.Editor.Background);
			});
		}
	}
}