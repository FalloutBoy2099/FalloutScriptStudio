namespace FalloutScriptStudio.UiTests.Smoke
{
	using System;
	using System.Windows.Automation;
	using FalloutScriptStudio.TestSupport;
	using FalloutScriptStudio.UiTests.Support;
	using Xunit;

	/// <summary>The theme and the language, switched through the View menu of the real program.</summary>
	[Collection(UiSmokeCollection.Name)]
	public sealed class AppearanceTests
	{
		private readonly UiSmokeSuite _suite;

		public AppearanceTests(UiSmokeSuite suite)
		{
			this._suite = suite;
		}

		[UiSmokeFact]
		public void The_view_menu_switches_the_theme_and_the_language()
		{
			using TestFolder work = new("ui-appearance");
			String script = Fixtures.SampleScript(Fixtures.CopySampleMod(work.Path));
			using AppSession app = AppSession.Start(this._suite, nameof(The_view_menu_switches_the_theme_and_the_language), new AppLaunch { Arguments = [script] });
			SmokeSteps steps = new(app);
			Double lightBrightness = 0;

			steps.Run("the light theme is bright", () =>
			{
				app.WaitForParserDone();
				lightBrightness = app.Shot(app.MainWindow, "theme-light").MeanBrightness();
				Assert.True(lightBrightness > 150, "The light theme has a mean brightness of " + lightBrightness);
			});

			steps.Run("the Dark item of the Theme menu darkens the window", () =>
			{
				app.InvokeMenu("View", "Theme", "Dark");
				Uia.WaitUntil(() => WindowImage.Capture(app.MainWindow.Handle, TimeSpan.FromSeconds(2))?.MeanBrightness() < 110, "the window to turn dark", TimeSpan.FromSeconds(10));
				WindowImage dark = app.Shot(app.MainWindow, "theme-dark");
				app.Step("brightness: light " + (Int32)lightBrightness + ", dark " + (Int32)dark.MeanBrightness());
				UiChecks.AssertReadableNames(app, app.MainWindow);
			});

			steps.Run("the Light item brings the bright theme back", () =>
			{
				app.InvokeMenu("View", "Theme", "Light");
				Uia.WaitUntil(() => WindowImage.Capture(app.MainWindow.Handle, TimeSpan.FromSeconds(2))?.MeanBrightness() > 150, "the window to turn bright", TimeSpan.FromSeconds(10));
			});

			steps.Run("the Language menu switches the whole window to French", () =>
			{
				app.InvokeMenu("View", "Language", "Français");
				Uia.WaitFor(app.MainWindow.Element, LocalizedStrings.Get("fr", "Str.Panels.TabProcedures"), ControlType.TabItem, null, TimeSpan.FromSeconds(10));
				app.Shot(app.MainWindow, "language-french");
				app.AssertNothingClipped(app.MainWindow);
			});

			steps.Finish();
			Assert.Equal(0, app.Close());
			app.AssertCleanLog();
		}
	}
}