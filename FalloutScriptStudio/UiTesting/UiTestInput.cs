namespace FalloutScriptStudio.UiTesting
{
	using System;
	using System.Windows;
	using System.Windows.Input;
	using System.Windows.Media;
	using System.Windows.Threading;
	using ICSharpCode.AvalonEdit.Editing;

	/// <summary>
	/// Delivers keys and text to the code editor of the main window for the UI tests. A program that is not in the foreground has no keyboard
	/// focus and receives no key from the keyboard, and giving it the focus would take the focus from the developer, so the test sends the
	/// command (<c>key:Tab</c>, <c>text:callp</c>) in a <c>WM_COPYDATA</c> message and the program raises the same routed events at the text
	/// area that the keyboard would. The events go through the handlers of the editor (snippets, completion, brackets) exactly as typed keys do.
	/// </summary>
	public static class UiTestInput
	{
		/// <summary>The identifier that marks a <c>WM_COPYDATA</c> message as a command of the UI tests.</summary>
		public const UInt32 CommandId = 0x55AA7E57;

		/// <summary>The prefix of a command that presses a key (the name of a <see cref="Key"/>).</summary>
		public const String KeyPrefix = "key:";

		/// <summary>The prefix of a command that types text.</summary>
		public const String TextPrefix = "text:";

		/// <summary>Queues a command for the user interface thread.</summary>
		/// <param name="command">The command.</param>
		public static void Post(String command)
		{
			ArgumentNullException.ThrowIfNull(command);
			_ = Application.Current?.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => Run(command)));
		}

		/// <summary>Runs a command: presses a key or types text at the text area of the editor of the active document.</summary>
		/// <param name="command">The command.</param>
		public static void Run(String command)
		{
			ArgumentNullException.ThrowIfNull(command);
			if (Application.Current?.MainWindow is { } window)
			{
				Run(window, command);
			}
		}

		/// <summary>Runs a command at the editor of a window.</summary>
		/// <param name="window">The window that holds the editor.</param>
		/// <param name="command">The command.</param>
		public static void Run(Window window, String command)
		{
			ArgumentNullException.ThrowIfNull(window);
			ArgumentNullException.ThrowIfNull(command);
			if (FindTextArea(window) is not { } target)
			{
				return;
			}

			if (command.StartsWith(TextPrefix, StringComparison.Ordinal))
			{
				foreach (Char character in command[TextPrefix.Length..])
				{
					TypeCharacter(target, character.ToString());
				}
			}
			else if (command.StartsWith(KeyPrefix, StringComparison.Ordinal) && Enum.TryParse(command[KeyPrefix.Length..], ignoreCase: true, out Key key))
			{
				PressKey(target, key);
			}
		}

		private static void TypeCharacter(TextArea target, String text)
		{
			TextComposition composition = new(InputManager.Current, target, text);
			TextCompositionEventArgs preview = new(Keyboard.PrimaryDevice, composition) { RoutedEvent = TextCompositionManager.PreviewTextInputEvent };
			target.RaiseEvent(preview);
			if (!preview.Handled)
			{
				target.RaiseEvent(new TextCompositionEventArgs(Keyboard.PrimaryDevice, composition) { RoutedEvent = TextCompositionManager.TextInputEvent });
			}
		}

		private static void PressKey(TextArea target, Key key)
		{
			PresentationSource? source = PresentationSource.FromVisual(target);
			if (source is null)
			{
				return;
			}

			Int32 timestamp = Environment.TickCount;
			KeyEventArgs preview = new(Keyboard.PrimaryDevice, source, timestamp, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
			target.RaiseEvent(preview);
			if (!preview.Handled && key == Key.Return)
			{
				// The editor starts a new line as text input, and only when it has the keyboard focus (its Enter command checks it): the
				// program never has the focus.
				TypeCharacter(target, "\n");
			}
			else if (!preview.Handled)
			{
				target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, timestamp, key) { RoutedEvent = Keyboard.KeyDownEvent });
			}

			target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, timestamp, key) { RoutedEvent = Keyboard.KeyUpEvent });
		}

		private static TextArea? FindTextArea(DependencyObject parent)
		{
			// The text areas of the main window are the editors of the documents; the one that is shown is the active one.
			Int32 count = VisualTreeHelper.GetChildrenCount(parent);
			for (Int32 index = 0; index < count; index++)
			{
				DependencyObject child = VisualTreeHelper.GetChild(parent, index);
				if (child is TextArea { IsVisible: true } area)
				{
					return area;
				}

				if (FindTextArea(child) is { } found)
				{
					return found;
				}
			}

			return null;
		}
	}
}