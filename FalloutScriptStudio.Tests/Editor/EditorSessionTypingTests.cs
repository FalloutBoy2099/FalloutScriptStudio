namespace FalloutScriptStudio.Tests.Editor
{
	using System;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Editor;
	using FalloutScriptStudio.Tests.Editor.Support;
	using Xunit;

	[Collection("Test collection for FalloutScriptStudio.Tests.Editor.EditorSessionTests")]
	public sealed class EditorSessionTypingTests
	{
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