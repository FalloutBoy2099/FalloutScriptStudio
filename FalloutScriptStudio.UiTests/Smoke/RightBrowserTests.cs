namespace FalloutScriptStudio.UiTests.Smoke
{
	using System;
	using System.Linq;
	using System.Windows.Automation;
	using FalloutScriptStudio.TestSupport;
	using FalloutScriptStudio.UiTests.Support;
	using Xunit;

	/// <summary>The Globals and Help tabs of the right browser on a real project: the lists fill, the filters narrow them, a group opens.</summary>
	[Collection(UiSmokeCollection.Name)]
	public sealed class RightBrowserTests
	{
		private readonly UiSmokeSuite _suite;

		public RightBrowserTests(UiSmokeSuite suite)
		{
			this._suite = suite;
		}

		private static Int32 Count(AutomationElement root, ControlType type)
		{
			return root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, type)).Count;
		}

		[UiSmokeFact]
		public void The_globals_and_help_tabs_list_filter_and_expand()
		{
			using TestFolder work = new("ui-right");
			String source = Fixtures.CopySampleMod(work.Path);
			String script = Fixtures.SampleScript(source);
			using AppSession app = AppSession.Start(this._suite, nameof(The_globals_and_help_tabs_list_filter_and_expand), new AppLaunch { Arguments = [script], SettingsJson = Fixtures.CreateSettingsForFolder(source) });
			SmokeSteps steps = new(app);

			steps.Run("the Globals tab lists the variables of the project", () =>
			{
				app.WaitForParserFinished();
				app.SelectTab("Globals");
				AutomationElement tree = Uia.WaitFor(app.MainWindow.Element, "Globals", ControlType.Tree);
				Uia.WaitUntil(() => Count(tree, ControlType.TreeItem) >= 1, "the global variables of the project", TimeSpan.FromSeconds(40));
				app.Step("globals groups: " + Count(tree, ControlType.TreeItem));
				app.Shot(app.MainWindow, "globals-list");
			});

			steps.Run("a group of the Globals tab expands and the filter narrows the list", () =>
			{
				AutomationElement tree = Uia.WaitFor(app.MainWindow.Element, "Globals", ControlType.Tree);
				AutomationElement group = tree.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TreeItem)).Cast<AutomationElement>().First();
				Uia.Expand(group);
				Uia.WaitUntil(() => Count(tree, ControlType.TreeItem) > 1, "the group to expand");
				app.Shot(app.MainWindow, "globals-expanded");

				AutomationElement filter = Uia.WaitFor(app.MainWindow.Element, LocalizedStrings.Get("en", "Str.Panels.GlobalsFilterPlaceholder"), ControlType.Edit);
				Uia.SetValue(filter, "no-such-variable-anywhere");
				Uia.WaitUntil(() => Count(tree, ControlType.TreeItem) == 0, "the filter to hide every variable");
				app.Shot(app.MainWindow, "globals-filtered-empty");
				Uia.SetValue(filter, String.Empty);
				Uia.WaitUntil(() => Count(tree, ControlType.TreeItem) >= 1, "the list to come back");
			});

			steps.Run("the Help tab filters the functions and shows the documentation of one", () =>
			{
				app.SelectTab("Help");
				AutomationElement filter = Uia.WaitFor(app.MainWindow.Element, LocalizedStrings.Get("en", "Str.Panels.HelpFilterPlaceholder"), ControlType.Edit);
				Uia.SetValue(filter, "sfall_global");
				AutomationElement list = Uia.WaitFor(app.MainWindow.Element, LocalizedStrings.Get("en", "Str.Panels.HelpResultsName"), ControlType.List);
				Uia.WaitUntil(() => Count(list, ControlType.ListItem) >= 2, "the functions that match the filter");
				AutomationElement first = list.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem)).Cast<AutomationElement>().First();
				Uia.Select(first);
				System.Threading.Thread.Sleep(500);
				app.Shot(app.MainWindow, "help-filtered");
				app.AssertNothingClipped(app.MainWindow);
				UiChecks.AssertReadableNames(app, app.MainWindow);
			});

			steps.Finish();
			Assert.Equal(0, app.Close());
			app.AssertCleanLog();
		}
	}
}