namespace FalloutScriptStudio.Tests.ToolWindows
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Tests.Shell.Support;
	using FalloutScriptStudio.ViewModels.ToolWindows;
	using Microsoft.Extensions.Logging.Abstractions;
	using Xunit;

	/// <summary>
	/// The Settings window tells which options the open project overrides (the global value is then not in effect), links to the option in
	/// the project settings, and keeps the global values editable.
	/// </summary>
	public sealed class SettingsViewModelProjectOverrideTests : IDisposable
	{
		private readonly TempFolder _root = new TempFolder();
		private readonly SettingsService _settings;
		private readonly ProjectService _projects;
		private readonly String _folder;

		public SettingsViewModelProjectOverrideTests()
		{
			this._settings = new SettingsService(
				AppPaths.FromBaseDirectory(this._root.Combine("app")),
				new LegacySettingsImporter(NullLogger<LegacySettingsImporter>.Instance),
				NullLogger<SettingsService>.Instance);
			this._settings.Load();
			this._projects = new ProjectService(this._settings, NullLogger<ProjectService>.Instance, ProjectFileWatchOptions.Disabled);
			this._folder = this._root.Combine("mod");
			Directory.CreateDirectory(this._folder);
		}

		public void Dispose()
		{
			this._projects.Dispose();
			this._settings.Dispose();
			this._root.Dispose();
		}

		private SettingsViewModel Create()
		{
			return new SettingsViewModel(this._settings, new FakeThemeService(), new FakeDialogService(), new FakeFileAssociationService(), new FakeFontCatalog());
		}

		private void OpenProject(String json)
		{
			File.WriteAllText(Path.Combine(this._folder, ProjectSettings.FileName), json);
			Assert.True(this._projects.Open(this._folder));
		}

		[Fact]
		public void Without_a_project_nothing_is_overridden()
		{
			SettingsViewModel viewModel = this.Create();

			Assert.Empty(viewModel.OverriddenOptions);
			Assert.False(viewModel.HasProjectOverrides);
			Assert.False(viewModel.IsMessagePathsOverridden);
			Assert.False(viewModel.HasProjectDefines);
			Assert.Equal(String.Empty, viewModel.ProjectName);
		}

		[Fact]
		public void A_project_folder_without_a_settings_file_overrides_nothing()
		{
			Assert.True(this._projects.Open(this._folder));

			SettingsViewModel viewModel = this.Create();

			Assert.Empty(viewModel.OverriddenOptions);
			Assert.False(viewModel.HasProjectOverrides);
		}

		[Fact]
		public void The_options_the_project_sets_are_listed_with_the_name_of_the_project()
		{
			this.OpenProject("{ \"name\": \"Great Mod\", \"outputDirectory\": \"out\", \"optimization\": \"full\", \"showWarnings\": false, \"messageSearchDirectories\": [ \"text\" ] }");

			SettingsViewModel viewModel = this.Create();

			Assert.Equal(new[] { ProjectOptions.OutputDirectory, ProjectOptions.Optimization, ProjectOptions.ShowWarnings, ProjectOptions.MessageSearchDirectories }, viewModel.OverriddenOptions);
			Assert.True(viewModel.HasProjectOverrides);
			Assert.True(viewModel.IsMessagePathsOverridden);
			Assert.False(viewModel.HasProjectDefines);
			Assert.Equal("Great Mod", viewModel.ProjectName);
			Assert.Equal("Overridden by project Great Mod", viewModel.OverrideBadgeText);
			Assert.StartsWith("Not in effect while project Great Mod is open", viewModel.OverrideTipText, StringComparison.Ordinal);
		}

		[Fact]
		public void The_name_of_the_folder_is_used_when_the_project_has_no_name()
		{
			this.OpenProject("{ \"shortCircuit\": true }");

			Assert.Equal("Overridden by project mod", this.Create().OverrideBadgeText);
		}

		[Fact]
		public void Project_definitions_and_a_selected_definition_are_noted_on_the_definitions()
		{
			this.OpenProject("{ \"defines\": [ \"MODDEF\" ] }");
			SettingsViewModel viewModel = this.Create();

			Assert.True(viewModel.HasProjectDefines);
			Assert.Equal("Definitions added by project mod", viewModel.DefinesBadgeText);

			this.OpenProject("{ \"preprocessorDefine\": \"RELEASE\" }");
			Assert.True(this.Create().HasProjectDefines);
		}

		[Fact]
		public void The_global_values_stay_editable_and_are_never_replaced_by_the_project_values()
		{
			this.OpenProject("{ \"optimization\": \"full\" }");
			this._settings.Settings.Compiler.Optimization = OptimizationLevel.None;
			SettingsViewModel viewModel = this.Create();

			Assert.Equal(OptimizationLevel.None, viewModel.Draft.Compiler.Optimization);
			viewModel.Draft.Compiler.Optimization = OptimizationLevel.Basic;
			viewModel.OkCommand.Execute(null);

			Assert.Equal(OptimizationLevel.Basic, this._settings.Settings.Compiler.Optimization);
			Assert.Equal(OptimizationLevel.Full, this._settings.Effective.Settings.Compiler.Optimization);
		}

		[Fact]
		public async Task Edit_project_setting_opens_the_project_settings_on_the_option_and_the_list_is_read_again()
		{
			this.OpenProject("{ \"optimization\": \"full\" }");
			SettingsViewModel viewModel = this.Create();
			List<String> asked = [];
			List<String> changed = [];
			viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? String.Empty);
			viewModel.EditProjectSettingHandler = optionId =>
			{
				asked.Add(optionId);
				ProjectSettings edited = this._projects.Settings!.Clone();
				edited.Optimization = null;
				edited.OutputDirectory = Path.Combine(this._folder, "out");
				this._projects.SaveSettings(edited);
				return Task.CompletedTask;
			};

			await viewModel.EditProjectSettingCommand.ExecuteAsync(ProjectOptions.Optimization);

			Assert.Equal(new[] { ProjectOptions.Optimization }, asked);
			Assert.Equal(new[] { ProjectOptions.OutputDirectory }, viewModel.OverriddenOptions);
			Assert.Contains(nameof(SettingsViewModel.OverriddenOptions), changed);
			Assert.Contains(nameof(SettingsViewModel.HasProjectOverrides), changed);
		}

		[Fact]
		public async Task Edit_project_setting_without_a_handler_or_an_option_does_nothing()
		{
			this.OpenProject("{ \"optimization\": \"full\" }");
			SettingsViewModel viewModel = this.Create();

			await viewModel.EditProjectSettingCommand.ExecuteAsync(ProjectOptions.Optimization);
			viewModel.EditProjectSettingHandler = _ => throw new InvalidOperationException("must not be called");
			await viewModel.EditProjectSettingCommand.ExecuteAsync(null);
			await viewModel.EditProjectSettingCommand.ExecuteAsync(String.Empty);

			Assert.Equal(new[] { ProjectOptions.Optimization }, viewModel.OverriddenOptions);
		}

		[Fact]
		public void A_project_that_is_closed_or_changed_after_the_window_opened_is_picked_up_by_the_refresh()
		{
			this.OpenProject("{ \"optimization\": \"full\" }");
			SettingsViewModel viewModel = this.Create();
			Assert.True(viewModel.HasProjectOverrides);

			this._projects.Close();
			viewModel.RefreshProjectOverrides();

			Assert.False(viewModel.HasProjectOverrides);
			Assert.Equal(String.Empty, viewModel.ProjectName);
		}
	}
}