namespace FalloutScriptStudio.Tests.Editor
{
	using System;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.Formatting;
	using FalloutScriptStudio.Core.TextTools;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Editor;
	using FalloutScriptStudio.Tests.Editor.Support;
	using Xunit;

	/// <summary>Format Document, Format Selection and the formatting after a semicolon follow the style of the settings.</summary>
	[Collection("Test collection for FalloutScriptStudio.Tests.Editor.EditorSessionTests")]
	public sealed class EditorFormattingTests
	{
		private const String Messy = "procedure start\r\nbegin\r\nvariable x:=1;\r\nif (x==1) then begin\r\ny:=2;\r\nend\r\nend\r\n";

		[Fact]
		public void FormatDocument_UsesTheStyleOfTheSettings_AsOneUndoStep()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				env.SettingsService.Settings.Editor.Formatting = new FormatSettings { Reindent = true, Begin = BeginPlacement.SameLine, Else = ElsePlacement.AfterEnd };
				DocumentViewModel document = env.CreateDocument(Messy);
				using EditorSession session = env.StartSession(document);
				IEditorSurface surface = new EditorSurface(session);

				surface.Execute(EditorCommand.FormatDocument);

				Assert.Equal("procedure start begin\r\n   variable x := 1;\r\n   if (x == 1) then begin\r\n      y := 2;\r\n   end\r\nend\r\n", document.TextDocument.Text);
				document.TextDocument.UndoStack.Undo();
				Assert.Equal(Messy, document.TextDocument.Text);
			});
		}

		[Fact]
		public void FormatDocument_WithTheDefaultStyle_OnlyPutsSpacesAroundOperators()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(Messy);
				using EditorSession session = env.StartSession(document);
				IEditorSurface surface = new EditorSurface(session);

				surface.Execute(EditorCommand.FormatDocument);

				Assert.Equal("procedure start\r\nbegin\r\nvariable x := 1;\r\nif (x == 1) then begin\r\ny := 2;\r\nend\r\nend\r\n", document.TextDocument.Text);
			});
		}

		[Fact]
		public void FormatSelection_ChangesOnlyTheSelectedLines_ButIndentsThemByTheBlocksAroundThem()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				env.SettingsService.Settings.Editor.Formatting = new FormatSettings { Reindent = true };
				DocumentViewModel document = env.CreateDocument(Messy);
				using EditorSession session = env.StartSession(document);
				IEditorSurface surface = new EditorSurface(session);
				Int32 start = Messy.IndexOf("if (", StringComparison.Ordinal);
				surface.Select(start, Messy.IndexOf("end\r\nend", StringComparison.Ordinal) - start);

				surface.Execute(EditorCommand.FormatSelection);

				Assert.Equal("procedure start\r\nbegin\r\nvariable x:=1;\r\n   if (x == 1) then begin\r\n      y := 2;\r\nend\r\nend\r\n", document.TextDocument.Text);
			});
		}

		[Fact]
		public void FormatSelection_WithoutASelection_FormatsTheLineOfTheCaret()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				env.SettingsService.Settings.Editor.Formatting = new FormatSettings { Reindent = true };
				DocumentViewModel document = env.CreateDocument(Messy);
				using EditorSession session = env.StartSession(document);
				IEditorSurface surface = new EditorSurface(session);
				surface.CaretOffset = Messy.IndexOf("variable", StringComparison.Ordinal) + 3;

				surface.Execute(EditorCommand.FormatSelection);

				Assert.Equal("procedure start\r\nbegin\r\n   variable x := 1;\r\nif (x==1) then begin\r\ny:=2;\r\nend\r\nend\r\n", document.TextDocument.Text);
			});
		}

		[Fact]
		public void TheStyleOfTheProject_WinsOverTheGlobalStyle()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				env.SettingsService.Settings.Editor.Formatting = new FormatSettings { Reindent = true };
				env.SettingsService.SetActiveProject("C:\\mod", new ProjectSettings { Formatting = new FormatSettings { OperatorSpacing = SpacingStyle.Remove, AssignmentSpacing = SpacingStyle.Remove } });
				DocumentViewModel document = env.CreateDocument("x := 1 + 2;");
				using EditorSession session = env.StartSession(document);
				IEditorSurface surface = new EditorSurface(session);

				surface.Execute(EditorCommand.FormatDocument);

				Assert.Equal("x:=1+2;", document.TextDocument.Text);
			});
		}

		[Fact]
		public void TypingASemicolon_FormatsTheLineBeforeTheCaret_AccordingToTheSpacingRules()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(String.Empty);
				using EditorSession session = env.StartSession(document);

				TypingSimulator.Type(session, "x:=a+b;");

				Assert.Equal("x := a + b;", document.TextDocument.Text);
			});
		}

		[Fact]
		public void TypingASemicolon_LeavesTheLineAlone_WhenTheSpacingRulesKeepTheText()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				env.SettingsService.Settings.Editor.Formatting = new FormatSettings { OperatorSpacing = SpacingStyle.Keep, AssignmentSpacing = SpacingStyle.Keep, CommaSpacing = SpacingStyle.Keep };
				DocumentViewModel document = env.CreateDocument(String.Empty);
				using EditorSession session = env.StartSession(document);

				TypingSimulator.Type(session, "x:=a+b;");

				Assert.Equal("x:=a+b;", document.TextDocument.Text);
			});
		}

		[Fact]
		public void TheCommandsOfAMessageFile_StayDisabled()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel message = env.CreateDocument("{100}{}{text}", "C:\\text\\a.msg");
				using EditorSession session = env.StartSession(message);
				IEditorSurface surface = new EditorSurface(session);

				Assert.False(surface.CanExecute(EditorCommand.FormatDocument));
				Assert.False(surface.CanExecute(EditorCommand.FormatSelection));
			});
		}
	}
}