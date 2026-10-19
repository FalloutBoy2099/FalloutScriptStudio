namespace FalloutScriptStudio.Tests.Editor
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.Parsing;
	using FalloutScriptStudio.Core.Snippets;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Editor;
	using FalloutScriptStudio.Editor.Completion;
	using FalloutScriptStudio.Editor.Controllers;
	using FalloutScriptStudio.Tests.Editor.Support;
	using FalloutScriptStudio.TestSupport;
	using ICSharpCode.AvalonEdit.Document;
	using Xunit;

	/// <summary>Snippets in the editor: expansion of a trigger with Tab, tab stops and mirrors, one undo step, the completion list, Insert Snippet and Surround With.</summary>
	[Collection("Test collection for FalloutScriptStudio.Tests.Editor.EditorSessionTests")]
	public sealed class EditorSnippetTests
	{
		private const String Pair = "variable ${1:name} := ${2:0}; // ${1}$0";

		private static void SaveUserSnippets(EditorTestEnvironment env, params SnippetDefinition[] snippets)
		{
			Assert.True(env.Services.Snippets.TrySave(SnippetSource.User, snippets, out String? error), error);
		}

		private static SnippetDefinition Define(String trigger, String body, Boolean enabled = true)
		{
			return new SnippetDefinition { Trigger = trigger, Description = trigger, Body = body, Enabled = enabled };
		}

		private static Int32 After(DocumentViewModel document, String text)
		{
			return document.TextDocument.Text.IndexOf(text, StringComparison.Ordinal) + text.Length;
		}

		[Fact]
		public void Tab_OnATrigger_InsertsTheSnippet_SelectsItsFirstStop_AndOneUndoRestoresTheTrigger()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument("procedure start begin\r\n   dispmsg\r\nend\r\n");
				using EditorSession session = env.StartSession(document);
				session.Context.TextArea.Caret.Offset = After(document, "dispmsg");

				Assert.True(session.Input.Snippets.TryExpandTrigger());

				Assert.Equal("procedure start begin\r\n   display_msg(\"message\");\r\nend\r\n", document.TextDocument.Text);
				Assert.Equal("\"message\"", session.Context.Editor.SelectedText);
				Assert.True(session.Input.Snippets.IsActive);
				Assert.Equal(1, session.Input.Snippets.CurrentStopIndex);

				document.TextDocument.UndoStack.Undo();
				Assert.Equal("procedure start begin\r\n   dispmsg\r\nend\r\n", document.TextDocument.Text);
			});
		}

		[Fact]
		public void TheLinesOfTheBody_AreIndentedLikeTheLineOfTheTrigger_WithTheIndentationOfTheSettings()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument("procedure start begin\r\n   ifb\r\nend\r\n");
				using EditorSession session = env.StartSession(document);
				session.Context.TextArea.Caret.Offset = After(document, "ifb");

				Assert.True(session.Input.Snippets.TryExpandTrigger());

				Assert.Equal("procedure start begin\r\n   if (condition) then begin\r\n      \r\n   end\r\nend\r\n", document.TextDocument.Text);
				Assert.Equal("condition", session.Context.Editor.SelectedText);

				Assert.True(session.Input.Snippets.MoveToNextStop());
				Assert.False(session.Input.Snippets.IsActive);
				Assert.Equal(After(document, "begin\r\n      "), session.Context.TextArea.Caret.Offset);
			});
		}

		[Fact]
		public void WithTabsInTheSettings_TheBodyIsIndentedWithTabs()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				env.SettingsService.Settings.Editor.TabsToSpaces = false;
				env.SettingsService.Apply(env.SettingsService.Settings);
				DocumentViewModel document = env.CreateDocument("procedure start begin\r\n\tifelse\r\nend\r\n");
				using EditorSession session = env.StartSession(document);
				session.Context.TextArea.Caret.Offset = After(document, "ifelse");

				Assert.True(session.Input.Snippets.TryExpandTrigger());

				Assert.Equal("procedure start begin\r\n\tif (condition) then begin\r\n\t\t\r\n\tend else begin\r\n\t\t\r\n\tend\r\nend\r\n", document.TextDocument.Text);
			});
		}

		[Fact]
		public void ADocumentWithLineFeeds_GetsLineFeeds()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument("procedure start begin\n   whileb\nend\n");
				using EditorSession session = env.StartSession(document);
				session.Context.TextArea.Caret.Offset = After(document, "whileb");

				Assert.True(session.Input.Snippets.TryExpandTrigger());

				Assert.Equal("procedure start begin\n   while (condition) do begin\n      \n   end\nend\n", document.TextDocument.Text);
				Assert.DoesNotContain('\r', document.TextDocument.Text);
			});
		}

		[Fact]
		public void TypingInAStop_UpdatesItsMirrors_InTheUndoStepOfTheTyping()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				SaveUserSnippets(env, Define("pair", Pair));
				DocumentViewModel document = env.CreateDocument("procedure start begin\r\n   pair\r\nend\r\n");
				using EditorSession session = env.StartSession(document);
				session.Context.TextArea.Caret.Offset = After(document, "pair");
				Assert.True(session.Input.Snippets.TryExpandTrigger());
				Assert.Equal("   variable name := 0; // name", document.TextDocument.GetText(document.TextDocument.GetLineByNumber(2).Offset, document.TextDocument.GetLineByNumber(2).Length));

				TypingSimulator.Type(session, "h");

				Assert.Equal("procedure start begin\r\n   variable h := 0; // h\r\nend\r\n", document.TextDocument.Text);
				Assert.True(session.Input.Snippets.IsActive);

				document.TextDocument.UndoStack.Undo();
				Assert.Equal("procedure start begin\r\n   variable name := 0; // name\r\nend\r\n", document.TextDocument.Text);
				document.TextDocument.UndoStack.Undo();
				Assert.Equal("procedure start begin\r\n   pair\r\nend\r\n", document.TextDocument.Text);
			});
		}

		[Fact]
		public void TheTabKey_ExpandsTheTrigger_ThenMovesToTheFinalCaret_AndEscapeEndsTheSession()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				SaveUserSnippets(env, Define("pair", Pair));
				DocumentViewModel document = env.CreateDocument("pair");
				using EditorSession session = env.StartSession(document);
				session.Context.TextArea.Caret.Offset = 4;
				using System.Windows.Interop.HwndSource source = new System.Windows.Interop.HwndSource(0, 0, 0, 0, 0, "snippet-key-test", IntPtr.Zero);

				System.Windows.Input.KeyEventArgs tab = new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, source, 0, System.Windows.Input.Key.Tab) { RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent };
				session.Context.TextArea.RaiseEvent(tab);
				Assert.True(tab.Handled);
				Assert.Equal("name", session.Context.Editor.SelectedText);

				System.Windows.Input.KeyEventArgs second = new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, source, 0, System.Windows.Input.Key.Tab) { RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent };
				session.Context.TextArea.RaiseEvent(second);
				Assert.True(second.Handled);
				Assert.Equal("0", session.Context.Editor.SelectedText);

				System.Windows.Input.KeyEventArgs escape = new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, source, 0, System.Windows.Input.Key.Escape) { RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent };
				session.Context.TextArea.RaiseEvent(escape);
				Assert.True(escape.Handled);
				Assert.False(session.Input.Snippets.IsActive);
			});
		}

		[Fact]
		public void TabAndShiftTab_MoveBetweenTheStops_AndTheLastTabGoesToTheFinalCaret()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				SaveUserSnippets(env, Define("pair", Pair));
				DocumentViewModel document = env.CreateDocument("pair");
				using EditorSession session = env.StartSession(document);
				session.Context.TextArea.Caret.Offset = 4;
				SnippetController snippets = session.Input.Snippets;
				Assert.True(snippets.TryExpandTrigger());
				Assert.Equal((2, "name"), (snippets.StopCount, session.Context.Editor.SelectedText));

				Assert.True(snippets.MoveToNextStop());
				Assert.Equal(("0", 2), (session.Context.Editor.SelectedText, snippets.CurrentStopIndex));

				Assert.True(snippets.MoveToPreviousStop());
				Assert.Equal(("name", 1), (session.Context.Editor.SelectedText, snippets.CurrentStopIndex));

				Assert.True(snippets.MoveToNextStop());
				Assert.True(snippets.MoveToNextStop());
				Assert.False(snippets.IsActive);
				Assert.Equal(document.TextDocument.TextLength, session.Context.TextArea.Caret.Offset);
				Assert.Equal(String.Empty, session.Context.Editor.SelectedText);
				Assert.False(snippets.MoveToNextStop());
			});
		}

		[Fact]
		public void Escape_LeavesSnippetMode_AndTheMirrorsStopFollowing()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				SaveUserSnippets(env, Define("pair", Pair));
				DocumentViewModel document = env.CreateDocument("pair");
				using EditorSession session = env.StartSession(document);
				session.Context.TextArea.Caret.Offset = 4;
				Assert.True(session.Input.Snippets.TryExpandTrigger());

				session.Input.Snippets.End();
				TypingSimulator.Type(session, "x");

				Assert.False(session.Input.Snippets.IsActive);
				Assert.Equal("variable x := 0; // name", document.TextDocument.Text);
			});
		}

		[Fact]
		public void MovingTheCaretOutOfTheSnippet_EndsTheSession()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument("procedure start begin\r\n   ifb\r\nend\r\n");
				using EditorSession session = env.StartSession(document);
				session.Context.TextArea.Caret.Offset = After(document, "ifb");
				Assert.True(session.Input.Snippets.TryExpandTrigger());
				Assert.True(session.Input.Snippets.IsActive);

				session.Context.TextArea.Caret.Offset = 0;

				Assert.False(session.Input.Snippets.IsActive);
			});
		}

		[Fact]
		public void TheBlockCompletionOfTheLegacyKeywords_KeepsItsBehaviour_EvenWhenASnippetUsesTheWord()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				SaveUserSnippets(env, Define("if", "SNIPPET$0"));
				DocumentViewModel document = env.CreateDocument("procedure start begin\r\n   if\r\nend\r\n");
				using EditorSession session = env.StartSession(document);
				session.Context.TextArea.Caret.Offset = After(document, "if");

				Assert.False(session.Input.Snippets.TryExpandTrigger());
				Assert.True(session.Input.TryCompleteBlock());

				Assert.Equal("procedure start begin\r\n   if ({condition}) then begin\r\n      \r\n   end\r\nend\r\n", document.TextDocument.Text);
				Assert.False(session.Input.Snippets.IsActive);
			});
		}

		[Fact]
		public void ATriggerInACommentOrInTheMiddleOfAWord_IsNotExpanded()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument("// ifb\r\nxifb\r\nifbx\r\n");
				using EditorSession session = env.StartSession(document);
				SnippetController snippets = session.Input.Snippets;

				session.Context.TextArea.Caret.Offset = After(document, "// ifb");
				Assert.False(snippets.TryExpandTrigger());
				session.Context.TextArea.Caret.Offset = After(document, "\r\nxifb");
				Assert.False(snippets.TryExpandTrigger());
				session.Context.TextArea.Caret.Offset = document.TextDocument.Text.IndexOf("ifbx", StringComparison.Ordinal) + 3;
				Assert.False(snippets.TryExpandTrigger());

				Assert.Equal("// ifb\r\nxifb\r\nifbx\r\n", document.TextDocument.Text);
			});
		}

		[Fact]
		public void ADisabledSnippet_HidesTheBuiltInOneOfTheSameTrigger()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				SaveUserSnippets(env, Define("dispmsg", "ignored", false));
				DocumentViewModel document = env.CreateDocument("dispmsg");
				using EditorSession session = env.StartSession(document);
				session.Context.TextArea.Caret.Offset = 7;

				Assert.False(session.Input.Snippets.TryExpandTrigger());
				Assert.Equal("dispmsg", document.TextDocument.Text);
			});
		}

		[Fact]
		public void TheSnippetOfAProject_WinsOverTheUserSnippet_AndTheUserSnippetOverTheBuiltInOne()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				String project = TestFiles.CreateWorkFolder("snippet-project");
				try
				{
					SaveUserSnippets(env, Define("dispmsg", "USER$0"), Define("zz", "USER-ZZ$0"));
					env.SettingsService.SetActiveProject(project, new ProjectSettings());
					Assert.True(env.Services.Snippets.TrySave(SnippetSource.Project, new[] { Define("zz", "PROJECT-ZZ$0") }, out String? error), error);
					DocumentViewModel document = env.CreateDocument("dispmsg\r\nzz\r\n");
					using EditorSession session = env.StartSession(document);

					session.Context.TextArea.Caret.Offset = 7;
					Assert.True(session.Input.Snippets.TryExpandTrigger());
					session.Context.TextArea.Caret.Offset = document.TextDocument.Text.IndexOf("zz", StringComparison.Ordinal) + 2;
					Assert.True(session.Input.Snippets.TryExpandTrigger());

					Assert.Equal("USER\r\nPROJECT-ZZ\r\n", document.TextDocument.Text);
					Assert.True(File.Exists(Path.Combine(project, SnippetFile.ProjectFileName)));
				}
				finally
				{
					TestFiles.DeleteWorkFolder(project);
				}
			});
		}

		[Fact]
		public void TheCompletionList_OffersTheSnippetsWhoseTriggerStartsWithThePrefix_AndChoosingOneExpandsIt()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument("procedure start begin\r\n   disp\r\nend\r\n");
				using EditorSession session = env.StartSession(document);
				session.Context.TextArea.Caret.Offset = After(document, "disp");

				Assert.True(session.Completion.Show(true));

				ScriptCompletionData snippet = Assert.Single(session.Completion.Window!.CompletionList.CompletionData.OfType<ScriptCompletionData>(), data => data.Text == "dispmsg");
				Assert.NotNull(snippet.Description);
				TextSegment segment = new TextSegment { StartOffset = After(document, "begin\r\n   "), Length = 4 };
				snippet.Complete(session.Context.TextArea, segment, EventArgs.Empty);

				Assert.Equal("procedure start begin\r\n   display_msg(\"message\");\r\nend\r\n", document.TextDocument.Text);
				Assert.True(session.Input.Snippets.IsActive);
			});
		}

		[Fact]
		public void TheCompletionList_ShowsASnippetEvenWhenTheOnlyOtherEntryIsTheTypedWord()
		{
			using EditorTestEnvironment env = new EditorTestEnvironment();
			IReadOnlyList<AutosuggestItem> items = CompletionSource.GetItems(
				"talkproc",
				null,
				env.Services.GetOpcodeCatalog(),
				1,
				new[] { new Snippet("talkproc", "A talk procedure", "x") },
				null);

			AutosuggestItem item = Assert.Single(items);
			Assert.Equal(AutosuggestKind.Snippet, item.Kind);
		}

		[Fact]
		public void InsertSnippet_AsksForASnippet_AndInsertsTheChosenOneAtTheCaret()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				env.Recorder.SnippetAnswer = BuiltInSnippets.All.First(snippet => snippet.Trigger == "callp");
				DocumentViewModel document = env.CreateDocument("procedure start begin\r\n   \r\nend\r\n");
				using EditorSession session = env.StartSession(document);
				session.Context.TextArea.Caret.Offset = After(document, "begin\r\n   ");
				IEditorSurface surface = new EditorSurface(session);

				Assert.True(surface.CanExecute(EditorCommand.InsertSnippet));
				surface.Execute(EditorCommand.InsertSnippet);

				Assert.Equal("procedure start begin\r\n   call procedure_name();\r\nend\r\n", document.TextDocument.Text);
				SnippetPickerRequestView request = new SnippetPickerRequestView(Assert.Single(env.Recorder.SnippetRequests));
				Assert.False(request.Surround);
				Assert.Contains("callp", request.Triggers);
			});
		}

		[Fact]
		public void InsertSnippet_WhenTheListIsCancelled_ChangesNothing()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument("procedure start begin\r\nend\r\n");
				using EditorSession session = env.StartSession(document);
				IEditorSurface surface = new EditorSurface(session);

				surface.Execute(EditorCommand.InsertSnippet);

				Assert.Equal("procedure start begin\r\nend\r\n", document.TextDocument.Text);
				Assert.Single(env.Recorder.SnippetRequests);
			});
		}

		[Fact]
		public void SurroundWith_NeedsASelection_OffersOnlyTheSnippetsThatUseIt_AndWrapsTheSelectedCode()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				env.Recorder.SnippetAnswer = BuiltInSnippets.All.First(snippet => snippet.Trigger == "ifb");
				DocumentViewModel document = env.CreateDocument("procedure start begin\r\n   x := 1;\r\n   y := 2;\r\nend\r\n");
				using EditorSession session = env.StartSession(document);
				IEditorSurface surface = new EditorSurface(session);
				Assert.False(surface.CanExecute(EditorCommand.SurroundWith));

				Int32 start = document.TextDocument.Text.IndexOf("x := 1;", StringComparison.Ordinal);
				surface.Select(start, "x := 1;\r\n   y := 2;".Length);
				Assert.True(surface.CanExecute(EditorCommand.SurroundWith));
				surface.Execute(EditorCommand.SurroundWith);

				Assert.Equal("procedure start begin\r\n   if (condition) then begin\r\n      x := 1;\r\n      y := 2;\r\n   end\r\nend\r\n", document.TextDocument.Text);
				SnippetPickerRequestView request = new SnippetPickerRequestView(Assert.Single(env.Recorder.SnippetRequests));
				Assert.True(request.Surround);
				Assert.Contains("ifb", request.Triggers);
				Assert.DoesNotContain("callp", request.Triggers);

				document.TextDocument.UndoStack.Undo();
				Assert.Equal("procedure start begin\r\n   x := 1;\r\n   y := 2;\r\nend\r\n", document.TextDocument.Text);
			});
		}

		[Fact]
		public void TheVariablesOfTheContext_AreFilledIn()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				SaveUserSnippets(env, Define("who", "// $SCRIPT_NAME $FILE_NAME in $CURRENT_PROCEDURE$0"));
				DocumentViewModel document = env.CreateDocument("procedure start begin\r\n   who\r\nend\r\n", "C:\\scripts\\gecko.ssl");
				using EditorSession session = env.StartSession(document);
				session.Context.TextArea.Caret.Offset = After(document, "who");

				Assert.True(session.Input.Snippets.TryExpandTrigger());

				Assert.Equal("procedure start begin\r\n   // gecko gecko.ssl in start\r\nend\r\n", document.TextDocument.Text);
			});
		}

		private sealed class SnippetPickerRequestView
		{
			public SnippetPickerRequestView(FalloutScriptStudio.Services.ToolWindows.SnippetPickerRequest request)
			{
				this.Surround = request.Surround;
				this.Triggers = request.Snippets.Select(snippet => snippet.Trigger).ToList();
			}

			public Boolean Surround { get; }

			public List<String> Triggers { get; }
		}
	}
}