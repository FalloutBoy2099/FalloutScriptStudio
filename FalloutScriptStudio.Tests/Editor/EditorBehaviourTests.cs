namespace FalloutScriptStudio.Tests.Editor
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Windows.Media;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Editor;
	using FalloutScriptStudio.Editor.Controllers;
	using FalloutScriptStudio.Editor.Rendering;
	using FalloutScriptStudio.Editor.Tips;
	using FalloutScriptStudio.Tests.Editor.Support;
	using Xunit;

	/// <summary>Zoom persistence, placeholder selection with the keyboard, include errors and the inserted-text mark.</summary>
	public sealed class EditorBehaviourTests
	{
		private const String Script = "procedure start begin\r\n   x := foo({cond}, 1);\r\n   y := {a:b};\r\nend\r\n";

		[Fact]
		public void Zoom_IsStoredInTheSettings()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();

				env.Services.ZoomPercent = 150;

				Assert.Equal(150, env.SettingsService.Settings.Editor.ZoomPercent);
			});
		}

		[Fact]
		public void Zoom_IsRestoredFromTheSettingsWhenTheEditorStarts()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				env.SettingsService.Settings.Editor.ZoomPercent = 180;

				ScriptEditorServices restarted = new ScriptEditorServices(env.SettingsService, env.Theme, env.Workspace, env.CollapsedStore);

				Assert.Equal(180, restarted.ZoomPercent);
			});
		}

		[Fact]
		public void Zoom_IsClampedToTheAllowedRange()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();

				env.Services.ZoomPercent = 5000;
				Assert.Equal(EditorSettings.MaxZoomPercent, env.SettingsService.Settings.Editor.ZoomPercent);

				env.Services.ZoomPercent = 1;
				Assert.Equal(EditorSettings.MinZoomPercent, env.SettingsService.Settings.Editor.ZoomPercent);
			});
		}

		[Fact]
		public void Zoom_FollowsReplacedSettings()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				Int32 raised = 0;
				env.Services.ZoomChanged += (_, _) => raised++;
				AppSettings replacement = env.SettingsService.Settings.Clone();
				replacement.Editor.ZoomPercent = 220;

				env.SettingsService.Apply(replacement);

				Assert.Equal(220, env.Services.ZoomPercent);
				Assert.Equal(1, raised);
			});
		}

		[Fact]
		public void Zoom_ChangeKeepsTheFontSizeOfTheEditorsInSync()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				using EditorSession session = env.StartSession(env.CreateDocument(Script));
				Double normal = session.Context.Editor.FontSize;

				env.Services.ZoomPercent = 200;
				UiThread.Flush();

				Assert.Equal(normal * 2, session.Context.Editor.FontSize, 3);
			});
		}

		[Fact]
		public void Caret_MovedWithTheKeyboardIntoAPlaceholder_SelectsIt()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(Script);
				using EditorSession session = env.StartSession(document);
				Int32 inside = document.TextDocument.Text.IndexOf("{cond}", StringComparison.Ordinal) + 3;

				session.Context.TextArea.Caret.Offset = inside;

				Assert.Equal("{cond}", session.Context.Editor.SelectedText);
				Assert.Equal(inside, session.Context.TextArea.Caret.Offset);
			});
		}

		[Theory]
		[InlineData(true, false)]
		[InlineData(false, true)]
		public void Caret_MovedWhileExtendingTheSelectionOrDraggingWithTheMouse_LeavesThePlaceholderAlone(Boolean shift, Boolean mouse)
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(Script);
				using EditorSession session = env.StartSession(document);
				Int32 inside = document.TextDocument.Text.IndexOf("{cond}", StringComparison.Ordinal) + 3;
				env.Input.Modifiers = shift ? System.Windows.Input.ModifierKeys.Shift : System.Windows.Input.ModifierKeys.None;
				env.Input.IsLeftButtonPressed = mouse;

				session.Context.TextArea.Caret.Offset = inside;

				Assert.Equal(String.Empty, session.Context.Editor.SelectedText);
			});
		}

		[Fact]
		public void Caret_AtTheOpeningBraceAndBeforeTheClosingOne_SelectsThePlaceholder()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(Script);
				using EditorSession session = env.StartSession(document);
				Int32 start = document.TextDocument.Text.IndexOf("{cond}", StringComparison.Ordinal);

				session.Context.TextArea.Caret.Offset = start;
				Assert.Equal("{cond}", session.Context.Editor.SelectedText);

				session.Context.TextArea.ClearSelection();
				session.Context.TextArea.Caret.Offset = start + 5;
				Assert.Equal("{cond}", session.Context.Editor.SelectedText);

				session.Context.TextArea.ClearSelection();
				session.Context.TextArea.Caret.Offset = start + 6;
				Assert.Equal(String.Empty, session.Context.Editor.SelectedText);
			});
		}

		[Fact]
		public void Caret_InsideABraceBlockWithAColon_SelectsNothing()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(Script);
				using EditorSession session = env.StartSession(document);

				session.Context.TextArea.Caret.Offset = document.TextDocument.Text.IndexOf("{a:b}", StringComparison.Ordinal) + 2;

				Assert.Equal(String.Empty, session.Context.Editor.SelectedText);
			});
		}

		[Fact]
		public void Caret_MovedByTyping_DoesNotSelectThePlaceholderItReaches()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument("foo( {cond})");
				using EditorSession session = env.StartSession(document);
				session.Context.TextArea.Caret.Offset = 4;

				TypingSimulator.Type(session, " ");

				Assert.Equal(5, session.Context.TextArea.Caret.Offset);
				Assert.Equal(String.Empty, session.Context.Editor.SelectedText);
			});
		}

		[Fact]
		public void Caret_MovedWithAnExtendedSelection_IsNotReplacedByThePlaceholder()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(Script);
				using EditorSession session = env.StartSession(document);
				session.Context.TextArea.Caret.Offset = 0;
				session.Context.TextArea.Selection = ICSharpCode.AvalonEdit.Editing.Selection.Create(session.Context.TextArea, 0, 4);

				session.Context.TextArea.Caret.Offset = document.TextDocument.Text.IndexOf("{cond}", StringComparison.Ordinal) + 2;

				Assert.Equal("proc", session.Context.Editor.SelectedText);
			});
		}

		[Fact]
		public void Caret_InAMessageFile_DoesNotSelectBraceBlocks()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument("{100}{}{Hello}\r\n", "C:\\text\\test.msg");
				using EditorSession session = env.StartSession(document);

				session.Context.TextArea.Caret.Offset = 2;

				Assert.Equal(String.Empty, session.Context.Editor.SelectedText);
			});
		}

		[Fact]
		public void IncludeErrorNote_IsMarkedWithItsOwnSeverityAndColor()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment(dark: true);
				DocumentViewModel document = env.CreateDocument("#include \"a.h\"\r\n\r\nprocedure start begin\r\nend\r\n");
				document.ParserErrors = new List<BuildError>
				{
					new BuildError(BuildErrorType.Parser, "Error while parsing the contents of the header file.", document.FilePath!, 1),
				};
				using EditorSession session = env.StartSession(document);

				DiagnosticMarker marker = Assert.Single(session.Diagnostics.GetMarkersAt(2));

				Assert.Equal(MarkerSeverity.IncludeError, marker.Severity);
				Assert.Equal(Colors.Lime, ((SolidColorBrush)env.Services.Palette.IncludeErrorUnderline).Color);
				Assert.NotEqual(((SolidColorBrush)env.Services.Palette.ErrorUnderline).Color, ((SolidColorBrush)env.Services.Palette.IncludeErrorUnderline).Color);
			});
		}

		[Fact]
		public void IncludeErrorNote_InTheLightThemeIsBlue()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment(dark: false);

				Assert.Equal(Colors.Blue, ((SolidColorBrush)env.Services.Palette.IncludeErrorUnderline).Color);
			});
		}

		[Fact]
		public void IncludeErrorNote_OnALineThatHasAnErrorAlready_IsLeftOut()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument("#include \"a.h\"\r\n\r\nprocedure start begin\r\nend\r\n");
				document.ParserErrors = new List<BuildError>
				{
					new BuildError(BuildErrorType.Error, "Bad include", document.FilePath!, 1),
					new BuildError(BuildErrorType.Parser, "Error while parsing the contents of the header file.", document.FilePath!, 1),
				};
				using EditorSession session = env.StartSession(document);

				DiagnosticMarker marker = Assert.Single(session.Diagnostics.GetMarkersAt(2));

				Assert.Equal(MarkerSeverity.Error, marker.Severity);
				Assert.Equal(1, session.Diagnostics.Renderer.Count);
			});
		}

		[Fact]
		public void IncludeErrorNote_Tip_IsAnErrorInTheCurrentLanguage()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument("#include \"a.h\"\r\n\r\nprocedure start begin\r\nend\r\n");
				document.ParserErrors = new List<BuildError>
				{
					new BuildError(BuildErrorType.Parser, "not shown", document.FilePath!, 1),
				};
				using EditorSession session = env.StartSession(document);

				TipInfo tip = Assert.Single(session.Tips.BuildTips(2));

				Assert.Equal(TipKind.Error, tip.Kind);
				Assert.DoesNotContain("not shown", tip.Body, StringComparison.Ordinal);
				Assert.False(String.IsNullOrWhiteSpace(tip.Body));
			});
		}

		[Fact]
		public void InsertedText_IsMarkedUntilTheNextEdit()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(Script);
				using EditorSession session = env.StartSession(document);
				EditorSurface surface = new EditorSurface(session);

				surface.HighlightInsertedText(0, 9);
				Assert.Equal(1, session.Occurrences.FlashCount);

				document.TextDocument.Insert(0, "x");
				Assert.Equal(0, session.Occurrences.FlashCount);
			});
		}

		[Fact]
		public void InsertedText_ReplacesTheHighlightedWordAndIsReplacedByTheNextHighlight()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(Script);
				using EditorSession session = env.StartSession(document);
				session.Occurrences.Highlight("start", true);
				Assert.Equal(1, session.Occurrences.OccurrenceCount);

				session.Occurrences.HighlightInserted(0, 9);
				Assert.Equal(0, session.Occurrences.OccurrenceCount);
				Assert.Equal(1, session.Occurrences.FlashCount);

				session.Occurrences.Highlight("procedure", true);
				Assert.Equal(0, session.Occurrences.FlashCount);
				Assert.Equal(1, session.Occurrences.OccurrenceCount);
			});
		}

		[Fact]
		public void InsertedHighlight_UsesTheLegacyColorsOfTheTheme()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment dark = new EditorTestEnvironment(dark: true);
				using EditorTestEnvironment light = new EditorTestEnvironment(dark: false);

				Assert.Equal(Colors.DarkSlateGray, ((SolidColorBrush)dark.Services.Palette.IncludeHighlight).Color);
				Assert.Equal(Colors.Beige, ((SolidColorBrush)light.Services.Palette.IncludeHighlight).Color);
			});
		}

		[Fact]
		public void DroppedFiles_GoThroughTheWorkspaceDropPath()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				using EditorSession session = env.StartSession(env.CreateDocument(Script));
				System.Windows.DataObject data = new System.Windows.DataObject(System.Windows.DataFormats.FileDrop, new[] { "C:\\scripts\\a.ssl", "C:\\scripts\\b.ssl" });

				Boolean handled = session.Input.TryOpenDroppedFiles(data);

				Assert.True(handled);
				Assert.Contains("OpenDroppedFilesAsync(System.String[])", env.Recorder.Calls);
				Assert.DoesNotContain(env.Recorder.Calls, call => call.StartsWith("OpenAsync", StringComparison.Ordinal));
			});
		}

		[Fact]
		public void DroppedText_IsNotTreatedAsFiles()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				using EditorSession session = env.StartSession(env.CreateDocument(Script));

				Assert.False(session.Input.TryOpenDroppedFiles(new System.Windows.DataObject(System.Windows.DataFormats.Text, "hello")));
				Assert.DoesNotContain(env.Recorder.Calls, call => call.StartsWith("OpenDropped", StringComparison.Ordinal));
			});
		}
	}
}