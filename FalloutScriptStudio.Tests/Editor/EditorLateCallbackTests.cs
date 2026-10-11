namespace FalloutScriptStudio.Tests.Editor
{
	using System;
	using System.Linq;
	using System.Windows.Input;
	using System.Windows.Interop;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Editor;
	using FalloutScriptStudio.Editor.Rendering;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Tests.Editor.Support;
	using ICSharpCode.AvalonEdit;
	using Xunit;

	/// <summary>Work that was queued on the dispatcher must not touch an editor control after its session was stopped.</summary>
	public sealed class EditorLateCallbackTests
	{
		private const String Script = "procedure other;\r\n\r\nprocedure other begin\r\n   ot\r\nend\r\n";

		private static Int32 CountTabMarkers(TextEditor editor)
		{
			return editor.TextArea.TextView.BackgroundRenderers.OfType<TabMarkerRenderer>().Count();
		}

		[Fact]
		public void ZoomThenDispose_QueuedApplyDoesNotAddATabMarkerRendererToTheSharedControl()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(Script);
				EditorSession session = env.StartSession(document);
				TextEditor editor = session.Context.Editor;
				Assert.Equal(1, CountTabMarkers(editor));

				env.Services.ZoomPercent = 150;
				session.Dispose();
				UiThread.Flush();

				Assert.Equal(0, CountTabMarkers(editor));
			});
		}

		[Fact]
		public void OptionsThenStop_ThenANewSessionOnTheSameControl_HasASingleTabMarkerRenderer()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(Script);
				TextEditor editor = new TextEditor();
				EditorSession first = new EditorSession(editor, document, env.Services, () => { });
				first.Start();

				env.Services.ZoomPercent = 130;
				first.Stop();
				using EditorSession second = new EditorSession(editor, document, env.Services, () => { });
				second.Start();
				UiThread.Flush();

				Assert.Equal(1, CountTabMarkers(editor));
				first.Dispose();
				Assert.Equal(1, CountTabMarkers(editor));
			});
		}

		[Fact]
		public void Zoom_AppliesTheAppearanceOnlyOnce()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(Script);
				using EditorSession session = env.StartSession(document);
				Int32 applied = 0;
				session.AppearanceApplied += (_, _) => applied++;
				Double before = session.Context.Editor.FontSize;

				env.Services.ZoomPercent = 200;
				UiThread.Flush();

				Assert.Equal(1, applied);
				Assert.True(session.Context.Editor.FontSize > before);
				Assert.Equal(1, CountTabMarkers(session.Context.Editor));
			});
		}

		[Fact]
		public void SeveralChangesBeforeTheDispatcherRuns_AreAppliedOnce()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(Script);
				using EditorSession session = env.StartSession(document);
				Int32 applied = 0;
				session.AppearanceApplied += (_, _) => applied++;

				env.Services.ZoomPercent = 120;
				env.Services.ZoomPercent = 140;
				env.Theme.Apply(AppTheme.Dark);
				UiThread.Flush();

				Assert.Equal(1, applied);
			});
		}

		[Fact]
		public void DisposeTwice_IsHarmless_AndApplyAfterDisposeDoesNothing()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(Script);
				EditorSession session = env.StartSession(document);
				TextEditor editor = session.Context.Editor;

				session.Dispose();
				session.Dispose();
				session.Appearance.Dispose();
				session.Appearance.Apply();
				session.Appearance.ApplyTo(editor);

				Assert.Equal(0, CountTabMarkers(editor));
			});
		}

		[Fact]
		public void BackspaceThenStop_QueuedCompletionDoesNotOpenOnTheControlOfTheNextSession()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				TextEditor editor = new TextEditor();
				DocumentViewModel firstDocument = env.CreateDocument(Script);
				EditorSession first = new EditorSession(editor, firstDocument, env.Services, () => { });
				first.Start();
				Int32 caret = firstDocument.TextDocument.Text.IndexOf("   ot", StringComparison.Ordinal) + 5;
				first.Context.TextArea.Caret.Offset = caret;
				Assert.True(first.Completion.Show(true));

				using HwndSource source = new HwndSource(0, 0, 0, 0, 0, "late-callback-test", IntPtr.Zero);
				KeyEventArgs args = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Back) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
				editor.TextArea.RaiseEvent(args);
				first.Stop();
				using EditorSession second = new EditorSession(editor, env.CreateDocument(Script), env.Services, () => { });
				second.Start();
				second.Context.TextArea.Caret.Offset = caret;
				UiThread.Flush();

				Assert.False(first.Completion.IsOpen);
				Assert.False(second.Completion.IsOpen);
				first.Dispose();
			});
		}
	}
}
