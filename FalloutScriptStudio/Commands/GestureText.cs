namespace FalloutScriptStudio.Commands
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Windows.Input;

	/// <summary>Writes a keyboard shortcut the way the menus of the application show it: <c>Ctrl+Alt+Shift+Key</c>, with the signs and digits as they are on the key.</summary>
	public static class GestureText
	{
		/// <summary>Formats a key gesture.</summary>
		/// <param name="gesture">The gesture.</param>
		/// <returns>The text, for example <c>Ctrl+Shift+S</c>.</returns>
		public static String Format(KeyGesture gesture)
		{
			ArgumentNullException.ThrowIfNull(gesture);
			return Format(gesture.Key, gesture.Modifiers);
		}

		/// <summary>Formats a key and its modifiers.</summary>
		/// <param name="key">The key.</param>
		/// <param name="modifiers">The modifier keys.</param>
		/// <returns>The text, for example <c>Ctrl+Shift+S</c>.</returns>
		public static String Format(Key key, ModifierKeys modifiers)
		{
			List<String> parts = [];
			if (modifiers.HasFlag(ModifierKeys.Control))
			{
				parts.Add("Ctrl");
			}

			if (modifiers.HasFlag(ModifierKeys.Alt))
			{
				parts.Add("Alt");
			}

			if (modifiers.HasFlag(ModifierKeys.Shift))
			{
				parts.Add("Shift");
			}

			if (modifiers.HasFlag(ModifierKeys.Windows))
			{
				parts.Add("Win");
			}

			parts.Add(KeyName(key));
			return String.Join('+', parts);
		}

		/// <summary>Formats all the gestures of a command as the menus do: <c>Ctrl+Space / Alt+Space</c>.</summary>
		/// <param name="gestures">The gestures; the ones that are not key gestures are skipped.</param>
		/// <returns>The text, or <see langword="null"/> when there is no key gesture.</returns>
		public static String? FormatAll(IEnumerable<InputGesture> gestures)
		{
			ArgumentNullException.ThrowIfNull(gestures);
			String[] texts = gestures.OfType<KeyGesture>().Select(Format).ToArray();
			return texts.Length == 0 ? null : String.Join(" / ", texts);
		}

		private static String KeyName(Key key)
		{
			return key switch
			{
				Key.OemPlus or Key.Add => "+",
				Key.OemMinus or Key.Subtract => "-",
				Key.OemQuestion or Key.Divide => "/",
				Key.OemPeriod => ".",
				Key.Multiply => "*",
				Key.Cancel => "Break",
				>= Key.D0 and <= Key.D9 => ((Int32)key - (Int32)Key.D0).ToString(System.Globalization.CultureInfo.InvariantCulture),
				_ => key.ToString(),
			};
		}
	}
}