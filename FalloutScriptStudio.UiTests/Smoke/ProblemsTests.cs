namespace FalloutScriptStudio.UiTests.Smoke
{
	using System;
	using System.Linq;
	using System.Windows.Automation;
	using FalloutScriptStudio.TestSupport;
	using FalloutScriptStudio.UiTests.Support;
	using Xunit;

	/// <summary>The program started without its native parser (<c>parser.dll</c> is missing from the private copy): the built-in parser takes over and the Errors tab says so.</summary>
	[Collection(UiSmokeCollection.Name)]
	public sealed class ProblemsTests
	{
		private readonly UiSmokeSuite _suite;

		public ProblemsTests(UiSmokeSuite suite)
		{
			this._suite = suite;
		}

		[UiSmokeFact]
		public void Without_the_native_parser_the_built_in_parser_takes_over_and_the_errors_tab_explains_it()
		{
			using TestFolder work = new("ui-problems");
			String script = Fixtures.SampleScript(Fixtures.CopySampleMod(work.Path));
			using AppSession app = AppSession.Start(this._suite, nameof(Without_the_native_parser_the_built_in_parser_takes_over_and_the_errors_tab_explains_it), new AppLaunch { Arguments = [script], WithoutParser = true });
			SmokeSteps steps = new(app);

			steps.Run("the status bar says that the built-in parser is used", () =>
			{
				String status = app.WaitForParserFinished();
				Assert.Equal(LocalizedStrings.Get("en", "Str.Shell.Parser.Builtin"), status);
				app.Shot(app.MainWindow, "main-built-in-parser");
			});

			steps.Run("the Errors tab explains how to get the exact data", () =>
			{
				app.SelectTab("Errors");
				AutomationElement grid = Uia.WaitFor(app.MainWindow.Element, "Errors", ControlType.DataGrid);
				String[] rows = Uia.Wait(
					() =>
					{
						String[] current = grid.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.DataItem)).Cast<AutomationElement>().Select(item => item.Current.Name).ToArray();
						return current.Any(row => row.Contains("parser.dll", StringComparison.Ordinal)) ? current : null;
					},
					"the problem that tells that parser.dll is missing");
				app.Step("rows: " + String.Join(" | ", rows));
				app.Shot(app.MainWindow, "errors-built-in-parser");
			});

			steps.Run("the built-in parser still fills the procedures", () =>
			{
				app.SelectTab("Procedures");
				AutomationElement tree = Uia.WaitFor(app.MainWindow.Element, "Procedures", ControlType.Tree);
				Uia.WaitUntil(() => tree.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TreeItem)).Count >= 5, "the procedures found by the built-in parser");
				app.Shot(app.MainWindow, "procedures-built-in-parser");
			});

			steps.Finish();
			Assert.Equal(0, app.Close());
			app.AssertCleanLog();
		}
	}
}