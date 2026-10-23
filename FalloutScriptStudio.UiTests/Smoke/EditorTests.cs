namespace FalloutScriptStudio.UiTests.Smoke
{
	using System;
	using System.Linq;
	using System.Text.RegularExpressions;
	using System.Windows.Automation;
	using FalloutScriptStudio.TestSupport;
	using FalloutScriptStudio.UiTests.Support;
	using Xunit;

	/// <summary>The code editor of the real program: the live diagnostics (squiggles and problems), a snippet, the quick fix.</summary>
	[Collection(UiSmokeCollection.Name)]
	public sealed class EditorTests
	{
		private readonly UiSmokeSuite _suite;

		public EditorTests(UiSmokeSuite suite)
		{
			this._suite = suite;
		}

		[UiSmokeFact]
		public void Live_diagnostics_underline_the_mistakes_and_list_them_as_problems()
		{
			using TestFolder work = new("ui-editor");
			String script = Fixtures.CreateScriptWithMistakes(work.Combine("m"));
			using AppSession app = AppSession.Start(this._suite, nameof(Live_diagnostics_underline_the_mistakes_and_list_them_as_problems), new AppLaunch { Arguments = [script] });
			SmokeSteps steps = new(app);

			steps.Run("the problems of the script are counted in the status bar", () =>
			{
				app.WaitForParserFinished();
				Uia.WaitUntil(() => ReadProblemCounts(app) is { Errors: >= 2, Warnings: >= 1 }, "the errors and the warning of the script (counts: " + ReadProblemCounts(app) + ")", TimeSpan.FromSeconds(30));
			});

			steps.Run("the Errors tab lists them with the file and the line", () =>
			{
				app.SelectTab("Errors");
				AutomationElement grid = Uia.WaitFor(app.MainWindow.Element, "Errors", ControlType.DataGrid);
				Uia.WaitUntil(() => grid.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.DataItem)).Cast<AutomationElement>().Any(item => item.Current.Name.Contains("unknown_function_name", StringComparison.Ordinal)), "the unknown name in the Errors tab");
				String[] rows = grid.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.DataItem)).Cast<AutomationElement>().Select(item => item.Current.Name).ToArray();
				app.Step("problems: " + String.Join(" | ", rows.Select(row => row.Substring(Math.Max(0, row.LastIndexOf('\\') + 1)))));
				Assert.Contains(rows, row => row.Contains("mistakes.ssl", StringComparison.Ordinal) && row.Contains("display_msg", StringComparison.Ordinal));
				UiChecks.AssertReadableNames(app, app.MainWindow);
			});

			steps.Run("the mistakes are underlined in the editor (red for an error, orange for a warning)", () =>
			{
				WindowImage image = app.Shot(app.MainWindow, "diagnostics-light");
				System.Windows.Rect editor = app.MainWindow.Element.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "PrimaryEditor")).Current.BoundingRectangle;
				System.Windows.Rect window = app.MainWindow.Bounds;
				Int32 left = (Int32)(editor.X - window.X);
				Int32 top = (Int32)(editor.Y - window.Y);
				Int32 red = image.Count(left, top, (Int32)editor.Width, (Int32)editor.Height, (r, g, b) => r >= 250 && g <= 90 && b <= 90);
				Int32 orange = image.Count(left, top, (Int32)editor.Width, (Int32)editor.Height, (r, g, b) => r >= 250 && g is >= 120 and <= 180 && b <= 70);
				app.Step("squiggle pixels: red " + red + ", orange " + orange);
				Assert.True(red >= 150, "No red squiggle under the errors: " + red + " red pixels in the editor.");
				Assert.True(orange >= 40, "No orange squiggle under the warning: " + orange + " orange pixels in the editor.");
			});

			steps.Finish();
			Assert.Equal(0, app.Close());
			app.AssertCleanLog();
		}

		[UiSmokeFact]
		public void The_quick_fix_runs_from_the_command_palette()
		{
			using TestFolder work = new("ui-editor");
			String script = Fixtures.CreateScriptWithMissingDeclaration(work.Combine("q"));
			using AppSession app = AppSession.Start(this._suite, nameof(The_quick_fix_runs_from_the_command_palette), new AppLaunch { Arguments = [script] });
			SmokeSteps steps = new(app);

			steps.Run("the call of a procedure that is defined later is reported", () =>
			{
				app.WaitForParserFinished();
				Uia.WaitUntil(() => ReadProblemCounts(app) is { Errors: >= 1 }, "the error of the script (counts: " + ReadProblemCounts(app) + ")", TimeSpan.FromSeconds(30));
			});

			steps.Run("Go to line puts the caret on the call", () =>
			{
				AppWindow window = app.OpenNewWindow("Edit", "Go to Line");
				Uia.SetValue(window.WaitFor(null, ControlType.Edit, "LineBox"), "5");
				Uia.Invoke(window.WaitFor("Go", ControlType.Button));
				app.WaitForClosed(window);
				Uia.WaitUntil(() => app.MainWindow.Texts().Contains("Ln 5"), "the caret on line 5");

				// The fix is offered where the caret is inside the name: "<tab>call helper_proc;" has the name from the seventh column.
				for (Int32 key = 0; key < 8; key++)
				{
					app.PressInEditor("Right");
				}
			});

			steps.Run("Quick Fix declares the procedure before its use", () =>
			{
				AppWindow palette = app.OpenNewWindow("View", "Command Palette");
				Uia.SetValue(palette.WaitFor(null, ControlType.Edit, "FilterBox"), "Quick Fix");
				AutomationElement list = palette.WaitFor(null, ControlType.List, "CommandList");
				Uia.WaitUntil(() => list.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem)).Cast<AutomationElement>().Any(item => item.Current.Name.StartsWith("Quick Fix", StringComparison.Ordinal)), "the Quick Fix command");
				AutomationElement item = list.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem)).Cast<AutomationElement>().First(candidate => candidate.Current.Name.StartsWith("Quick Fix", StringComparison.Ordinal));
				Uia.Select(item);
				AutomationElement run = palette.WaitFor("Run", ControlType.Button);
				Uia.WaitUntil(() => run.Current.IsEnabled, "the Run button of the palette to be enabled (palette: " + String.Join(" | ", palette.Texts().Where(text => text.Length < 120).Take(12)) + ")", TimeSpan.FromSeconds(5));
				Uia.Invoke(run);
				app.WaitForClosed(palette);
				Uia.WaitUntil(() => ReadEditorText(app).Contains("procedure helper_proc;", StringComparison.Ordinal), "the forward declaration to be inserted (text: " + ReadEditorText(app).Replace('\r', ' ').Replace('\n', ' ') + ")");
				Uia.WaitUntil(() => ReadProblemCounts(app) is { Errors: 0 }, "the error to go (counts: " + ReadProblemCounts(app) + ")", TimeSpan.FromSeconds(30));
				app.Shot(app.MainWindow, "quick-fix-done");
			});

			steps.Finish();
			Assert.Equal(0, app.Close());
			app.AssertCleanLog();
		}

		[UiSmokeFact]
		public void A_snippet_is_inserted_from_the_picker_and_expands_with_Tab()
		{
			using TestFolder work = new("ui-editor");
			String script = Fixtures.CreateEditableScript(work.Combine("e"));
			// The list of the code completion is switched off: opened off-screen, its window keeps the program busy in layout for seconds, and the
			// Tab key would commit its selected entry instead of expanding the trigger.
			using AppSession app = AppSession.Start(this._suite, nameof(A_snippet_is_inserted_from_the_picker_and_expands_with_Tab), new AppLaunch { Arguments = [script], SettingsJson = "{\"isFirstRunCompleted\": true, \"editor\": {\"autocomplete\": false}}" });
			SmokeSteps steps = new(app);

			steps.Run("the snippet of the picker is inserted at the caret", () =>
			{
				app.WaitForParserFinished();
				AppWindow goTo = app.OpenNewWindow("Edit", "Go to Line");
				Uia.SetValue(goTo.WaitFor(null, ControlType.Edit, "LineBox"), "5");
				Uia.Invoke(goTo.WaitFor("Go", ControlType.Button));
				app.WaitForClosed(goTo);
				AppWindow picker = app.OpenNewWindow("Code", "Insert Snippet");
				AutomationElement list = picker.WaitFor(null, ControlType.List, "SnippetList");
				AutomationElement dispmsg = list.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem)).Cast<AutomationElement>().First(item => item.Current.Name.StartsWith("dispmsg", StringComparison.Ordinal));
				Uia.Select(dispmsg);
				Uia.Invoke(picker.WaitFor("Insert", ControlType.Button));
				app.WaitForClosed(picker);
				Uia.WaitUntil(() => ReadEditorText(app).Contains("display_msg(\"message\");", StringComparison.Ordinal), "the snippet to be inserted (text: " + ReadEditorText(app).Replace('\r', ' ').Replace('\n', ' ') + ")");
				app.Shot(app.MainWindow, "snippet-inserted");
			});

			steps.Run("a trigger typed in the editor expands with Tab", () =>
			{
				app.PressInEditor("Escape");
				app.PressInEditor("End");
				app.PressInEditor("Return");
				app.TypeInEditor("callp");
				app.PressInEditor("Tab");
				Uia.WaitUntil(() => ReadEditorText(app).Contains("call procedure_name(", StringComparison.Ordinal), "the snippet to expand (text: " + ReadEditorText(app).Replace('\r', ' ').Replace('\n', ' ') + ")");
				app.Shot(app.MainWindow, "snippet-expanded");
			});

			steps.Finish();
			Assert.Equal(0, app.Close());
			app.AssertCleanLog();
		}

		private static String ReadEditorText(AppSession app)
		{
			AutomationElement editor = Uia.WaitFor(app.MainWindow.Element, null, ControlType.Document, "PrimaryEditor");
			return ((TextPattern)editor.GetCurrentPattern(TextPattern.Pattern)).DocumentRange.GetText(-1);
		}

		/// <summary>Reads the counts of the status bar button <c>N error(s), M warning(s). Click to show the problem list.</c>.</summary>
		private static (Int32 Errors, Int32 Warnings) ReadProblemCounts(AppSession app)
		{
			foreach (String text in app.MainWindow.Texts())
			{
				Match match = Regex.Match(text, @"^(\d+) error\(s\), (\d+) warning\(s\)");
				if (match.Success)
				{
					return (Int32.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture), Int32.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture));
				}
			}

			return (-1, -1);
		}
	}
}