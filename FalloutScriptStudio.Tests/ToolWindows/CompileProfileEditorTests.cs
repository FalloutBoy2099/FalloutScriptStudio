namespace FalloutScriptStudio.Tests.ToolWindows
{
	using System;
	using System.Collections.Generic;
	using System.ComponentModel;
	using System.IO;
	using System.Linq;
	using System.Text.Json;
	using System.Text.Json.Serialization;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Tests.Shell.Support;
	using FalloutScriptStudio.ViewModels.ToolWindows;
	using Microsoft.Extensions.Logging.Abstractions;
	using Xunit;

	/// <summary>The editor of compile profiles, in the Settings window (global profiles) and in the project settings window.</summary>
	[Collection("Test collection for FalloutScriptStudio.Tests.ToolWindows.SettingsViewModelTests")]
	public sealed class CompileProfileEditorTests : SettingsViewModelTestBase
	{
		private static CompileProfileEditorViewModel CreateEditor(IEnumerable<CompileProfile> profiles, CompilerSettings? baseline = null)
		{
			CompileProfileEditorViewModel editor = new CompileProfileEditorViewModel(() => new[] { "DEBUG", "RELEASE", "MINE" });
			editor.Load(profiles, baseline ?? new CompilerSettings());
			return editor;
		}

		[Fact]
		public void Load_shows_the_profiles_selects_the_first_and_offers_the_definitions()
		{
			CompileProfileEditorViewModel editor = CreateEditor(CompileProfile.CreateDefaults());

			Assert.Equal(new[] { "Debug", "Release" }, editor.Profiles.Select(profile => profile.Name));
			Assert.Same(editor.Profiles[0], editor.SelectedProfile);
			Assert.True(editor.HasSelection);
			Assert.Equal(new[] { "DEBUG", "RELEASE", "MINE" }, editor.DefineChoices);
			Assert.Equal(4, editor.OptimizationChoices.Count);
		}

		[Fact]
		public void An_option_the_profile_does_not_set_inherits_and_shows_the_baseline()
		{
			CompilerSettings baseline = new CompilerSettings { Optimization = OptimizationLevel.Experimental, ShortCircuit = true, DebugInfo = false };
			CompileProfileEditorViewModel editor = CreateEditor(new[] { new CompileProfile { Name = "P", BackwardCompatibility = true } }, baseline);

			CompileProfileItem item = editor.Profiles[0];

			Assert.False(item.Optimization.IsOverridden);
			Assert.Equal(OptimizationLevel.Experimental, item.Optimization.Value);
			Assert.False(item.DebugInfo.IsOverridden);
			Assert.True(item.ShortCircuit.Value);
			Assert.True(item.BackwardCompatibility.IsOverridden);
			Assert.True(item.BackwardCompatibility.Value);
			CompileProfile profile = item.ToProfile();
			Assert.Null(profile.Optimization);
			Assert.Null(profile.ShortCircuit);
			Assert.Null(profile.DebugInfo);
			Assert.Null(profile.PreprocessorDefine);
			Assert.True(profile.BackwardCompatibility);
		}

		[Fact]
		public void Overriding_an_option_stores_the_value_and_inheriting_again_forgets_it()
		{
			CompileProfileEditorViewModel editor = CreateEditor(new[] { new CompileProfile { Name = "P" } });
			CompileProfileItem item = editor.Profiles[0];

			item.Optimization.ModeIndex = 1;
			item.Optimization.Value = OptimizationLevel.None;
			item.PreprocessorDefine.ModeIndex = 1;
			item.PreprocessorDefine.Value = " MINE ";
			CompileProfile set = item.ToProfile();
			Assert.Equal(OptimizationLevel.None, set.Optimization);
			Assert.Equal("MINE", set.PreprocessorDefine);

			item.Optimization.ModeIndex = 0;
			item.PreprocessorDefine.ModeIndex = 0;
			CompileProfile cleared = item.ToProfile();
			Assert.Null(cleared.Optimization);
			Assert.Null(cleared.PreprocessorDefine);

			// An empty definition that is set means "no definition", which is not the same as not setting it.
			item.PreprocessorDefine.ModeIndex = 1;
			item.PreprocessorDefine.Value = String.Empty;
			Assert.Equal(String.Empty, item.ToProfile().PreprocessorDefine);
		}

		[Fact]
		public void Adding_profiles_gives_each_a_free_name_selects_it_and_tells_the_host()
		{
			CompileProfileEditorViewModel editor = CreateEditor(Array.Empty<CompileProfile>());
			Int32 changes = 0;
			editor.ProfilesChanged += (_, _) => changes++;
			Assert.False(editor.HasSelection);
			Assert.False(editor.DeleteProfileCommand.CanExecute(null));

			editor.AddProfileCommand.Execute(null);
			editor.AddProfileCommand.Execute(null);

			Assert.Equal(new[] { "New profile", "New profile 2" }, editor.Profiles.Select(profile => profile.Name));
			Assert.Same(editor.Profiles[1], editor.SelectedProfile);
			Assert.True(editor.DeleteProfileCommand.CanExecute(null));
			Assert.Equal(2, changes);
		}

		[Fact]
		public void A_profile_can_be_renamed_but_not_to_a_name_that_is_blank_too_long_or_taken()
		{
			CompileProfileEditorViewModel editor = CreateEditor(CompileProfile.CreateDefaults());
			CompileProfileItem debug = editor.Profiles[0];
			List<String> raised = [];
			((INotifyPropertyChanged)debug).PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? String.Empty);
			Int32 changes = 0;
			editor.ProfilesChanged += (_, _) => changes++;

			debug.Name = "  Trace  ";
			Assert.Equal("Trace", debug.Name);
			Assert.Equal(1, changes);

			foreach (String refused in new[] { "release", "   ", new String('x', CompileProfile.MaxNameLength + 1) })
			{
				raised.Clear();
				debug.Name = refused;
				Assert.Equal("Trace", debug.Name);
				Assert.Contains(nameof(CompileProfileItem.Name), raised);
			}

			Assert.Equal(1, changes);
			Assert.Equal(new[] { "Trace", "Release" }, editor.ToProfiles().Select(profile => profile.Name));
		}

		[Fact]
		public void Deleting_selects_the_next_profile_and_the_last_one_leaves_no_selection()
		{
			CompileProfileEditorViewModel editor = CreateEditor(CompileProfile.CreateDefaults());
			Int32 changes = 0;
			editor.ProfilesChanged += (_, _) => changes++;

			editor.DeleteProfileCommand.Execute(null);
			Assert.Equal(new[] { "Release" }, editor.Profiles.Select(profile => profile.Name));
			Assert.Same(editor.Profiles[0], editor.SelectedProfile);

			editor.DeleteProfileCommand.Execute(null);
			Assert.Empty(editor.Profiles);
			Assert.Null(editor.SelectedProfile);
			Assert.False(editor.HasSelection);
			Assert.Equal(2, changes);
		}

		[Fact]
		public void The_profiles_keep_the_properties_this_version_does_not_know()
		{
			CompileProfile source = JsonSerializer.Deserialize<CompileProfile>("{ \"name\": \"P\", \"optimization\": \"full\", \"future\": { \"x\": 1 } }", new JsonSerializerOptions { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } })!;
			CompileProfileEditorViewModel editor = CreateEditor(new[] { source });

			CompileProfile saved = editor.ToProfiles().Single();

			Assert.Equal(OptimizationLevel.Full, saved.Optimization);
			Assert.NotNull(saved.UnknownProperties);
			Assert.True(saved.UnknownProperties!.ContainsKey("future"));
		}

		[Fact]
		public void MapName_follows_a_renamed_profile_and_leaves_other_names_alone()
		{
			CompileProfileEditorViewModel editor = CreateEditor(CompileProfile.CreateDefaults());
			editor.Profiles[0].Name = "Trace";

			Assert.Equal("Trace", editor.MapName("debug"));
			Assert.Equal("Release", editor.MapName("Release"));
			Assert.Equal("Other", editor.MapName("Other"));
			Assert.Null(editor.MapName(null));
			Assert.Equal(String.Empty, editor.MapName(String.Empty));
		}

		[Fact]
		public void A_definition_a_profile_uses_stays_in_the_list_of_choices()
		{
			CompileProfileEditorViewModel editor = CreateEditor(new[] { new CompileProfile { Name = "P", PreprocessorDefine = "ODD" } });

			Assert.Equal(new[] { "DEBUG", "RELEASE", "MINE", "ODD" }, editor.DefineChoices);
		}

		[Fact]
		public void The_settings_window_saves_the_profiles_and_the_chosen_profile_follows_a_rename()
		{
			this._settings.Settings.Compiler.ActiveProfile = "Debug";
			SettingsViewModel viewModel = this.Create();
			CompileProfileEditorViewModel editor = viewModel.ProfileEditor;
			Assert.Equal(new[] { "Debug", "Release" }, editor.Profiles.Select(profile => profile.Name));

			editor.Profiles[0].Name = "Trace";
			editor.Profiles[0].Optimization.ModeIndex = 1;
			editor.Profiles[0].Optimization.Value = OptimizationLevel.Experimental;
			editor.AddProfileCommand.Execute(null);
			viewModel.Commit();

			List<CompileProfile> saved = this._settings.Settings.Compiler.Profiles;
			Assert.Equal(new[] { "Trace", "Release", "New profile" }, saved.Select(profile => profile.Name));
			Assert.Equal(OptimizationLevel.Experimental, saved[0].Optimization);
			Assert.Equal("Trace", this._settings.Settings.Compiler.ActiveProfile);
			Assert.NotSame(viewModel.Draft.Compiler.Profiles, saved);
		}

		[Fact]
		public void The_settings_window_leaves_the_chosen_profile_alone_when_nothing_was_renamed_and_never_touches_profiles_it_did_not_show()
		{
			this._settings.Settings.Compiler.ActiveProfile = "Release";
			this._settings.Settings.Compiler.Profiles = [new CompileProfile { Name = "Release" }];
			SettingsViewModel viewModel = this.Create();
			Int32 updatesBefore = this._settings.UpdateCount;

			viewModel.Commit();

			Assert.Equal("Release", this._settings.Settings.Compiler.ActiveProfile);
			Assert.Equal("Release", Assert.Single(this._settings.Settings.Compiler.Profiles).Name);
			Assert.Equal(updatesBefore + 1, this._settings.UpdateCount);
		}

		[Fact]
		public void The_definitions_of_the_settings_window_reach_the_profile_editor()
		{
			SettingsViewModel viewModel = this.Create();
			CompileProfileEditorViewModel editor = viewModel.ProfileEditor;
			Assert.Equal(new[] { "DEBUG", "RELEASE" }, editor.DefineChoices);

			viewModel.NewPreprocessorDefine = "EXTRA";
			viewModel.AddPreprocessorDefineCommand.Execute(null);

			Assert.Equal(new[] { "DEBUG", "RELEASE", "EXTRA" }, editor.DefineChoices);
		}
	}

	/// <summary>The profiles in the project settings window.</summary>
	public sealed class ProjectCompileProfileTests : IDisposable
	{
		private readonly TempFolder _root = new TempFolder();
		private readonly SettingsService _settings;
		private readonly ProjectService _projects;
		private readonly String _folder;

		public ProjectCompileProfileTests()
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

		private String ProjectFile => Path.Combine(this._folder, ProjectSettings.FileName);

		private ProjectSettingsViewModel Open(String json)
		{
			File.WriteAllText(this.ProjectFile, json);
			Assert.True(this._projects.Open(this._folder));
			return new ProjectSettingsViewModel(this._projects, this._settings, new FakeDialogService());
		}

		[Fact]
		public void A_project_without_profiles_inherits_the_active_one_and_offers_the_global_profiles()
		{
			this._settings.Settings.Compiler.ActiveProfile = "Release";
			ProjectSettingsViewModel viewModel = this.Open("{ }");

			Assert.Empty(viewModel.ProfileEditor.Profiles);
			Assert.False(viewModel.ActiveProfile.IsOverridden);
			Assert.Equal("Release", viewModel.ActiveProfile.Value);
			Assert.Equal(new[] { String.Empty, "Debug", "Release" }, viewModel.ActiveProfileChoices.Select(option => option.Value));
			ProjectSettings saved = viewModel.ToSettings();
			Assert.Empty(saved.CompileProfiles);
			Assert.Null(saved.ActiveCompileProfile);
		}

		[Fact]
		public void The_profiles_and_the_choice_of_the_project_are_saved_to_its_file()
		{
			ProjectSettingsViewModel viewModel = this.Open("{ \"futureThing\": 1 }");

			viewModel.ProfileEditor.AddProfileCommand.Execute(null);
			CompileProfileItem mine = viewModel.ProfileEditor.SelectedProfile!;
			mine.Name = "Mine";
			mine.ShortCircuit.ModeIndex = 1;
			mine.ShortCircuit.Value = true;
			viewModel.ActiveProfile.ModeIndex = 1;
			viewModel.ActiveProfile.Value = "Mine";
			Assert.Equal(new[] { String.Empty, "Debug", "Mine", "Release" }, viewModel.ActiveProfileChoices.Select(option => option.Value).OrderBy(name => name, StringComparer.Ordinal));

			viewModel.SaveCommand.Execute(null);

			String written = File.ReadAllText(this.ProjectFile);
			Assert.Contains("\"activeCompileProfile\": \"Mine\"", written, StringComparison.Ordinal);
			Assert.Contains("\"name\": \"Mine\"", written, StringComparison.Ordinal);
			Assert.Contains("\"shortCircuit\": true", written, StringComparison.Ordinal);
			Assert.Contains("futureThing", written, StringComparison.Ordinal);
			EffectiveSettings effective = this._settings.Effective;
			Assert.Equal("Mine", effective.ActiveCompileProfile!.Name);
			Assert.True(effective.Settings.Compiler.ShortCircuit);
			Assert.Contains(ProjectOptions.CompileProfiles, effective.GetOverriddenOptions());
			Assert.Contains(ProjectOptions.ActiveCompileProfile, effective.GetOverriddenOptions());
		}

		[Fact]
		public void The_project_can_choose_no_profile_and_a_renamed_profile_stays_chosen()
		{
			ProjectSettingsViewModel none = this.Open("{ \"activeCompileProfile\": \"\" }");
			Assert.True(none.ActiveProfile.IsOverridden);
			Assert.Equal(String.Empty, none.ToSettings().ActiveCompileProfile);

			ProjectSettingsViewModel renamed = this.Open("{ \"activeCompileProfile\": \"Mine\", \"compileProfiles\": [ { \"name\": \"Mine\" } ] }");
			renamed.ProfileEditor.Profiles[0].Name = "Ours";

			Assert.Equal("Ours", renamed.ToSettings().ActiveCompileProfile);
			Assert.Equal("Ours", Assert.Single(renamed.ToSettings().CompileProfiles).Name);
			Assert.Contains("Ours", renamed.ActiveProfileChoices.Select(option => option.Value));
			Assert.DoesNotContain("Mine", renamed.ActiveProfileChoices.Select(option => option.Value));
		}
	}
}