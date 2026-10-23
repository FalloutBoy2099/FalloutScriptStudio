namespace FalloutScriptStudio.UiTests.Support
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text.RegularExpressions;
	using Xunit;

	/// <summary>Checks on the UI Automation tree of a window that apply to every window: names a screen reader could read, texts, size.</summary>
	internal static class UiChecks
	{
		private static readonly Regex GarbageName = new(@"^\s*\w+\s*\{.*=.*\}\s*$|^(?:FalloutScriptStudio|System|ICSharpCode|Wpf|MS\.Internal)\.[\w.+`]+$|^[A-Z][a-z]+(?:[A-Z][a-z]+)*\d{2}$", RegexOptions.Compiled);

		/// <summary>Fails when an element has a name that is the text of an object (a record, a type name) instead of something a person can read.</summary>
		/// <param name="app">The program.</param>
		/// <param name="window">The window.</param>
		public static void AssertReadableNames(AppSession app, AppWindow window)
		{
			List<String> garbage = window.Snapshot().Where(node => GarbageName.IsMatch(node.Name)).Select(node => node.ToString()).Distinct().ToList();
			foreach (String text in garbage)
			{
				app.Step("UNREADABLE NAME in '" + window.Title + "': " + text);
			}

			Assert.True(garbage.Count == 0, "Elements of '" + window.Title + "' have a name that is not text for a person:" + Environment.NewLine + String.Join(Environment.NewLine, garbage.Take(8)));
		}

		/// <summary>Fails when a window shows (almost) no text: an empty or garbled window.</summary>
		/// <param name="window">The window.</param>
		/// <param name="minimum">The least number of texts.</param>
		public static void AssertHasTexts(AppWindow window, Int32 minimum = 3)
		{
			IReadOnlyList<String> texts = window.Texts();
			Assert.True(texts.Count >= minimum, "The window '" + window.Title + "' shows only " + texts.Count + " text(s): " + String.Join(" | ", texts));
		}

		/// <summary>Fails when the window is smaller than a minimum in device pixels.</summary>
		/// <param name="window">The window.</param>
		/// <param name="width">The least width.</param>
		/// <param name="height">The least height.</param>
		public static void AssertMinimumSize(AppWindow window, Int32 width, Int32 height)
		{
			System.Windows.Rect bounds = window.Bounds;
			Assert.True(bounds.Width >= width && bounds.Height >= height, "The window '" + window.Title + "' is only " + (Int32)bounds.Width + "x" + (Int32)bounds.Height + " pixels.");
		}
	}
}