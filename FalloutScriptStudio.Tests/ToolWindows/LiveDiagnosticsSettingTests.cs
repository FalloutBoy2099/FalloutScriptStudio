namespace FalloutScriptStudio.Tests.ToolWindows
{
	using System;
	using System.Linq;
	using System.Xml.Linq;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.ViewModels.ToolWindows;
	using Xunit;

	/// <summary>The option <c>LiveDiagnostics</c> (Settings &gt; Editor): the choices, the draft, the commit and the card of the window.</summary>
	[Collection("Test collection for FalloutScriptStudio.Tests.ToolWindows.SettingsViewModelTests")]
	public sealed class LiveDiagnosticsSettingTests : SettingsViewModelTestBase
	{
		[Fact]
		public void The_three_levels_are_offered_in_the_order_off_errors_errors_and_warnings()
		{
			SettingsViewModel viewModel = this.Create();

			Assert.Equal(
				new[] { LiveDiagnosticsLevel.Off, LiveDiagnosticsLevel.ErrorsOnly, LiveDiagnosticsLevel.ErrorsAndWarnings },
				viewModel.LiveDiagnosticsChoices.Select(choice => choice.Value));
			Assert.Equal(new[] { "Off", "Errors only", "Errors and warnings" }, viewModel.LiveDiagnosticsChoices.Select(choice => choice.Text));
		}

		[Fact]
		public void The_draft_starts_with_the_level_of_the_settings_and_a_commit_writes_the_choice_back()
		{
			this._settings.Settings.Editor.LiveDiagnostics = LiveDiagnosticsLevel.ErrorsOnly;
			SettingsViewModel viewModel = this.Create();
			Assert.Equal(LiveDiagnosticsLevel.ErrorsOnly, viewModel.Draft.Editor.LiveDiagnostics);

			viewModel.Draft.Editor.LiveDiagnostics = LiveDiagnosticsLevel.Off;
			Assert.Equal(LiveDiagnosticsLevel.ErrorsOnly, this._settings.Settings.Editor.LiveDiagnostics);
			viewModel.Commit();

			Assert.Equal(LiveDiagnosticsLevel.Off, this._settings.Settings.Editor.LiveDiagnostics);
		}

		[Fact]
		public void The_editor_page_has_a_card_for_the_option_that_is_labelled_and_bound_to_the_draft()
		{
			String directory = AppContext.BaseDirectory;
			while (!System.IO.File.Exists(System.IO.Path.Combine(directory, "FalloutScriptStudio.sln")))
			{
				directory = System.IO.Path.GetDirectoryName(directory)!;
			}

			XDocument window = XDocument.Load(System.IO.Path.Combine(directory, "FalloutScriptStudio", "Views", "ToolWindows", "SettingsWindow.xaml"));

			XElement box = window.Descendants().Single(e => e.Name.LocalName == "ComboBox" && (String?)e.Attribute("SelectedValue") == "{Binding Draft.Editor.LiveDiagnostics, Mode=TwoWay}");
			XElement card = box.Parent!;
			Assert.Equal("{DynamicResource Str.ToolWindows.Opt.LiveDiagnostics.Title}", (String?)card.Attribute("Title"));
			Assert.Equal("{DynamicResource Str.ToolWindows.Opt.LiveDiagnostics.Desc}", (String?)card.Attribute("Description"));
			Assert.Equal("{Binding LiveDiagnosticsChoices}", (String?)box.Attribute("ItemsSource"));
		}
	}
}