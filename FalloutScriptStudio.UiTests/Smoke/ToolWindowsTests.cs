namespace FalloutScriptStudio.UiTests.Smoke
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text.RegularExpressions;
	using System.Windows.Automation;
	using FalloutScriptStudio.TestSupport;
	using FalloutScriptStudio.UiTests.Support;
	using Xunit;

	/// <summary>Every tool window of the real program: it opens from its menu, shows its content, is not blank or clipped, is readable for a screen reader, does what it is for, and closes.</summary>
	[Collection(UiSmokeCollection.Name)]
	public sealed class ToolWindowsTests
	{
		private readonly UiSmokeSuite _suite;

		public ToolWindowsTests(UiSmokeSuite suite)
		{
			this._suite = suite;
		}

		[UiSmokeFact]
		public void Every_tool_window_opens_renders_works_and_closes()
		{
			this.Sweep(nameof(Every_tool_window_opens_renders_works_and_closes), "en", "light", exercise: true);
		}

		[UiSmokeTheory]
		[InlineData("fr", "dark")]
		[InlineData("de", "light")]
		[InlineData("ru", "dark")]
		public void The_tool_windows_fit_their_texts_in_other_languages(String language, String theme)
		{
			this.Sweep(nameof(The_tool_windows_fit_their_texts_in_other_languages) + "-" + language + "-" + theme, language, theme, exercise: false);
		}

		private static IReadOnlyList<AutomationElement> ListItems(AutomationElement list)
		{
			return list.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem)).Cast<AutomationElement>().ToList();
		}

		private static Int32 ReadCount(AppWindow window, String pattern)
		{
			foreach (String text in window.Texts())
			{
				Match match = Regex.Match(text, pattern);
				if (match.Success)
				{
					return Int32.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
				}
			}

			return -1;
		}

		/// <summary>The windows with the longest texts, the ones that can overflow in a language with long words: the only ones the sweeps of the other languages open.</summary>
		private static readonly HashSet<String> LongTextWindows = new(StringComparer.Ordinal) { "settings", "project-settings", "about", "batch-compile", "search", "snippet-manager", "format-project", "compare-messages", "flowchart", "dialogue-preview" };

		private void Sweep(String test, String language, String theme, Boolean exercise)
		{
			using TestFolder work = new("ui-windows");
			DialogueMod mod = Fixtures.CreateDialogueMod(work.Combine("mod"));
			using AppSession app = AppSession.Start(this._suite, test, new AppLaunch { Arguments = [mod.Script], SettingsJson = Fixtures.CreateSettingsFor(mod), Language = language, Theme = theme });
			app.WaitForParserFinished();
			SmokeSteps steps = new(app);
			ToolContext context = new(app, mod);

			(String Name, String[] Path, String Title, Func<ToolContext, AppWindow, Boolean>? Exercise)[] windows =
			[
				("settings", ["Options", "Settings"], "Settings", ExerciseSettings),
				("project-settings", ["Options", "Project Settings"], "Project settings", null),
				("about", ["Help", "About Fallout Script Studio"], "About", null),
				("go-to-line", ["Edit", "Go to Line"], "Go to line", ExerciseGoToLine),
				("go-to-symbol", ["Edit", "Go to Symbol"], "Go to Symbol", ExerciseGoToSymbol),
				("headers", ["Headers", "Browse Headers"], "", null),
				("register-script", ["Script", "Register Current Script"], "", null),
				("batch-compile", ["Compile", "Mass Compile"], "Batch compilation", null),
				("search", ["Edit", "Find in Files"], "Search", null),
				("command-palette", ["View", "Command Palette"], "Command Palette", ExerciseCommandPalette),
				("snippet-picker", ["Code", "Insert Snippet"], "Insert Snippet", null),
				("snippet-manager", ["Code", "Manage Snippets"], "Snippets", null),
				("format-project", ["Code", "Format Project"], "Format Project", ExerciseFormatProject),
				("compare-messages", ["Dialogue", "Compare Message Files"], "Compare message files", null),
				("message-editor", ["Dialogue", "Message Editor"], "Message Editor", null),
				("flowchart", ["Dialogue", "Dialogue Flowchart"], "Flowchart", null),
				("dialogue-preview", ["Dialogue", "Preview and Testing"], "", null),
				("dialogue-rules", ["Dialogue", "Dialogue Functions"], "Dialogue functions", null),
			];

			foreach ((String name, String[] path, String title, Func<ToolContext, AppWindow, Boolean>? exerciseWindow) in windows)
			{
				if (!exercise && !LongTextWindows.Contains(name))
				{
					continue;
				}

				steps.Run(name, () =>
				{
					AppWindow window = app.OpenNewWindow(path);
					Assert.NotEmpty(window.Title);
					if (language == "en")
					{
						Assert.Contains(Uia.Normalize(title), Uia.Normalize(window.Title), StringComparison.Ordinal);
					}

					System.Threading.Thread.Sleep(400);
					app.Shot(window, name);
					app.Dump(window, name);
					UiChecks.AssertHasTexts(window);
					UiChecks.AssertReadableNames(app, window);
					app.AssertNothingClipped(window);
					if (exercise && exerciseWindow is not null && exerciseWindow(context, window))
					{
						return;
					}

					app.CloseWindow(window);
				});
			}

			steps.Finish();
			Assert.Equal(0, app.Close());
			app.AssertCleanLog();
		}

		private static Boolean ExerciseSettings(ToolContext context, AppWindow window)
		{
			AutomationElement pages = window.WaitFor(id: "PagesList");
			IReadOnlyList<AutomationElement> items = ListItems(pages);
			Assert.True(items.Count >= 7, "The settings window lists only " + items.Count + " page(s).");
			Int32 index = 0;
			foreach (AutomationElement item in items)
			{
				String name = item.Current.Name;
				Uia.Select(item);
				Uia.WaitUntil(() => Uia.IsSelected(item), "the settings page " + name + " to be selected");
				System.Threading.Thread.Sleep(500);
				context.App.Shot(window, "settings-page-" + index++ + "-" + name.Replace(' ', '-').ToLowerInvariant());
				context.App.AssertNothingClipped(window);
				UiChecks.AssertReadableNames(context.App, window);
				if (index == 2)
				{
					// The formatting options with the live preview of the sample script are in the lower half of the page of the editor.
					Uia.ScrollTo(window.Element, 48);
					System.Threading.Thread.Sleep(500);
					context.App.Shot(window, "settings-formatting-preview");
				}
			}

			return false;
		}

		private static Boolean ExerciseGoToLine(ToolContext context, AppWindow window)
		{
			Uia.SetValue(window.WaitFor(id: "LineBox"), "30");
			Uia.Invoke(window.WaitFor(LocalizedStrings.Translate("Go", context.App.Launch.Language), ControlType.Button));
			context.App.WaitForClosed(window);
			Uia.WaitUntil(() => context.App.MainWindow.Texts().Contains("Ln 30"), "the status bar to show line 30");
			return true;
		}

		private static Boolean ExerciseGoToSymbol(ToolContext context, AppWindow window)
		{
			AutomationElement list = window.WaitFor(id: "SymbolList");
			Int32 all = ListItems(list).Count;
			Uia.SetValue(window.WaitFor(id: "FilterBox"), "Node00");
			Uia.WaitUntil(() => ListItems(list) is { Count: > 0 } items && items.Count < all && items.All(item => item.Current.Name.Contains("Node00", StringComparison.Ordinal)), "the filter to narrow the symbols");
			AutomationElement third = ListItems(list).First(item => item.Current.Name.Contains("Node003", StringComparison.Ordinal));
			String line = Regex.Match(third.Current.Name, @"(\d+)\s*$").Groups[1].Value;
			Uia.Select(third);
			Uia.WaitUntil(() => Uia.IsSelected(third), "Node003 to be selected");
			Uia.Invoke(window.WaitFor(LocalizedStrings.Translate("Open", context.App.Launch.Language), ControlType.Button));
			context.App.WaitForClosed(window);
			Uia.WaitUntil(() => context.App.MainWindow.Texts().Contains("Ln " + line), "the status bar to show line " + line);
			return true;
		}

		private static Boolean ExerciseCommandPalette(ToolContext context, AppWindow window)
		{
			Int32 before = ReadCount(window, @"^(\d+)\s");
			Assert.True(before > 100, "The command palette lists only " + before + " command(s).");
			Uia.SetValue(window.WaitFor(id: "FilterBox"), LocalizedStrings.Translate("Snippet", context.App.Launch.Language));
			Uia.WaitUntil(() => ReadCount(window, @"^(\d+)\s") is > 0 and var count && count < before, "the filter to narrow the commands");
			return false;
		}

		private static Boolean ExerciseFormatProject(ToolContext context, AppWindow window)
		{
			AppSession app = context.App;
			AutomationElement scope = window.WaitFor(LocalizedStrings.Translate("Project folder", app.Launch.Language), ControlType.RadioButton);
			Uia.Select(scope);
			Uia.Invoke(window.WaitFor(LocalizedStrings.Translate("Preview changes", app.Launch.Language), ControlType.Button));
			AutomationElement files = window.WaitFor(id: "FileList");
			Uia.WaitUntil(() => ListItems(files).Count >= 1, "the files that would change");
			Assert.Contains(ListItems(files), item => item.Current.Name.Contains("ugly.ssl", StringComparison.OrdinalIgnoreCase));
			app.Shot(window, "format-project-preview");
			String before = File.ReadAllText(context.Mod.UglyScript);
			Uia.Invoke(window.WaitFor(LocalizedStrings.Translate("Format the selected files", app.Launch.Language), ControlType.Button));
			Uia.WaitUntil(() => File.ReadAllText(context.Mod.UglyScript) != before, "the formatted file to be written");
			Assert.Contains("a := 1 + 2;", File.ReadAllText(context.Mod.UglyScript), StringComparison.Ordinal);
			app.Shot(window, "format-project-done");
			return false;
		}

		private sealed record ToolContext(AppSession App, DialogueMod Mod);
	}
}