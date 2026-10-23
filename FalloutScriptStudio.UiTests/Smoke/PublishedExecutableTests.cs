namespace FalloutScriptStudio.UiTests.Smoke
{
	using System;
	using System.Windows.Automation;
	using FalloutScriptStudio.TestSupport;
	using FalloutScriptStudio.UiTests.Support;
	using Xunit;

	/// <summary>The two executables that <c>Publish.ps1</c> builds (the single-file program and the one that carries the .NET runtime), started from a private copy of the publish folder.</summary>
	[Collection(UiSmokeCollection.Name)]
	public sealed class PublishedExecutableTests
	{
		private readonly UiSmokeSuite _suite;

		public PublishedExecutableTests(UiSmokeSuite suite)
		{
			this._suite = suite;
		}

		[UiSmokePublishedTheory]
		[InlineData("FalloutScriptStudio.exe")]
		[InlineData("FalloutScriptStudio-NET.exe")]
		public void A_published_executable_starts_opens_a_script_parses_it_and_exits(String executable)
		{
			using TestFolder work = new("ui-published");
			String script = Fixtures.SampleScript(Fixtures.CopySampleMod(work.Path));
			using AppSession app = AppSession.Start(this._suite, "Published_" + executable.Replace(".exe", String.Empty, StringComparison.Ordinal).Replace('-', '_'), new AppLaunch { Arguments = [script], Executable = executable, ProgramDirectory = this._suite.PublishCopy });
			SmokeSteps steps = new(app);

			steps.Run("the window opens the script and the native parser finishes", () =>
			{
				Assert.Contains("gl_g_ammobox.ssl", app.MainWindow.Title, StringComparison.Ordinal);
				app.WaitForParserDone(TimeSpan.FromSeconds(60));
				app.SelectTab("Procedures");
				AutomationElement tree = Uia.WaitFor(app.MainWindow.Element, "Procedures", ControlType.Tree);
				Uia.WaitUntil(() => tree.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TreeItem)).Count >= 10, "the procedures of the script");
				app.Shot(app.MainWindow, "published-main");
			});

			steps.Finish();
			Assert.Equal(0, app.Close());
			app.AssertCleanLog();
		}
	}
}