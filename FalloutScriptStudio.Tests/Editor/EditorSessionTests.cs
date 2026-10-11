namespace FalloutScriptStudio.Tests.Editor
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Core.TextTools;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Editor;
	using FalloutScriptStudio.Editor.Rendering;
	using FalloutScriptStudio.Tests.Editor.Support;
	using ICSharpCode.AvalonEdit.Folding;
	using Xunit;

	public sealed class EditorSessionTests
	{
		private const String Script =
			"#include \"..\\headers\\define.h\"\r\n"
			+ "\r\n"
			+ "procedure start;\r\n"
			+ "procedure other;\r\n"
			+ "\r\n"
			+ "variable begin\r\n"
			+ "   count;\r\n"
			+ "end\r\n"
			+ "\r\n"
			+ "procedure start begin\r\n"
			+ "   count := 1;\r\n"
			+ "   other;\r\n"
			+ "end\r\n"
			+ "\r\n"
			+ "procedure other begin\r\n"
			+ "   count := 2;\r\n"
			+ "end\r\n";

		private static DocumentViewModel CreateScriptWithVariable(EditorTestEnvironment env)
		{
			DocumentViewModel document = env.CreateDocument(Script);
			ScriptVariable variable = new ScriptVariable("count", VariableScope.Global) { DeclaredFile = document.FilePath, DeclaredLine = 7 };
			document.ProgramInfo = new ProgramInfo(document.ProgramInfo!.Procedures, new[] { variable }, null, null, document.FilePath);
			return document;
		}

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
		public void TabKey_IfKeyword_ExpandsToBlockAndSelectsTheCondition()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument("procedure start begin\r\n   if\r\nend\r\n");
				using EditorSession session = env.StartSession(document);
				session.Context.TextArea.Caret.Offset = document.TextDocument.Text.IndexOf("if", StringComparison.Ordinal) + 2;

				Boolean expanded = session.Input.TryCompleteBlock();

				Assert.True(expanded);
				Assert.Equal(
					"procedure start begin\r\n   if ({condition}) then begin\r\n      \r\n   end\r\nend\r\n",
					document.TextDocument.Text);
				Assert.Equal("{condition}", session.Context.Editor.SelectedText);
			});
		}

		[Fact]
		public void TabKey_InsideComment_DoesNothing()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument("// if");
				using EditorSession session = env.StartSession(document);
				session.Context.TextArea.Caret.Offset = 5;

				Assert.False(session.Input.TryCompleteBlock());
				Assert.Equal("// if", document.TextDocument.Text);
			});
		}

		[Fact]
		public void Typing_OpeningParenthesis_InsertsTheClosingOne()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument("x := foo");
				using EditorSession session = env.StartSession(document);
				session.Context.TextArea.Caret.Offset = 8;

				TypingSimulator.Type(session, "(");

				Assert.Equal("x := foo()", document.TextDocument.Text);
				Assert.Equal(9, session.Context.TextArea.Caret.Offset);
			});
		}

		[Fact]
		public void Typing_ClosingParenthesis_OverwritesTheAutomaticOne()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument("x := foo");
				using EditorSession session = env.StartSession(document);
				session.Context.TextArea.Caret.Offset = 8;

				TypingSimulator.Type(session, "(1)");

				Assert.Equal("x := foo(1)", document.TextDocument.Text);
				Assert.Equal(11, session.Context.TextArea.Caret.Offset);
			});
		}

		[Fact]
		public void Typing_Quote_InsertsPairAndTypeOverWorks()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument("x := ");
				using EditorSession session = env.StartSession(document);
				session.Context.TextArea.Caret.Offset = 5;

				TypingSimulator.Type(session, "\"abc\"");

				Assert.Equal("x := \"abc\"", document.TextDocument.Text);
				Assert.Equal(10, session.Context.TextArea.Caret.Offset);
			});
		}

		[Fact]
		public void Typing_BracketsWhenSettingIsOff_InsertsOnlyTheTypedCharacter()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				env.SettingsService.Settings.Editor.AutoInputPairedCharacters = false;
				env.SettingsService.Apply(env.SettingsService.Settings);
				DocumentViewModel document = env.CreateDocument("x := foo");
				using EditorSession session = env.StartSession(document);
				session.Context.TextArea.Caret.Offset = 8;

				TypingSimulator.Type(session, "(");

				Assert.Equal("x := foo(", document.TextDocument.Text);
			});
		}

		[Fact]
		public void Typing_Semicolon_FormatsTheLineBeforeTheCaret()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument("   x:=1+2");
				using EditorSession session = env.StartSession(document);
				session.Context.TextArea.Caret.Offset = 9;

				TypingSimulator.Type(session, ";");

				Assert.Equal("   x := 1 + 2;", document.TextDocument.Text);
				Assert.Equal(document.TextDocument.TextLength, session.Context.TextArea.Caret.Offset);
			});
		}

		[Fact]
		public void Typing_EndOnItsOwnLine_AlignsWithTheBegin()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument("procedure start begin\r\n   foo;\r\n   en");
				using EditorSession session = env.StartSession(document);
				session.Context.TextArea.Caret.Offset = document.TextDocument.TextLength;

				TypingSimulator.Type(session, "d");

				Assert.Equal("procedure start begin\r\n   foo;\r\nend", document.TextDocument.Text);
			});
		}

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

		[Fact]
		public void EnterKey_AfterBegin_IndentsTheNewLine()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument("procedure start begin");
				using EditorSession session = env.StartSession(document);
				session.Context.TextArea.Caret.Offset = document.TextDocument.TextLength;

				session.Context.TextArea.PerformTextInput("\n");

				Assert.Equal("procedure start begin\r\n   \r\nend", document.TextDocument.Text);
				Assert.Equal(26, session.Context.TextArea.Caret.Offset);
			});
		}

		[Fact]
		public void EnterKey_BetweenBeginAndEnd_PutsTheEndBackAtTheBeginIndentation()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument("procedure start beginend");
				using EditorSession session = env.StartSession(document);
				session.Context.TextArea.Caret.Offset = "procedure start begin".Length;

				session.Context.TextArea.PerformTextInput("\n");

				Assert.Equal("procedure start begin\r\nend", document.TextDocument.Text);
			});
		}

		[Fact]
		public void TabKey_InsertsSpacesUpToTheNextTabStop_AndIndentsSelectedLines()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument("a;\r\nb;");
				using EditorSession session = env.StartSession(document);
				session.Context.TextArea.Caret.Offset = 1;

				System.Windows.Documents.EditingCommands.TabForward.Execute(null, session.Context.TextArea);
				Assert.Equal("a  ;\r\nb;", document.TextDocument.Text);

				new EditorSurface(session).Select(0, document.TextDocument.TextLength);
				System.Windows.Documents.EditingCommands.TabForward.Execute(null, session.Context.TextArea);
				Assert.Equal("   a  ;\r\n   b;", document.TextDocument.Text);

				System.Windows.Documents.EditingCommands.TabBackward.Execute(null, session.Context.TextArea);
				Assert.Equal("a  ;\r\nb;", document.TextDocument.Text);
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

		[Fact]
		public void TabKey_WithTabsOption_InsertsTabCharacters()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				env.SettingsService.Settings.Editor.TabsToSpaces = false;
				env.SettingsService.Apply(env.SettingsService.Settings);
				DocumentViewModel document = env.CreateDocument("a;");
				using EditorSession session = env.StartSession(document);
				session.Context.TextArea.Caret.Offset = 0;

				System.Windows.Documents.EditingCommands.TabForward.Execute(null, session.Context.TextArea);

				Assert.Equal("\ta;", document.TextDocument.Text);
			});
		}
	}
}
