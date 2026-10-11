namespace FalloutScriptStudio.Tests.Editor
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Input;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Editor;
	using FalloutScriptStudio.Editor.Rendering;
	using FalloutScriptStudio.Editor.Tips;
	using FalloutScriptStudio.Tests.Editor.Support;
	using FalloutScriptStudio.Views.Editor;
	using ICSharpCode.AvalonEdit;
	using Xunit;

	/// <summary>The split view: the second pane behaves like the first one, and the commands act on the pane that has the focus.</summary>
	public sealed class SplitViewTests
	{
		private const String Script =
			"#include \"..\\headers\\define.h\"\r\n"
			+ "\r\n"
			+ "procedure start;\r\n"
			+ "procedure other;\r\n"
			+ "\r\n"
			+ "procedure start begin\r\n"
			+ "   other;\r\n"
			+ "   x := foo(1 + 2);\r\n"
			+ "end\r\n"
			+ "\r\n"
			+ "procedure other begin\r\n"
			+ "   display_msg(\"x\");\r\n"
			+ "end\r\n";

		private static Window Show(FrameworkElement view)
		{
			Window window = new Window
			{
				Content = view,
				Width = 800,
				Height = 600,
				Left = -20000,
				Top = -20000,
				ShowInTaskbar = false,
				WindowStartupLocation = WindowStartupLocation.Manual,
			};
			window.Show();
			UiThread.Flush();
			return window;
		}

		private static EditorPane Split(EditorSession session)
		{
			TextEditor editor = new TextEditor();
			EditorPane pane = session.ShowSecondary(editor);
			editor.Measure(new Size(900, 300));
			editor.Arrange(new Rect(0, 0, 900, 300));
			editor.UpdateLayout();
			return pane;
		}

		private static void GiveFocus(EditorPane pane)
		{
			KeyboardFocusChangedEventArgs args = new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, 0, null, pane.Context.TextArea)
			{
				RoutedEvent = Keyboard.GotKeyboardFocusEvent,
			};
			pane.Context.TextArea.RaiseEvent(args);
		}

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

		[Fact]
		public void View_SplitCommand_TogglesTheSecondPane()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(Script);
				ScriptEditorView view = new ScriptEditorView(env.Services) { DataContext = document };
				Window window = Show(view);

				view.Surface!.Execute(EditorCommand.SplitDocument);
				UiThread.Flush();
				Assert.True(view.Session!.IsSplit);
				TextEditor second = view.Session.Secondary!.Context.Editor;
				Assert.Equal(Visibility.Visible, second.Visibility);
				Assert.NotNull(second.ContextMenu);
				Assert.NotNull(view.Editor.ContextMenu);

				view.Surface.Execute(EditorCommand.SplitDocument);
				UiThread.Flush();
				Assert.False(view.Session.IsSplit);
				Assert.Equal(Visibility.Collapsed, second.Visibility);
				Assert.False(document.ViewState.IsSplit);
				window.Close();
			});
		}

		[Fact]
		public void View_SplitStaysWhenAnotherTabIsSelectedAndTheViewIsCreatedAgain()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(Script);
				ScriptEditorView first = new ScriptEditorView(env.Services) { DataContext = document };
				Window window = Show(first);
				first.Surface!.Execute(EditorCommand.SplitDocument);
				UiThread.Flush();
				Int32 caret = document.TextDocument.GetLineByNumber(8).Offset + 2;
				first.Session!.Secondary!.Context.TextArea.Caret.Offset = caret;

				window.Content = null;
				UiThread.Flush();
				Assert.Null(document.Editor);

				ScriptEditorView second = new ScriptEditorView(env.Services) { DataContext = document };
				window.Content = second;
				UiThread.Flush();

				Assert.True(second.Session!.IsSplit);
				EditorPane secondary = second.Session.Secondary!;
				Assert.Equal(Visibility.Visible, secondary.Context.Editor.Visibility);
				Assert.Equal(caret, secondary.Context.TextArea.Caret.Offset);
				window.Close();
			});
		}

		[Fact]
		public void View_ClosedSplit_StaysClosedWhenTheViewIsCreatedAgain()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(Script);
				ScriptEditorView first = new ScriptEditorView(env.Services) { DataContext = document };
				Window window = Show(first);
				first.Surface!.Execute(EditorCommand.SplitDocument);
				first.Surface.Execute(EditorCommand.SplitDocument);
				window.Content = null;
				UiThread.Flush();

				ScriptEditorView second = new ScriptEditorView(env.Services) { DataContext = document };
				window.Content = second;
				UiThread.Flush();

				Assert.False(second.Session!.IsSplit);
				window.Close();
			});
		}

		[Fact]
		public void View_ContextMenuOfTheSecondPane_UsesItsOwnClipboardTarget()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(Script);
				ScriptEditorView view = new ScriptEditorView(env.Services) { DataContext = document };
				Window window = Show(view);
				view.Surface!.Execute(EditorCommand.SplitDocument);
				UiThread.Flush();

				EditorPane secondary = view.Session!.Secondary!;
				MenuItem copy = secondary.Context.Editor.ContextMenu!.Items.OfType<MenuItem>().Single(item => item.Command == ApplicationCommands.Copy);
				MenuItem primaryCopy = view.Editor.ContextMenu!.Items.OfType<MenuItem>().Single(item => item.Command == ApplicationCommands.Copy);

				Assert.Same(secondary.Context.TextArea, copy.CommandTarget);
				Assert.Same(view.Editor.TextArea, primaryCopy.CommandTarget);
				window.Close();
			});
		}

		[Fact]
		public void View_StoppingTheSessionWhileSplit_ReleasesTheSecondEditor()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(Script);
				ScriptEditorView view = new ScriptEditorView(env.Services) { DataContext = document };
				Window window = Show(view);
				view.Surface!.Execute(EditorCommand.SplitDocument);
				UiThread.Flush();
				TextEditor second = view.Session!.Secondary!.Context.Editor;

				window.Content = null;
				UiThread.Flush();

				Assert.NotSame(document.TextDocument, second.Document);
				Assert.Equal(Visibility.Collapsed, second.Visibility);
				window.Close();
			});
		}
	}
}
