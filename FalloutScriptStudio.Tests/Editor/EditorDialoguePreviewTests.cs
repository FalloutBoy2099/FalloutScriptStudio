namespace FalloutScriptStudio.Tests.Editor
{
	using System;
	using System.Linq;
	using System.Windows;
	using System.Windows.Controls;
	using System.Windows.Input;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Editor;
	using FalloutScriptStudio.Tests.Editor.Support;
	using FalloutScriptStudio.Tests.Support;
	using FalloutScriptStudio.Views.Editor;
	using Xunit;

	public sealed class EditorDialoguePreviewTests
	{
		private static readonly String _script = String.Join(
			"\r\n",
			"procedure start;",              // 1
			"procedure talk_p_proc;",        // 2
			"procedure Node001;",            // 3
			"procedure helper;",             // 4
			String.Empty,                    // 5
			"procedure start begin",         // 6
			"end",                           // 7
			String.Empty,                    // 8
			"procedure talk_p_proc begin",   // 9
			"   Reply(100);",                // 10
			"end",                           // 11
			String.Empty,                    // 12
			"procedure Node001 begin",       // 13
			"   Reply(101);",                // 14
			"end",                           // 15
			String.Empty,                    // 16
			"procedure helper begin",        // 17
			"   display_msg(\"x\");",        // 18
			"end",                           // 19
			String.Empty);

		private static Int32 OffsetOfLine(DocumentViewModel document, Int32 line)
		{
			return document.TextDocument.GetLineByNumber(line).Offset + 4;
		}

		[Fact]
		public void The_command_has_the_gesture_that_the_documentation_lists()
		{
			KeyGesture gesture = Assert.IsType<KeyGesture>(Assert.Single(EditorCommands.Get(EditorCommand.PreviewDialogueFromNode).InputGestures));

			Assert.Equal("Ctrl+Shift+P", gesture.GetDisplayStringForCulture(System.Globalization.CultureInfo.InvariantCulture));
		}

		[Theory]
		[InlineData(10, "talk_p_proc")]
		[InlineData(14, "Node001")]
		public void The_command_previews_the_node_that_contains_the_caret(Int32 line, String expected)
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel script = env.CreateDocument(_script);
				using EditorSession session = env.StartSession(script);
				IEditorSurface surface = new EditorSurface(session);
				surface.CaretOffset = OffsetOfLine(script, line);

				Assert.True(surface.CanExecute(EditorCommand.PreviewDialogueFromNode));
				surface.Execute(EditorCommand.PreviewDialogueFromNode);

				Assert.Contains(env.Recorder.Calls, call => call.StartsWith("PreviewDialogueFromNodeAsync(", StringComparison.Ordinal) && call.EndsWith(", " + expected + ")", StringComparison.Ordinal));
			});
		}

		[Theory]
		[InlineData(1)]
		[InlineData(7)]
		[InlineData(18)]
		public void The_command_is_not_available_outside_a_dialogue_node(Int32 line)
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel script = env.CreateDocument(_script);
				using EditorSession session = env.StartSession(script);
				IEditorSurface surface = new EditorSurface(session);
				surface.CaretOffset = OffsetOfLine(script, line);

				Assert.False(surface.CanExecute(EditorCommand.PreviewDialogueFromNode));
				surface.Execute(EditorCommand.PreviewDialogueFromNode);

				Assert.DoesNotContain(env.Recorder.Calls, call => call.StartsWith("PreviewDialogueFromNodeAsync(", StringComparison.Ordinal));
			});
		}

		[Fact]
		public void The_command_is_not_available_in_a_message_file()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel messages = env.CreateDocument("{100}{}{Node}\r\n", "C:\\scripts\\node.msg");
				using EditorSession session = env.StartSession(messages);
				IEditorSurface surface = new EditorSurface(session);

				Assert.False(surface.CanExecute(EditorCommand.PreviewDialogueFromNode));
			});
		}

		[Fact]
		public void The_context_menu_offers_the_command_only_while_the_caret_is_in_a_node()
		{
			UiThread.Run(() =>
			{
				using EditorTestEnvironment env = new EditorTestEnvironment();
				DocumentViewModel document = env.CreateDocument(_script);
				ScriptEditorView view = new ScriptEditorView(env.Services) { DataContext = document };
				Window window = OffscreenWindow.Show(view, 800, 500);
				UiThread.Flush();
				ContextMenu menu = view.Editor.ContextMenu!;
				MenuItem item = menu.Items.OfType<MenuItem>().Single(candidate => candidate.Command == EditorCommands.Get(EditorCommand.PreviewDialogueFromNode));

				view.Surface!.CaretOffset = OffsetOfLine(document, 14);
				menu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent, menu));
				UiThread.Flush();
				Assert.Equal(Visibility.Visible, item.Visibility);
				Assert.Equal("Preview Dialogue From This Node", EditorCommands.Get(EditorCommand.PreviewDialogueFromNode).Text);

				view.Surface.CaretOffset = OffsetOfLine(document, 18);
				menu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent, menu));
				UiThread.Flush();
				Assert.Equal(Visibility.Collapsed, item.Visibility);
				window.Close();
			});
		}
	}
}