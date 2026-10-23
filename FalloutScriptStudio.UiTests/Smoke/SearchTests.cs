namespace FalloutScriptStudio.UiTests.Smoke
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Windows.Automation;
	using FalloutScriptStudio.TestSupport;
	using FalloutScriptStudio.UiTests.Support;
	using Xunit;

	/// <summary>Find in Files and the preview of Replace in Files on a copy of the sample mod: the preview shows the changes and writes nothing.</summary>
	[Collection(UiSmokeCollection.Name)]
	public sealed class SearchTests
	{
		private readonly UiSmokeSuite _suite;

		public SearchTests(UiSmokeSuite suite)
		{
			this._suite = suite;
		}

		private static void SetText(AutomationElement box, String text)
		{
			AutomationElement edit = box.Current.ControlType == ControlType.Edit ? box : Uia.WaitFor(box, null, ControlType.Edit);
			Uia.SetValue(edit, text);
		}

		[UiSmokeFact]
		public void Find_in_files_lists_the_lines_and_the_replace_preview_writes_nothing()
		{
			using TestFolder work = new("ui-search");
			String source = Fixtures.CopySampleMod(work.Path);
			String script = Fixtures.SampleScript(source);
			String before = File.ReadAllText(script);
			using AppSession app = AppSession.Start(this._suite, nameof(Find_in_files_lists_the_lines_and_the_replace_preview_writes_nothing), new AppLaunch { Arguments = [script], SettingsJson = Fixtures.CreateSettingsForFolder(source) });
			SmokeSteps steps = new(app);
			AppWindow? search = null;

			steps.Run("Find all lists the lines of the files of a folder", () =>
			{
				app.WaitForParserFinished();
				search = app.OpenNewWindow("Edit", "Find in Files");
				Uia.Select(search.WaitFor("Files in a folder", ControlType.RadioButton));
				SetText(search.WaitFor("Folder to search", ControlType.ComboBox), source);
				SetText(search.WaitFor("Find text", ControlType.ComboBox), "sfall_global");
				Uia.Invoke(search.WaitFor("Find all", ControlType.Button));
				AutomationElement grid = search.WaitFor("Search results", ControlType.DataGrid);
				Uia.WaitUntil(() => grid.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.DataItem)).Count >= 3, "the lines that match", TimeSpan.FromSeconds(30));
				app.Shot(search, "search-results");
				app.AssertNothingClipped(search);
			});

			steps.Run("Replace in Files shows the preview of the changes", () =>
			{
				AppWindow window = search ?? throw new InvalidOperationException("The search window is not open.");
				SetText(window.WaitFor("Replace text", ControlType.Edit), "sfall_global_x");
				Uia.Invoke(window.WaitFor("Replace in Files...", ControlType.Button));
				AppWindow preview = Uia.Wait(() => app.Windows().FirstOrDefault(candidate => candidate.Handle != window.Handle && candidate.Handle != app.MainWindow.Handle), "the preview of the replacement");
				System.Threading.Thread.Sleep(800);
				app.Shot(preview, "replace-preview");
				app.Dump(preview, "replace-preview");
				app.AssertNothingClipped(preview);
				UiChecks.AssertReadableNames(app, preview);
				Assert.Equal(before, File.ReadAllText(script));
				app.CloseWindow(preview);
				app.CloseWindow(window);
			});

			steps.Finish();
			Assert.Equal(0, app.Close());
			app.AssertCleanLog();
			Assert.Equal(before, File.ReadAllText(script));
		}
	}
}