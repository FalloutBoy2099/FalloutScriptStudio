namespace FalloutScriptStudio.Tests.ToolWindows
{
	using System;
	using System.Linq;
	using System.Xml.Linq;
	using FalloutScriptStudio.ViewModels.ToolWindows;
	using Xunit;

	/// <summary>The options <c>GlobalVariableFindings</c> and <c>GlobalVariableIgnore</c> (Settings &gt; Editor): the draft, the text of the ignore list, the commit and the cards.</summary>
	[Collection("Test collection for FalloutScriptStudio.Tests.ToolWindows.SettingsViewModelTests")]
	public sealed class GlobalVariableSettingsTests : SettingsViewModelTestBase
	{
		[Fact]
		public void The_findings_are_off_by_default()
		{
			SettingsViewModel viewModel = this.Create();

			Assert.False(viewModel.Draft.Editor.GlobalVariableFindings);
			Assert.Empty(viewModel.Draft.Editor.GlobalVariableIgnore);
			Assert.Equal(String.Empty, viewModel.GlobalVariableIgnoreText);
		}

		[Fact]
		public void The_ignore_list_is_edited_as_one_entry_per_line()
		{
			this._settings.Settings.Editor.GlobalVariableIgnore.AddRange(["GVAR_A", "27"]);
			SettingsViewModel viewModel = this.Create();
			Assert.Equal("GVAR_A" + Environment.NewLine + "27", viewModel.GlobalVariableIgnoreText);

			viewModel.GlobalVariableIgnoreText = " GVAR_B*\r\n\r\n  gvar_b* \n44\n";

			Assert.Equal(new[] { "GVAR_B*", "44" }, viewModel.Draft.Editor.GlobalVariableIgnore.ToArray());
			Assert.Equal(new[] { "GVAR_A", "27" }, this._settings.Settings.Editor.GlobalVariableIgnore.ToArray());
			viewModel.Commit();
			Assert.Equal(new[] { "GVAR_B*", "44" }, this._settings.Settings.Editor.GlobalVariableIgnore.ToArray());
		}

		[Fact]
		public void The_toggle_is_committed()
		{
			SettingsViewModel viewModel = this.Create();

			viewModel.Draft.Editor.GlobalVariableFindings = true;
			viewModel.Commit();

			Assert.True(this._settings.Settings.Editor.GlobalVariableFindings);
		}

		[Fact]
		public void The_editor_page_has_labelled_cards_for_both_options()
		{
			String directory = AppContext.BaseDirectory;
			while (!System.IO.File.Exists(System.IO.Path.Combine(directory, "FalloutScriptStudio.sln")))
			{
				directory = System.IO.Path.GetDirectoryName(directory)!;
			}

			XDocument window = XDocument.Load(System.IO.Path.Combine(directory, "FalloutScriptStudio", "Views", "ToolWindows", "SettingsWindow.xaml"));

			XElement toggle = window.Descendants().Single(e => e.Name.LocalName == "ToggleSwitch" && (String?)e.Attribute("IsChecked") == "{Binding Draft.Editor.GlobalVariableFindings}");
			Assert.Equal("{DynamicResource Str.ToolWindows.Opt.GlobalFindings.Title}", (String?)toggle.Parent!.Attribute("Title"));
			XElement box = window.Descendants().Single(e => e.Name.LocalName == "TextBox" && ((String?)e.Attribute("Text"))?.StartsWith("{Binding GlobalVariableIgnoreText", StringComparison.Ordinal) == true);
			Assert.Equal("{DynamicResource Str.ToolWindows.Opt.GlobalIgnore.Title}", (String?)box.Parent!.Attribute("Title"));
			Assert.Equal("True", (String?)box.Attribute("AcceptsReturn"));
		}
	}
}