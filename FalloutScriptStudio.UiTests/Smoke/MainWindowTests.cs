namespace FalloutScriptStudio.UiTests.Smoke
{
	using System;
	using System.Linq;
	using System.Windows.Automation;
	using FalloutScriptStudio.TestSupport;
	using FalloutScriptStudio.UiTests.Support;
	using Xunit;

	/// <summary>The main window of the real program with a real script of the sample mod.</summary>
	[Collection(UiSmokeCollection.Name)]
	public sealed class MainWindowTests
	{
		private readonly UiSmokeSuite _suite;

		public MainWindowTests(UiSmokeSuite suite)
		{
			this._suite = suite;
		}

		[UiSmokeFact]
		public void A_sample_script_opens_is_parsed_and_fills_the_procedures()
		{
			using TestFolder work = new("ui-main");
			String script = Fixtures.SampleScript(Fixtures.CopySampleMod(work.Path));
			using AppSession app = AppSession.Start(this._suite, nameof(A_sample_script_opens_is_parsed_and_fills_the_procedures), new AppLaunch { Arguments = [script] });
			SmokeSteps steps = new(app);

			steps.Run("the window shows the script and the parser finishes", () =>
			{
				Assert.StartsWith("Fallout Script Studio", app.MainWindow.Title, StringComparison.Ordinal);
				Assert.Contains("gl_g_ammobox.ssl", app.MainWindow.Title, StringComparison.Ordinal);
				app.WaitForParserDone();
				Uia.WaitFor(app.MainWindow.Element, "gl_g_ammobox.ssl", ControlType.ListItem);
			});

			steps.Run("the procedures tree lists the procedures of the script", () =>
			{
				app.SelectTab("Procedures");
				AutomationElement tree = Uia.WaitFor(app.MainWindow.Element, "Procedures", ControlType.Tree);
				Uia.WaitUntil(() => tree.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TreeItem)).Count >= 10, "the procedures of the script");
				Assert.NotNull(Uia.Find(tree, "get_active_weapon", ControlType.TreeItem));
				Assert.NotNull(Uia.Find(tree, "start", ControlType.TreeItem));
			});

			steps.Run("the picture of the window is not blank and nothing sticks out", () =>
			{
				app.Shot(app.MainWindow, "main-light-en");
				app.AssertNothingClipped(app.MainWindow);
				UiChecks.AssertReadableNames(app, app.MainWindow);
			});

			steps.Run("the six tabs of the right browser stay on one row", () =>
			{
				String[] names = ["Procedures", "Variables", "Functions", "Solution", "Globals", "Help"];
				Double[] rows = names.Select(name => Uia.WaitFor(app.MainWindow.Element, name, ControlType.TabItem).Current.BoundingRectangle.Top).ToArray();
				Assert.True(rows.Max() - rows.Min() < 4, "The tabs of the right browser wrap on several rows: " + String.Join(", ", rows.Select(row => (Int32)row)));
			});

			steps.Run("the right browser tabs are all reachable", () =>
			{
				foreach (String name in new[] { "Variables", "Functions", "Solution", "Globals", "Help", "Procedures" })
				{
					app.SelectTab(name);
					app.Shot(app.MainWindow, "right-tab-" + name.ToLowerInvariant());
				}
			});

			steps.Finish();
			Assert.Equal(0, app.Close());
			app.AssertCleanLog();
		}
	}
}