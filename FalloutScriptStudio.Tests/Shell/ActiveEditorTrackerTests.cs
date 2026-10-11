namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Tests.Shell.Support;
	using FalloutScriptStudio.Workspace;
	using Xunit;

	public sealed class ActiveEditorTrackerTests
	{
		private const String Script = "procedure start begin\r\nend\r\n";

		[Fact]
		public void Events_of_the_editor_of_the_active_document_are_forwarded()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				using ActiveEditorTracker tracker = new ActiveEditorTracker(harness.Workspace);
				Int32 carets = 0;
				Int32 zooms = 0;
				Int32 procedures = 0;
				tracker.CaretMoved += (_, _) => carets++;
				tracker.ZoomChanged += (_, _) => zooms++;
				tracker.CurrentProcedureChanged += (_, _) => procedures++;
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				FakeEditorSurface editor = harness.AttachEditor(document);

				editor.GoTo(2);
				editor.RaiseZoomChanged();
				editor.RaiseProcedureChanged();

				Assert.Same(editor, tracker.Editor);
				Assert.Same(document, tracker.Document);
				Assert.True(carets >= 1);
				Assert.Equal(1, zooms);
				Assert.Equal(1, procedures);
			});
		}

		[Fact]
		public void Events_of_a_background_editor_are_ignored()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				using ActiveEditorTracker tracker = new ActiveEditorTracker(harness.Workspace);
				DocumentViewModel first = await harness.OpenFileAsync("a.ssl", Script);
				FakeEditorSurface firstEditor = harness.AttachEditor(first);
				await harness.OpenFileAsync("b.ssl", Script);
				Int32 carets = 0;
				tracker.CaretMoved += (_, _) => carets++;

				firstEditor.GoTo(2);

				Assert.Equal(0, carets);
				Assert.Null(tracker.Editor);
			});
		}

		[Fact]
		public void The_change_event_is_raised_when_the_view_attaches_its_editor_later()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				using ActiveEditorTracker tracker = new ActiveEditorTracker(harness.Workspace);
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				Int32 changes = 0;
				tracker.EditorChanged += (_, _) => changes++;

				harness.AttachEditor(document);

				Assert.Equal(1, changes);
				Assert.NotNull(tracker.Editor);
			});
		}

		[Fact]
		public void Disposing_stops_the_forwarding()
		{
			ShellStaRunner.Run(async () =>
			{
				using WorkspaceHarness harness = new WorkspaceHarness();
				ActiveEditorTracker tracker = new ActiveEditorTracker(harness.Workspace);
				Int32 changes = 0;
				tracker.EditorChanged += (_, _) => changes++;
				tracker.Dispose();

				await harness.OpenFileAsync("a.ssl", Script);

				Assert.Equal(0, changes);
			});
		}
	}
}
