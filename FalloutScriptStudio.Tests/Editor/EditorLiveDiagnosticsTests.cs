namespace FalloutScriptStudio.Tests.Editor
{
	using System;
	using System.Linq;
	using FalloutScriptStudio.Core.Analysis;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Editor;
	using FalloutScriptStudio.Editor.Rendering;
	using FalloutScriptStudio.Editor.Text;
	using FalloutScriptStudio.Tests.Editor.Support;
	using Xunit;

	/// <summary>The semantic diagnostics in the editor: the underlines and their tool tips, and the Quick Fix command (Ctrl+.).</summary>
	[Collection("Test collection for FalloutScriptStudio.Tests.Editor.EditorSessionTests")]
	public sealed class EditorLiveDiagnosticsTests
	{
		private const String Typo = "procedure start begin\r\n   display_msgg(\"a\");\r\n   display_msg(\"a\", \"b\");\r\nend";

		private static void Publish(DocumentViewModel document)
		{
			document.LiveDiagnosticsVersion = document.TextDocument.Version;
			document.LiveDiagnostics = ScriptDiagnostics.Analyze(document.TextDocument.Text, new ScriptEnvironment { Functions = FunctionSignatureCatalog.Create(new[] { "void display_msg (string message) | - shows" }, Array.Empty<String>()) });
		}

		private static Int32 OffsetOf(DocumentViewModel document, String text, Int32 inside = 1)
		{
			return document.TextDocument.Text.IndexOf(text, StringComparison.Ordinal) + inside;
		}

		[Fact]
		public void The_findings_are_underlined_with_the_color_of_their_severity_and_their_message_as_the_tool_tip()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel script = env.CreateDocument(Typo);
				using EditorSession session = env.StartSession(script);

				Publish(script);

				DiagnosticMarker error = Assert.Single(session.Diagnostics.GetMarkersAt(OffsetOf(script, "display_msgg")));
				Assert.Equal(MarkerSeverity.Error, error.Severity);
				Assert.StartsWith("Unknown name 'display_msgg'", error.Message, StringComparison.Ordinal);
				Assert.Contains("Did you mean 'display_msg'?", error.Message, StringComparison.Ordinal);
				Assert.Contains(ScriptFixText.QuickFixGestureText, error.Message, StringComparison.Ordinal);
				Assert.Equal("display_msgg".Length, error.Length);
				DiagnosticMarker warning = Assert.Single(session.Diagnostics.GetMarkersAt(OffsetOf(script, "display_msg(\"a\", \"b\")")));
				Assert.Equal(MarkerSeverity.Warning, warning.Severity);
				Assert.DoesNotContain(ScriptFixText.QuickFixGestureText, warning.Message, StringComparison.Ordinal);
			});
		}

		[Fact]
		public void Findings_made_for_another_text_are_not_drawn()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel script = env.CreateDocument(Typo);
				using EditorSession session = env.StartSession(script);
				Publish(script);
				Assert.NotEmpty(session.Diagnostics.GetMarkersAt(OffsetOf(script, "display_msgg")));

				script.TextDocument.Insert(0, "// moved\r\n");
				session.Diagnostics.Update();

				Assert.Empty(session.Diagnostics.GetMarkersAt(OffsetOf(script, "display_msgg")));
			});
		}

		[Fact]
		public void The_underlines_follow_the_text_while_it_is_edited()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel script = env.CreateDocument(Typo);
				using EditorSession session = env.StartSession(script);
				Publish(script);

				script.TextDocument.Insert(0, "// moved\r\n");

				Assert.Single(session.Diagnostics.GetMarkersAt(OffsetOf(script, "display_msgg")));
			});
		}

		[Fact]
		public void Setting_the_findings_of_the_document_redraws_the_underlines()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel script = env.CreateDocument(Typo);
				using EditorSession session = env.StartSession(script);
				Assert.Equal(0, session.Diagnostics.Renderer.Count);

				Publish(script);
				System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);

				Assert.Equal(2, session.Diagnostics.Renderer.Count);
				script.LiveDiagnostics = Array.Empty<ScriptDiagnostic>();
				System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
				Assert.Equal(0, session.Diagnostics.Renderer.Count);
			});
		}

		[Fact]
		public void The_view_toggle_of_the_parse_error_underlines_hides_them_too()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				env.Services.ViewOptions.HighlightParseErrors = false;
				DocumentViewModel script = env.CreateDocument(Typo);
				using EditorSession session = env.StartSession(script);

				Publish(script);

				Assert.Equal(0, session.Diagnostics.Renderer.Count);
			});
		}

		[Fact]
		public void Headers_and_other_files_get_no_underlines()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel header = env.CreateDocument(Typo, "C:\\scripts\\lib.h");
				using EditorSession session = env.StartSession(header);

				Publish(header);

				Assert.Equal(0, session.Diagnostics.Renderer.Count);
			});
		}

		[Fact]
		public void Quick_fix_is_offered_only_where_a_finding_has_a_fix_and_asks_the_workspace()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel script = env.CreateDocument(Typo);
				using EditorSession session = env.StartSession(script);
				IEditorSurface surface = new EditorSurface(session);
				Publish(script);

				surface.CaretOffset = OffsetOf(script, "procedure start");
				Assert.False(surface.CanExecute(EditorCommand.QuickFix));
				surface.CaretOffset = OffsetOf(script, "display_msg(\"a\", \"b\")");
				Assert.False(surface.CanExecute(EditorCommand.QuickFix));
				Int32 offset = OffsetOf(script, "display_msgg", 4);
				surface.CaretOffset = offset;
				Assert.True(surface.CanExecute(EditorCommand.QuickFix));
				surface.Execute(EditorCommand.QuickFix);

				Assert.Contains(env.Recorder.Calls, call => call.StartsWith("ApplyQuickFixAtAsync(", StringComparison.Ordinal) && call.EndsWith(", " + offset + ")", StringComparison.Ordinal));
			});
		}

		[Fact]
		public void Quick_fix_is_not_offered_in_a_header_or_without_findings()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel script = env.CreateDocument(Typo);
				using EditorSession session = env.StartSession(script);
				IEditorSurface surface = new EditorSurface(session);
				surface.CaretOffset = OffsetOf(script, "display_msgg", 4);

				Assert.False(surface.CanExecute(EditorCommand.QuickFix));
			});
		}

		[Fact]
		public void Quick_fix_has_the_gesture_ctrl_period_and_is_in_the_context_menu_of_the_editor()
		{
			UiThread.Run(() =>
			{
				System.Windows.Input.RoutedUICommand command = EditorCommands.Get(EditorCommand.QuickFix);
				System.Windows.Input.KeyGesture gesture = Assert.IsType<System.Windows.Input.KeyGesture>(Assert.Single(command.InputGestures));

				Assert.Equal(System.Windows.Input.Key.OemPeriod, gesture.Key);
				Assert.Equal(System.Windows.Input.ModifierKeys.Control, gesture.Modifiers);
				Assert.Equal("Ctrl+.", ScriptFixText.QuickFixGestureText);
				Assert.Equal("Quick Fix", command.Text);
			});
		}
	}
}