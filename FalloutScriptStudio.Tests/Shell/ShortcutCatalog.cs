namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using System.Text.RegularExpressions;
	using System.Windows.Input;
	using System.Xml.Linq;
	using FalloutScriptStudio.Editor;
	using FalloutScriptStudio.Tests.Accessibility;
	using FalloutScriptStudio.Tests.Editor.Support;

	/// <summary>
	/// The keyboard shortcuts as they are really bound: the commands of the code editor and every <c>KeyBinding</c> of the windows. The
	/// documentation <c>docs\SHORTCUTS.md</c> is generated from this catalog and compared with it by a test, so a binding that is added
	/// without being documented (or a documented shortcut that was removed) fails the build.
	/// </summary>
	internal static class ShortcutCatalog
	{
		private const String ManualSection = "Keys handled by the controls";

		private static readonly XNamespace _presentation = SourceTree.Presentation;

		/// <summary>Gets the shortcuts: scope, normalized gesture and what they do.</summary>
		public static IReadOnlyList<(String Scope, String Gesture, String Description)> Read()
		{
			List<(String Scope, String Gesture, String Description)> entries = [];
			Dictionary<String, String> english = SourceTree.LoadStrings("en");

			// The code editor: the gestures of the commands of the editor, whatever the window.
			UiThread.Run(() =>
			{
				IEnumerable<RoutedUICommand> commands = EditorCommands.All.Select(pair => pair.Value).Concat(new[] { EditorCommands.NavigateBack, EditorCommands.NavigateForward });
				foreach (RoutedUICommand command in commands)
				{
					String key = EditorCommands.GetTextKey(command) ?? command.Name;
					String text = english.TryGetValue(key, out String? value) ? value : command.Name;
					foreach (KeyGesture gesture in command.InputGestures.OfType<KeyGesture>())
					{
						entries.Add(("Code editor", Normalize(gesture.Modifiers, gesture.Key), text));
					}
				}
			});

			// The key bindings declared in the markup of every window.
			XDocument main = SourceTree.Load(Path.Combine(SourceTree.AppFolder, "Views", "Shell", "MainWindow.xaml"));
			Dictionary<String, String> menuTexts = MenuTexts(main, english);
			foreach (String file in SourceTree.WindowFiles())
			{
				XDocument document = SourceTree.Load(file);
				String scope = ScopeOf(file);
				foreach (XElement binding in document.Descendants(_presentation + "KeyBinding"))
				{
					String command = (String?)binding.Attribute("Command") ?? String.Empty;
					String name = Regex.Match(command, @"(?:Binding\s+)?([A-Za-z0-9_.]+?)Command\b").Groups[1].Value;
					String text = menuTexts.TryGetValue(name, out String? known) ? known : Words(name.Split('.').Last());
					entries.Add((scope, Normalize(binding), text));
				}
			}

			return entries.Distinct().ToList();
		}

		/// <summary>Gets the name of the tables of the document that are generated, in order.</summary>
		public static IReadOnlyList<String> Scopes()
		{
			return Read().Select(entry => entry.Scope).Distinct(StringComparer.Ordinal).OrderBy(scope => scope == "Main window" ? 0 : scope == "Code editor" ? 1 : 2).ThenBy(scope => scope, StringComparer.Ordinal).ToList();
		}

		/// <summary>Writes the generated part of the document, followed by the part that is written by hand.</summary>
		public static String Render(String manualPart)
		{
			StringBuilder text = new StringBuilder();
			text.AppendLine("# Keyboard shortcuts");
			text.AppendLine();
			text.AppendLine("Fallout Script Studio is fully usable with the keyboard. The tables below list every shortcut that is bound in the application; they are generated from the real");
			text.AppendLine("bindings (`EditorCommands`, the `InputBindings` of the windows) and a test fails when a binding is added or removed without this file being updated.");
			text.AppendLine("To regenerate the tables run the tests with the environment variable `FSS_WRITE_SHORTCUTS=1`.");
			text.AppendLine();

			IReadOnlyList<(String Scope, String Gesture, String Description)> entries = Read();
			foreach (String scope in Scopes())
			{
				text.AppendLine("## " + scope);
				text.AppendLine();
				text.AppendLine("| Shortcut | Action |");
				text.AppendLine("| --- | --- |");
				foreach (IGrouping<String, (String Scope, String Gesture, String Description)> group in entries.Where(entry => entry.Scope == scope).GroupBy(entry => entry.Gesture, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal))
				{
					text.AppendLine("| `" + group.Key + "` | " + String.Join("; ", group.Select(entry => entry.Description).Distinct(StringComparer.Ordinal)) + " |");
				}

				text.AppendLine();
			}

			text.Append(manualPart);
			return text.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "\r\n", StringComparison.Ordinal).TrimEnd('\r', '\n');
		}

		/// <summary>Gets the pairs scope and gesture of the generated tables of a document.</summary>
		public static HashSet<String> Documented(String document)
		{
			HashSet<String> pairs = new(StringComparer.Ordinal);
			String? scope = null;
			foreach (String line in document.Split('\n').Select(l => l.TrimEnd('\r')))
			{
				if (line.StartsWith("## ", StringComparison.Ordinal))
				{
					scope = line[3..].Trim();
				}
				else if (scope is not null && scope != ManualSection && line.StartsWith("| `", StringComparison.Ordinal))
				{
					pairs.Add(scope + "|" + line[3..line.IndexOf('`', 3)]);
				}
			}

			return pairs;
		}

		/// <summary>Gets the pairs scope and gesture of the bindings.</summary>
		public static HashSet<String> Bound()
		{
			return Read().Select(entry => entry.Scope + "|" + entry.Gesture).ToHashSet(StringComparer.Ordinal);
		}

		/// <summary>Gets the name of the manual section of the document.</summary>
		public static String ManualSectionTitle => ManualSection;

		private static String ScopeOf(String file)
		{
			String name = Path.GetFileNameWithoutExtension(file);
			return name == "MainWindow" ? "Main window" : Words(name.EndsWith("Window", StringComparison.Ordinal) ? name[..^"Window".Length] : name) + " window";
		}

		/// <summary>Splits a name such as <c>NodeCodeEditor</c> into the words "Node code editor".</summary>
		private static String Words(String name)
		{
			String spaced = Regex.Replace(name, "(?<=[a-z0-9])(?=[A-Z])", " ");
			return spaced.Length == 0 ? spaced : spaced[0].ToString().ToUpperInvariant() + spaced[1..].ToLowerInvariant();
		}

		private static Dictionary<String, String> MenuTexts(XDocument window, Dictionary<String, String> english)
		{
			Dictionary<String, String> texts = new(StringComparer.Ordinal);
			foreach (XElement item in window.Descendants(_presentation + "MenuItem"))
			{
				Match command = Regex.Match((String?)item.Attribute("Command") ?? String.Empty, @"Binding\s+([A-Za-z0-9_.]+?)Command\b");
				Match header = Regex.Match((String?)item.Attribute("Header") ?? String.Empty, @"DynamicResource\s+(Str\.[A-Za-z0-9_.]+)");
				if (command.Success && header.Success && english.TryGetValue(header.Groups[1].Value, out String? text) && !texts.ContainsKey(command.Groups[1].Value))
				{
					texts[command.Groups[1].Value] = text.Replace("_", String.Empty, StringComparison.Ordinal);
				}
			}

			return texts;
		}

		/// <summary>Gets the normalized gesture of a key binding written with <c>Key</c> and <c>Modifiers</c> or with <c>Gesture</c>.</summary>
		public static String Normalize(XElement binding)
		{
			String? gesture = (String?)binding.Attribute("Gesture");
			if (gesture is not null)
			{
				String[] parts = gesture.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
				ModifierKeys modifiers = parts[..^1].Aggregate(ModifierKeys.None, (current, part) => current | ParseModifier(part));
				return Normalize(modifiers, ParseKey(parts[^1]));
			}

			ModifierKeys keys = ((String?)binding.Attribute("Modifiers") ?? String.Empty)
				.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
				.Aggregate(ModifierKeys.None, (current, part) => current | ParseModifier(part));
			return Normalize(keys, ParseKey((String?)binding.Attribute("Key") ?? String.Empty));
		}

		private static ModifierKeys ParseModifier(String text)
		{
			return text switch
			{
				"Ctrl" or "Control" => ModifierKeys.Control,
				"Alt" => ModifierKeys.Alt,
				"Shift" => ModifierKeys.Shift,
				"Windows" or "Win" => ModifierKeys.Windows,
				_ => ModifierKeys.None,
			};
		}

		private static Key ParseKey(String text)
		{
			return Enum.Parse<Key>(text, true);
		}

		private static String KeyText(Key key)
		{
			return key switch
			{
				Key.OemPlus or Key.Add => "+",
				Key.OemMinus or Key.Subtract => "-",
				Key.OemQuestion or Key.Divide => "/",
				Key.OemPeriod => ".",
				Key.Multiply => "*",
				Key.Cancel => "Break",
				Key.Return => "Enter",
				Key.Back => "Backspace",
				Key.Next => "PageDown",
				Key.Prior => "PageUp",
				>= Key.D0 and <= Key.D9 => ((Int32)(key - Key.D0)).ToString(System.Globalization.CultureInfo.InvariantCulture),
				_ => key.ToString(),
			};
		}

		private static String Normalize(ModifierKeys modifiers, Key key)
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

			parts.Add(KeyText(key));
			return String.Join("+", parts);
		}
	}
}