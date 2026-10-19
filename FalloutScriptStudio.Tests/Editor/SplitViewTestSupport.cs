namespace FalloutScriptStudio.Tests.Editor
{
	using FalloutScriptStudio.Tests.Support;
	using System;
	using System.Windows;
	using System.Windows.Input;
	using FalloutScriptStudio.Editor;
	using FalloutScriptStudio.Tests.Editor.Support;
	using ICSharpCode.AvalonEdit;

	internal static class SplitViewTestSupport
	{
		internal const String Script =
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

		internal static Window Show(FrameworkElement view)
		{
			Window window = OffscreenWindow.Show(view, 800, 600);
			UiThread.Flush();
			return window;
		}

		internal static EditorPane Split(EditorSession session)
		{
			TextEditor editor = new TextEditor();
			EditorPane pane = session.ShowSecondary(editor);
			editor.Measure(new Size(900, 300));
			editor.Arrange(new Rect(0, 0, 900, 300));
			editor.UpdateLayout();
			return pane;
		}

		internal static void GiveFocus(EditorPane pane)
		{
			KeyboardFocusChangedEventArgs args = new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, 0, null, pane.Context.TextArea)
			{
				RoutedEvent = Keyboard.GotKeyboardFocusEvent,
			};
			pane.Context.TextArea.RaiseEvent(args);
		}
	}
}