namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Windows.Input;
	using System.Xml.Linq;
	using FalloutScriptStudio.Editor;
	using FalloutScriptStudio.Tests.Editor.Support;
	using Xunit;

	/// <summary>
	/// The shortcuts shown in the menus of the main window (<c>InputGestureText</c>) must really be bound, and the shortcuts of the
	/// menu of the legacy editor must exist. The window markup is read from the source tree.
	/// </summary>
	public sealed class MainWindowGestureTests
	{
		private static readonly XNamespace _presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

		/// <summary>The shortcuts of the legacy menu (<c>ShortcutKeys</c> in <c>TextEditor.Designer.cs</c>) and the clipboard keys.</summary>
		private static readonly String[] _legacyShortcuts =
		{
			"Ctrl+N", "Ctrl+O", "Ctrl+S", "Ctrl+Shift+S", "Ctrl+F", "F3", "Shift+F3", "Ctrl+F3", "Ctrl+G", "Alt+S", "Ctrl+Shift+W",
			"Alt+M", "Alt+Space", "F7", "Alt+Shift+G", "F8", "F4", "F5", "Alt+R", "Alt+L", "Alt+Q", "Alt+E", "Alt+P", "Ctrl+P",
			"Ctrl+F12", "F12", "Alt+F12", "Alt+G", "Ctrl+Shift+A", "Ctrl+Shift+Z", "Ctrl+X", "Ctrl+C", "Ctrl+V", "Alt+C", "Alt+X",
			"Alt+A", "Alt+F", "Ctrl+B", "Ctrl+Shift+D", "Alt+Back",
		};

		/// <summary>Shortcuts that are handled by the system or by the text control itself.</summary>
		private static readonly String[] _builtIn = { "Ctrl+Z", "Ctrl+Y", "Ctrl+X", "Ctrl+C", "Ctrl+V", "Ctrl+A", "Alt+F4", "Alt+Back" };

		private static XDocument LoadWindow()
		{
			String? directory = AppContext.BaseDirectory;
			while (directory is not null && !File.Exists(Path.Combine(directory, "FalloutScriptStudio.sln")))
			{
				directory = Path.GetDirectoryName(directory);
			}

			Assert.NotNull(directory);
			return XDocument.Load(Path.Combine(directory, "FalloutScriptStudio", "Views", "Shell", "MainWindow.xaml"));
		}

		private static String KeyText(Key key)
		{
			return key switch
			{
				Key.OemPlus or Key.Add => "+",
				Key.OemMinus or Key.Subtract => "-",
				Key.OemQuestion or Key.Divide => "/",
				Key.Multiply => "*",
				>= Key.D0 and <= Key.D9 => ((Int32)(key - Key.D0)).ToString(System.Globalization.CultureInfo.InvariantCulture),
				_ => key.ToString(),
			};
		}

		private static String Normalize(ModifierKeys modifiers, Key key)
		{
			List<String> parts = new List<String>();
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

		private static HashSet<String> CollectBoundGestures(XDocument window)
		{
			HashSet<String> bound = new HashSet<String>(_builtIn, StringComparer.Ordinal);
			foreach (XElement binding in window.Descendants(_presentation + "KeyBinding"))
			{
				Key key = Enum.Parse<Key>((String)binding.Attribute("Key")!);
				ModifierKeys modifiers = ModifierKeys.None;
				String? text = (String?)binding.Attribute("Modifiers");
				if (text is not null)
				{
					foreach (String part in text.Split('+'))
					{
						modifiers |= part switch
						{
							"Ctrl" => ModifierKeys.Control,
							"Alt" => ModifierKeys.Alt,
							"Shift" => ModifierKeys.Shift,
							_ => ModifierKeys.None,
						};
					}
				}

				bound.Add(Normalize(modifiers, key));
			}

			return bound;
		}

		private static HashSet<String> CollectEditorGestures()
		{
			return UiThread.Run(() =>
			{
				HashSet<String> gestures = new HashSet<String>(StringComparer.Ordinal);
				IEnumerable<System.Windows.Input.RoutedUICommand> commands = EditorCommands.All.Select(pair => pair.Value)
					.Concat(new[] { EditorCommands.NavigateBack, EditorCommands.NavigateForward });
				foreach (System.Windows.Input.RoutedUICommand command in commands)
				{
					foreach (KeyGesture gesture in command.InputGestures.OfType<KeyGesture>())
					{
						gestures.Add(Normalize(gesture.Modifiers, gesture.Key));
					}
				}

				return gestures;
			});
		}

		[Fact]
		public void EveryShortcutShownInAMenuIsBound()
		{
			XDocument window = LoadWindow();
			HashSet<String> bound = CollectBoundGestures(window);
			bound.UnionWith(CollectEditorGestures());

			List<String> unbound = new List<String>();
			foreach (XElement item in window.Descendants(_presentation + "MenuItem"))
			{
				String? shown = (String?)item.Attribute("InputGestureText");
				if (shown is null)
				{
					continue;
				}

				foreach (String gesture in shown.Split(" / ", StringSplitOptions.TrimEntries))
				{
					if (!bound.Contains(gesture))
					{
						unbound.Add((String?)item.Attribute("Header") + " " + gesture);
					}
				}
			}

			Assert.True(unbound.Count == 0, "Shortcuts shown in the menu but not bound: " + String.Join("; ", unbound));
		}

		[Fact]
		public void EveryShortcutOfTheLegacyMenuExists()
		{
			XDocument window = LoadWindow();
			HashSet<String> available = CollectBoundGestures(window);
			available.UnionWith(CollectEditorGestures());

			String[] missing = _legacyShortcuts.Where(shortcut => !available.Contains(shortcut)).ToArray();

			Assert.True(missing.Length == 0, "Legacy shortcuts without a binding: " + String.Join(", ", missing));
		}

		[Fact]
		public void TheUpperAndLowerCaseItemsShowTheRealGestures()
		{
			XDocument window = LoadWindow();

			String? upper = GestureOfItemWithCommandParameter(window, "UpperCase");
			String? lower = GestureOfItemWithCommandParameter(window, "LowerCase");
			String? capitalize = GestureOfItemWithCommandParameter(window, "CapitalizeWord");

			Assert.Equal("Ctrl+Shift+A", upper);
			Assert.Equal("Ctrl+Shift+Z", lower);
			Assert.Equal("Ctrl+Shift+W", capitalize);
		}

		[Fact]
		public void RightBrowserAndLogWindowHaveKeyBindings()
		{
			XDocument window = LoadWindow();

			Assert.Contains(window.Descendants(_presentation + "KeyBinding"), binding =>
				(String?)binding.Attribute("Key") == "R" && (String?)binding.Attribute("Modifiers") == "Alt" && ((String?)binding.Attribute("Command"))!.Contains("ToggleRightBrowser", StringComparison.Ordinal));
			Assert.Contains(window.Descendants(_presentation + "KeyBinding"), binding =>
				(String?)binding.Attribute("Key") == "L" && (String?)binding.Attribute("Modifiers") == "Alt" && ((String?)binding.Attribute("Command"))!.Contains("ToggleLog", StringComparison.Ordinal));
		}

		[Fact]
		public void TheAutoFormattingCodeOptionIsInTheCodeMenu()
		{
			XDocument window = LoadWindow();

			XElement item = window.Descendants(_presentation + "MenuItem")
				.Single(element => ((String?)element.Attribute("IsChecked"))?.Contains("FormatCodeOnOpen", StringComparison.Ordinal) == true);

			Assert.Equal("True", (String?)item.Attribute("IsCheckable"));
			Assert.Equal("Str.Shell.Menu.Code", ExtractKey(item.Parent!.Attribute("Header")!.Value));
		}

		private static String ExtractKey(String resource)
		{
			return resource.Replace("{DynamicResource ", String.Empty, StringComparison.Ordinal).TrimEnd('}');
		}

		private static String? GestureOfItemWithCommandParameter(XDocument window, String command)
		{
			return window.Descendants(_presentation + "MenuItem")
				.Where(item => ((String?)item.Attribute("CommandParameter"))?.EndsWith("EditorCommand." + command + "}", StringComparison.Ordinal) == true)
				.Select(item => (String?)item.Attribute("InputGestureText"))
				.FirstOrDefault();
		}
	}
}
