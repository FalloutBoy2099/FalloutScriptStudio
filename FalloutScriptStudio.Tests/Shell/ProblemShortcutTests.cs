namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Xml.Linq;
	using Xunit;

	/// <summary>The shortcuts of the problem list and of Cancel are bound, do not collide with another binding and are documented.</summary>
	public sealed class ProblemShortcutTests
	{
		private static readonly XNamespace _presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

		private static String Root()
		{
			String? directory = AppContext.BaseDirectory;
			while (directory is not null && !File.Exists(Path.Combine(directory, "FalloutScriptStudio.sln")))
			{
				directory = Path.GetDirectoryName(directory);
			}

			Assert.NotNull(directory);
			return directory;
		}

		private static List<XElement> KeyBindings()
		{
			XDocument window = XDocument.Load(Path.Combine(Root(), "FalloutScriptStudio", "Views", "Shell", "MainWindow.xaml"));
			return window.Descendants(_presentation + "KeyBinding").ToList();
		}

		private static String Gesture(XElement binding)
		{
			return ((String?)binding.Attribute("Modifiers") ?? String.Empty) + "+" + (String?)binding.Attribute("Key");
		}

		[Fact]
		public void Next_and_previous_problem_and_cancel_are_bound_to_their_commands()
		{
			List<XElement> bindings = KeyBindings();

			Assert.Contains(bindings, binding => Gesture(binding) == "Ctrl+F8" && (String?)binding.Attribute("Command") == "{Binding Output.NextProblemCommand}");
			Assert.Contains(bindings, binding => Gesture(binding) == "Ctrl+Shift+F8" && (String?)binding.Attribute("Command") == "{Binding Output.PreviousProblemCommand}");
			Assert.Contains(bindings, binding => Gesture(binding) == "Ctrl+Cancel" && (String?)binding.Attribute("Command") == "{Binding CancelOperationCommand}");
		}

		[Fact]
		public void No_two_key_bindings_of_the_main_window_use_the_same_gesture()
		{
			List<String> duplicates = KeyBindings().Select(Gesture).GroupBy(gesture => gesture).Where(group => group.Count() > 1).Select(group => group.Key).ToList();

			Assert.True(duplicates.Count == 0, "Gestures bound twice: " + String.Join(", ", duplicates));
		}

		[Fact]
		public void The_shortcuts_are_documented()
		{
			String architecture = File.ReadAllText(Path.Combine(Root(), "docs", "ARCHITECTURE.md"));

			Assert.Contains("Ctrl+F8", architecture, StringComparison.Ordinal);
			Assert.Contains("Ctrl+Shift+F8", architecture, StringComparison.Ordinal);
			Assert.Contains("Ctrl+Break", architecture, StringComparison.Ordinal);
		}
	}
}
