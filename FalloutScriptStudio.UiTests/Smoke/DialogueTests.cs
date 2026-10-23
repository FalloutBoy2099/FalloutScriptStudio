namespace FalloutScriptStudio.UiTests.Smoke
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Windows.Automation;
	using FalloutScriptStudio.TestSupport;
	using FalloutScriptStudio.UiTests.Support;
	using Xunit;

	/// <summary>The dialogue tools of the real program on a small dialogue with deliberate weak points: the quality check, the message check, the flowchart badges, the comparison of message files.</summary>
	[Collection(UiSmokeCollection.Name)]
	public sealed class DialogueTests
	{
		private readonly UiSmokeSuite _suite;

		public DialogueTests(UiSmokeSuite suite)
		{
			this._suite = suite;
		}

		private static String[] ErrorRows(AppSession app)
		{
			app.SelectTab("Errors");
			AutomationElement grid = Uia.WaitFor(app.MainWindow.Element, "Errors", ControlType.DataGrid);
			return grid.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.DataItem)).Cast<AutomationElement>().Select(item => item.Current.Name).ToArray();
		}

		[UiSmokeFact]
		public void The_dialogue_and_message_checks_list_their_findings_in_the_errors_tab()
		{
			using TestFolder work = new("ui-dialogue");
			DialogueMod mod = Fixtures.CreateDialogueMod(work.Combine("mod"));
			using AppSession app = AppSession.Start(this._suite, nameof(The_dialogue_and_message_checks_list_their_findings_in_the_errors_tab), new AppLaunch { Arguments = [mod.Script], SettingsJson = Fixtures.CreateSettingsFor(mod) });
			SmokeSteps steps = new(app);

			steps.Run("Check Dialogue reports the unreachable node, the dead end and the undefined target", () =>
			{
				app.WaitForParserFinished();
				app.InvokeMenu("Dialogue", "Check Dialogue");
				Uia.WaitUntil(() => ErrorRows(app).Any(row => row.Contains("DLG001", StringComparison.Ordinal)) && ErrorRows(app).Any(row => row.Contains("DLG002", StringComparison.Ordinal)), "the dialogue findings (rows: " + String.Join(" | ", ErrorRows(app).Select(Short)) + ")", TimeSpan.FromSeconds(30));
				String[] rows = ErrorRows(app);
				app.Step("rows: " + String.Join(" | ", rows.Select(Short)));
				Assert.Contains(rows, row => row.Contains("DLG001", StringComparison.Ordinal) && row.Contains("Node005", StringComparison.Ordinal));
				Assert.Contains(rows, row => row.Contains("DLG002", StringComparison.Ordinal) && row.Contains("Node004", StringComparison.Ordinal));
				Assert.Contains(rows, row => row.Contains("DLG003", StringComparison.Ordinal) && row.Contains("Node777", StringComparison.Ordinal));
				app.Shot(app.MainWindow, "dialogue-findings");
			});

			steps.Run("Check Messages reports the messages that the file lacks", () =>
			{
				app.InvokeMenu("Dialogue", "Check Messages");
				Uia.WaitUntil(() => ErrorRows(app).Any(row => row.Contains("140", StringComparison.Ordinal) && row.Contains("dlgtest", StringComparison.Ordinal)), "the missing message 140 (rows: " + String.Join(" | ", ErrorRows(app).Select(Short)) + ")", TimeSpan.FromSeconds(30));
				app.Shot(app.MainWindow, "message-findings");
			});

			steps.Finish();
			Assert.Equal(0, app.Close());
			app.AssertCleanLog();
		}

		[UiSmokeFact]
		public void The_flowchart_marks_the_nodes_with_issues_and_lists_them()
		{
			using TestFolder work = new("ui-dialogue");
			DialogueMod mod = Fixtures.CreateDialogueMod(work.Combine("mod"));
			using AppSession app = AppSession.Start(this._suite, nameof(The_flowchart_marks_the_nodes_with_issues_and_lists_them), new AppLaunch { Arguments = [mod.Script], SettingsJson = Fixtures.CreateSettingsFor(mod) });
			SmokeSteps steps = new(app);

			steps.Run("the Issues button counts the findings and opens the list", () =>
			{
				app.WaitForParserFinished();
				AppWindow flowchart = app.OpenNewWindow("Dialogue", "Dialogue Flowchart");
				AutomationElement issues = Uia.Wait(() => flowchart.Snapshot().Select(node => node.Element).FirstOrDefault(element => element.Current.AutomationId == "IssuesButton" && element.Current.Name.Contains('(', StringComparison.Ordinal) && !element.Current.Name.Contains("(0)", StringComparison.Ordinal)), "the Issues button with a count");
				app.Step("issues button: " + issues.Current.Name);
				Assert.Contains("(3)", issues.Current.Name, StringComparison.Ordinal);
				System.Threading.Thread.Sleep(600);
				app.Shot(flowchart, "flowchart-issues");
				Uia.Invoke(issues);
				System.Threading.Thread.Sleep(800);
				app.ShotPopups("flowchart-issues-list");
				app.Dump(flowchart, "flowchart-issues-open");
				String[] items = app.TopLevelElements()
					.SelectMany(element => Uia.Snapshot(element))
					.Where(node => node.Type == ControlType.ListItem && !node.IsOffscreen)
					.Select(node => node.Name)
					.ToArray();
				app.Step("issue items: " + String.Join(" | ", items));
				Assert.True(items.Length >= 3, "The list of issues has " + items.Length + " item(s).");
				Assert.Contains(items, item => item.Contains("Node777", StringComparison.Ordinal) || item.Contains("Node003", StringComparison.Ordinal));
				app.CloseWindow(flowchart);
			});

			steps.Finish();
			Assert.Equal(0, app.Close());
			app.AssertCleanLog();
		}

		[UiSmokeFact]
		public void The_comparison_of_message_files_adds_the_missing_lines_to_the_translation()
		{
			using TestFolder work = new("ui-dialogue");
			DialogueMod mod = Fixtures.CreateDialogueMod(work.Combine("mod"));
			using AppSession app = AppSession.Start(this._suite, nameof(The_comparison_of_message_files_adds_the_missing_lines_to_the_translation), new AppLaunch { Arguments = [mod.Script], SettingsJson = Fixtures.CreateSettingsFor(mod) });
			SmokeSteps steps = new(app);

			steps.Run("the window lists the lines the translation lacks", () =>
			{
				app.WaitForParserFinished();
				AppWindow compare = app.OpenNewWindow("Dialogue", "Compare Message Files");
				AutomationElement grid = compare.WaitFor(null, ControlType.DataGrid, "Table");
				Uia.WaitUntil(() => grid.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.DataItem)).Count >= 3, "the lines of the comparison");
				String[] rows = grid.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.DataItem)).Cast<AutomationElement>().Select(item => item.Current.Name).ToArray();
				app.Step("rows: " + String.Join(" | ", rows));
				Assert.Contains(rows, row => row.StartsWith("130,", StringComparison.Ordinal) && row.Contains("Missing", StringComparison.Ordinal));
				Assert.Contains(rows, row => row.StartsWith("500,", StringComparison.Ordinal));
				Assert.Contains(rows, row => row.StartsWith("102,", StringComparison.Ordinal) && row.Contains("Empty", StringComparison.Ordinal));
			});

			steps.Run("Add missing lines writes them to the French file after a confirmation", () =>
			{
				AppWindow compare = app.Windows().First(window => window.Title.Contains("Compare", StringComparison.OrdinalIgnoreCase));
				Uia.Invoke(compare.WaitFor("Add missing lines", ControlType.Button));
				AppWindow confirmation = Uia.Wait(() => app.Windows().FirstOrDefault(window => window.Handle != app.MainWindow.Handle && window.Handle != compare.Handle), "the confirmation of the program");
				app.Shot(confirmation, "compare-confirmation");
				app.Step("confirmation buttons: " + String.Join(" | ", confirmation.Snapshot().Where(node => node.Type == ControlType.Button).Select(node => node.Name)));
				AutomationElement yes = confirmation.Snapshot().Where(node => node.Type == ControlType.Button && node.Name is not "Cancel" and not "No" and not "Close" and not "").Select(node => node.Element).First();
				Uia.Invoke(yes);
				Uia.WaitUntil(() => File.ReadAllText(mod.FrenchMessages).Contains("{500}", StringComparison.Ordinal), "the missing lines to be written to " + mod.FrenchMessages);
				String text = File.ReadAllText(mod.FrenchMessages);
				Assert.Contains("{130}", text, StringComparison.Ordinal);
				app.Step("french file now: " + text.Replace('\r', ' ').Replace('\n', '|'));
				app.Shot(compare, "compare-after-add");
				app.CloseWindow(compare);
			});

			steps.Finish();
			Assert.Equal(0, app.Close());
			app.AssertCleanLog();
		}

		private static String Short(String row)
		{
			return row.Substring(Math.Max(0, row.LastIndexOf('\\') + 1));
		}
	}
}