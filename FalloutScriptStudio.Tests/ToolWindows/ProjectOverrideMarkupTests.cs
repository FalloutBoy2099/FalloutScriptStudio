namespace FalloutScriptStudio.Tests.ToolWindows
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text.RegularExpressions;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Localization;
	using FalloutScriptStudio.Tests.Accessibility;
	using Xunit;

	/// <summary>
	/// The markup that ties the Settings window and the project settings window together: every option a project can override has a
	/// marked card in both windows, and the texts of the new buttons, notes and tool tips exist in every language.
	/// </summary>
	public sealed class ProjectOverrideMarkupTests
	{
		/// <summary>The options of the project that have a control in the Settings window (the others have no global counterpart in it).</summary>
		private static readonly String[] GlobalControls =
		{
			ProjectOptions.OutputDirectory,
			ProjectOptions.HeadersDirectory,
			ProjectOptions.ScriptsHeaderFile,
			ProjectOptions.Preprocessor,
			ProjectOptions.Optimization,
			ProjectOptions.ShowWarnings,
			ProjectOptions.DebugInfo,
			ProjectOptions.ShortCircuit,
			ProjectOptions.BackwardCompatibility,
			ProjectOptions.SearchIncludePath,
			ProjectOptions.CompileToSourceDirectory,
		};

		private static String Read(String name)
		{
			return File.ReadAllText(Path.Combine(SourceTree.AppFolder, "Views", "ToolWindows", name));
		}

		private static IReadOnlyList<String> OptionIds(String xaml)
		{
			return Regex.Matches(xaml, @"OptionId=""([A-Za-z]+)""").Select(match => match.Groups[1].Value).ToList();
		}

		[Fact]
		public void Every_option_that_has_a_global_control_is_marked_on_its_card_in_the_settings_window()
		{
			IReadOnlyList<String> marked = OptionIds(Read("SettingsWindow.xaml"));

			foreach (String option in GlobalControls)
			{
				Assert.Single(marked, option);
			}

			Assert.Equal(GlobalControls.Length, marked.Count);
		}

		[Fact]
		public void The_sections_of_the_settings_window_that_are_not_cards_link_to_their_option_too()
		{
			String xaml = Read("SettingsWindow.xaml");

			Assert.Contains("CommandParameter=\"Defines\"", xaml, StringComparison.Ordinal);
			Assert.Contains("CommandParameter=\"MessageSearchDirectories\"", xaml, StringComparison.Ordinal);
			Assert.Contains("Binding HasProjectDefines", xaml, StringComparison.Ordinal);
			Assert.Contains("Binding IsMessagePathsOverridden", xaml, StringComparison.Ordinal);
		}

		[Fact]
		public void The_card_of_the_settings_window_shows_the_note_the_link_and_dims_the_control_with_a_tool_tip()
		{
			String xaml = Read("SettingsWindow.xaml");

			Assert.Contains("Binding DataContext.OverrideBadgeText, RelativeSource={RelativeSource TemplatedParent}", xaml, StringComparison.Ordinal);
			Assert.Contains("Binding DataContext.EditProjectSettingCommand, RelativeSource={RelativeSource TemplatedParent}", xaml, StringComparison.Ordinal);
			Assert.Contains("Content=\"{DynamicResource Str.ToolWindows.Override.Edit}\"", xaml, StringComparison.Ordinal);
			Assert.Matches(@"<Trigger Property=""IsOverridden"" Value=""True"">", xaml);
			Assert.Contains("Property=\"Opacity\" Value=\"0.5\"", xaml, StringComparison.Ordinal);
			Assert.Contains("Binding DataContext.OverrideTipText", xaml, StringComparison.Ordinal);
			Assert.Contains("DataContext.OverriddenOptions", xaml, StringComparison.Ordinal);
			Assert.DoesNotMatch(new Regex(@"IsEnabled=""\{Binding [A-Za-z.]*Overrid"), xaml);
		}

		[Fact]
		public void Every_option_of_a_project_has_a_card_in_the_project_settings_window_to_scroll_to()
		{
			String xaml = Read("ProjectSettingsWindow.xaml");
			IReadOnlyList<String> marked = OptionIds(xaml);
			String[] expected = typeof(ProjectOptions).GetFields().Where(field => field.IsLiteral).Select(field => (String)field.GetRawConstantValue()!).Where(id => id != ProjectOptions.Defines).ToArray();

			foreach (String option in expected)
			{
				Assert.Single(marked, option);
			}

			Assert.Contains("Tag=\"Defines\"", xaml, StringComparison.Ordinal);
		}

		[Fact]
		public void The_project_settings_window_has_a_reload_button_and_a_button_that_opens_the_file()
		{
			String xaml = Read("ProjectSettingsWindow.xaml");

			Assert.Contains("Command=\"{Binding ReloadFromFileCommand}\"", xaml, StringComparison.Ordinal);
			Assert.Contains("Content=\"{DynamicResource Str.ToolWindows.Project.Reload}\"", xaml, StringComparison.Ordinal);
			Assert.Contains("Command=\"{Binding OpenProjectFileCommand}\"", xaml, StringComparison.Ordinal);
			Assert.Contains("Content=\"{Binding OpenFileText}\"", xaml, StringComparison.Ordinal);
		}

		[Fact]
		public void The_editor_page_offers_the_option_to_restore_the_open_files_of_a_project()
		{
			String xaml = Read("SettingsWindow.xaml");

			Assert.Contains("IsChecked=\"{Binding Draft.Editor.RestoreProjectDocuments}\"", xaml, StringComparison.Ordinal);
			Assert.Contains("Str.ToolWindows.Opt.RestoreFiles.Title", xaml, StringComparison.Ordinal);
			Assert.Contains("Str.ToolWindows.Opt.RestoreFiles.Desc", xaml, StringComparison.Ordinal);
		}

		[Theory]
		[InlineData("Str.ToolWindows.Override.Badge", "{0}")]
		[InlineData("Str.ToolWindows.Override.Tip", "{0}")]
		[InlineData("Str.ToolWindows.Override.DefinesBadge", "{0}")]
		[InlineData("Str.ToolWindows.Override.Edit", null)]
		[InlineData("Str.ToolWindows.Project.Reload", null)]
		[InlineData("Str.ToolWindows.Project.Reload.Tip", null)]
		[InlineData("Str.ToolWindows.Project.OpenFile", "{0}")]
		[InlineData("Str.ToolWindows.Project.OpenFile.Tip", null)]
		[InlineData("Str.ToolWindows.Opt.RestoreFiles.Title", null)]
		[InlineData("Str.ToolWindows.Opt.RestoreFiles.Desc", null)]
		[InlineData("Str.Shell.Project.Reloaded", null)]
		[InlineData("Str.Shell.Project.FileDeleted", "{0}")]
		[InlineData("Str.Shell.Project.FileInvalid", "{0}")]
		[InlineData("Str.Shell.Project.FilesRestored", "{0}")]
		[InlineData("Str.Shell.Project.FilesAvailable", "{1}")]
		[InlineData("Str.Shell.Problem.ProjectFileInvalid", "{3}")]
		[InlineData("Str.Shell.Problem.ProjectFileInvalidNoPosition", "{1}")]
		[InlineData("Str.Shell.Problem.ProjectFileInvalidHint", null)]
		[InlineData("Str.Shell.Problem.ProjectDocumentsAvailable", "{1}")]
		[InlineData("Str.Shell.Problem.ProjectDocumentsAvailableHint", null)]
		[InlineData("Str.Shell.Problem.ActionOpenFile", null)]
		[InlineData("Str.Shell.Problem.ActionRestoreFiles", null)]
		public void The_new_texts_exist_in_every_language(String key, String? placeholder)
		{
			foreach (LanguageInfo language in Loc.Languages)
			{
				String text = SourceTree.LoadStrings(language.Code)[key];

				Assert.False(String.IsNullOrWhiteSpace(text), language.Code + " " + key);
				if (placeholder is not null)
				{
					Assert.Contains(placeholder, text, StringComparison.Ordinal);
				}
			}
		}
	}
}
