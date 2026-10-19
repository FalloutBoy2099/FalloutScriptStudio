namespace FalloutScriptStudio.Core.Tests.Configuration
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;
	using System.Text;
	using FalloutScriptStudio.Core.Building;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.Tests.Compilation;
	using Microsoft.Extensions.Logging.Abstractions;
	using Xunit;

	/// <summary>Compile profiles: named sets of options, applied on top of the global options and those of the project.</summary>
	public sealed class CompileProfileTests : IDisposable
	{
		private readonly TempWorkspace _workspace = new TempWorkspace();

		public void Dispose()
		{
			this._workspace.Dispose();
		}

		private static AppSettings CreateGlobal()
		{
			AppSettings global = new AppSettings();
			global.Compiler.Optimization = OptimizationLevel.Basic;
			global.Compiler.ShortCircuit = true;
			global.Compiler.BackwardCompatibility = false;
			global.Compiler.DebugInfo = true;
			global.Compiler.PreprocessorDefine = "GLOBAL";
			return global;
		}

		private static EffectiveSettings Create(AppSettings global, ProjectSettings? project = null)
		{
			return new EffectiveSettings(global, new PreprocessorDefines(), project is null ? null : @"C:\project", project);
		}

		[Fact]
		public void A_new_installation_offers_a_debug_and_a_release_profile()
		{
			List<CompileProfile> profiles = new AppSettings().Compiler.Profiles;

			Assert.Equal(new[] { "Debug", "Release" }, profiles.Select(profile => profile.Name));
			Assert.Equal(OptimizationLevel.None, profiles[0].Optimization);
			Assert.Equal("DEBUG", profiles[0].PreprocessorDefine);
			Assert.Equal(OptimizationLevel.Full, profiles[1].Optimization);
			Assert.False(profiles[1].DebugInfo);
			Assert.Equal("RELEASE", profiles[1].PreprocessorDefine);
			Assert.Null(new AppSettings().Compiler.ActiveProfile);
		}

		[Fact]
		public void Without_an_active_profile_the_global_settings_themselves_are_in_effect()
		{
			AppSettings global = CreateGlobal();

			EffectiveSettings effective = Create(global);

			Assert.Same(global, effective.Settings);
			Assert.Null(effective.ActiveCompileProfile);
			Assert.Equal(new[] { "Debug", "Release" }, effective.CompileProfiles.Select(profile => profile.Name));
		}

		[Fact]
		public void A_name_that_matches_no_profile_changes_nothing()
		{
			AppSettings global = CreateGlobal();
			global.Compiler.ActiveProfile = "Nightly";

			EffectiveSettings effective = Create(global);

			Assert.Same(global, effective.Settings);
			Assert.Null(effective.ActiveCompileProfile);
		}

		[Fact]
		public void The_active_profile_overrides_only_the_options_it_defines()
		{
			AppSettings global = CreateGlobal();
			global.Compiler.Profiles = [new CompileProfile { Name = "Fast", Optimization = OptimizationLevel.Experimental, BackwardCompatibility = true }];
			global.Compiler.ActiveProfile = "fast";

			EffectiveSettings effective = Create(global);
			CompilerSettings compiler = effective.Settings.Compiler;

			Assert.NotSame(global, effective.Settings);
			Assert.Equal("Fast", effective.ActiveCompileProfile!.Name);
			Assert.Equal(OptimizationLevel.Experimental, compiler.Optimization);
			Assert.True(compiler.BackwardCompatibility);
			Assert.True(compiler.ShortCircuit);
			Assert.True(compiler.DebugInfo);
			Assert.Equal("GLOBAL", compiler.PreprocessorDefine);
			Assert.Equal(OptimizationLevel.Basic, global.Compiler.Optimization);
			Assert.False(global.Compiler.BackwardCompatibility);
		}

		[Fact]
		public void A_profile_can_set_and_clear_the_preprocessor_definition()
		{
			AppSettings global = CreateGlobal();
			global.Compiler.Profiles =
			[
				new CompileProfile { Name = "Debug", PreprocessorDefine = "DEBUG" },
				new CompileProfile { Name = "Plain", PreprocessorDefine = String.Empty },
			];

			global.Compiler.ActiveProfile = "Debug";
			Assert.Equal("DEBUG", Create(global).Settings.Compiler.PreprocessorDefine);

			global.Compiler.ActiveProfile = "Plain";
			Assert.Null(Create(global).Settings.Compiler.PreprocessorDefine);
		}

		[Fact]
		public void The_profile_wins_over_the_options_of_the_project()
		{
			AppSettings global = CreateGlobal();
			global.Compiler.ActiveProfile = "Release";
			ProjectSettings project = new ProjectSettings { Optimization = OptimizationLevel.Experimental, ShortCircuit = false, DebugInfo = true };

			AppSettings effective = Create(global, project).Settings;

			Assert.Equal(OptimizationLevel.Full, effective.Compiler.Optimization);
			Assert.False(effective.Compiler.DebugInfo);
			Assert.False(effective.Compiler.ShortCircuit);
			Assert.Equal("RELEASE", effective.Compiler.PreprocessorDefine);
		}

		[Fact]
		public void The_project_chooses_its_own_profile_and_can_choose_none()
		{
			AppSettings global = CreateGlobal();
			global.Compiler.ActiveProfile = "Release";

			Assert.Equal("Debug", Create(global, new ProjectSettings { ActiveCompileProfile = "Debug" }).ActiveCompileProfile!.Name);
			Assert.Equal("Release", Create(global, new ProjectSettings()).ActiveCompileProfile!.Name);

			EffectiveSettings none = Create(global, new ProjectSettings { ActiveCompileProfile = String.Empty });
			Assert.Null(none.ActiveCompileProfile);
			Assert.Equal(OptimizationLevel.Basic, none.Settings.Compiler.Optimization);
		}

		[Fact]
		public void The_profiles_of_the_project_are_added_to_the_global_ones_and_replace_those_of_the_same_name()
		{
			AppSettings global = CreateGlobal();
			ProjectSettings project = new ProjectSettings
			{
				ActiveCompileProfile = "release",
				CompileProfiles =
				[
					new CompileProfile { Name = "Mod only", Optimization = OptimizationLevel.None },
					new CompileProfile { Name = "RELEASE", Optimization = OptimizationLevel.Basic, DebugInfo = true },
				],
			};

			EffectiveSettings effective = Create(global, project);

			Assert.Equal(new[] { "Debug", "RELEASE", "Mod only" }, effective.CompileProfiles.Select(profile => profile.Name));
			Assert.Equal(OptimizationLevel.Basic, effective.Settings.Compiler.Optimization);
			Assert.True(effective.Settings.Compiler.DebugInfo);
			Assert.Equal("Release", global.Compiler.Profiles[1].Name);
		}

		[Fact]
		public void GetOverriddenOptions_NamesTheProfileOptionsOfTheProject()
		{
			ProjectSettings project = new ProjectSettings
			{
				ActiveCompileProfile = String.Empty,
				CompileProfiles = [new CompileProfile { Name = "Mine" }],
			};

			Assert.Equal(new[] { ProjectOptions.CompileProfiles, ProjectOptions.ActiveCompileProfile }, project.GetOverriddenOptions());
			Assert.Empty(new ProjectSettings().GetOverriddenOptions());
			Assert.Equal(new[] { ProjectOptions.ActiveCompileProfile }, new ProjectSettings { ActiveCompileProfile = "Debug" }.GetOverriddenOptions());
		}

		[Fact]
		public void The_profile_reaches_the_compiler_options_and_changes_the_fingerprint()
		{
			AppSettings global = CreateGlobal();
			AppPaths paths = AppPaths.FromBaseDirectory(this._workspace.Root);

			CompilerOptions none = Create(global).ToCompilerOptions(paths);
			global.Compiler.ActiveProfile = "Release";
			CompilerOptions release = Create(global).ToCompilerOptions(paths);
			global.Compiler.ActiveProfile = "Debug";
			CompilerOptions debug = Create(global).ToCompilerOptions(paths);

			Assert.Equal(OptimizationLevel.Full, release.Optimization);
			Assert.Equal("RELEASE", release.PreprocessorDefine);
			Assert.Equal("DEBUG", debug.PreprocessorDefine);
			Assert.NotEqual(BuildFingerprint.Create(none).Options, BuildFingerprint.Create(release).Options);
			Assert.NotEqual(BuildFingerprint.Create(release).Options, BuildFingerprint.Create(debug).Options);
			Assert.Equal(BuildFingerprint.Create(release).Options, BuildFingerprint.Create(release with { }).Options);
		}

		[Fact]
		public void Names_are_validated_and_repaired()
		{
			Assert.True(CompileProfile.IsValidName("Debug"));
			Assert.True(CompileProfile.IsValidName("  Final build  "));
			Assert.False(CompileProfile.IsValidName(null));
			Assert.False(CompileProfile.IsValidName("   "));
			Assert.False(CompileProfile.IsValidName(new String('x', CompileProfile.MaxNameLength + 1)));
			Assert.False(CompileProfile.IsValidName("a\tb"));

			List<CompileProfile> repaired = CompileProfile.Normalize(new CompileProfile?[]
			{
				new CompileProfile { Name = "  One  ", PreprocessorDefine = "bad define" },
				new CompileProfile { Name = "one", Optimization = OptimizationLevel.Full },
				new CompileProfile { Name = " " },
				null,
				new CompileProfile { Name = "Two", PreprocessorDefine = " TWO " },
			});

			Assert.Equal(new[] { "One", "Two" }, repaired.Select(profile => profile.Name));
			Assert.Equal(String.Empty, repaired[0].PreprocessorDefine);
			Assert.Null(repaired[0].Optimization);
			Assert.Equal("TWO", repaired[1].PreprocessorDefine);
			Assert.Empty(CompileProfile.Normalize(null));
		}

		[Fact]
		public void The_profiles_of_the_settings_survive_a_copy_and_a_normalization()
		{
			AppSettings settings = new AppSettings();
			settings.Compiler.Profiles.Add(new CompileProfile { Name = "Mine", ShortCircuit = true });
			settings.Compiler.ActiveProfile = "  Mine ";

			AppSettings copy = settings.Clone();
			copy.Normalize();

			Assert.Equal(new[] { "Debug", "Release", "Mine" }, copy.Compiler.Profiles.Select(profile => profile.Name));
			Assert.True(copy.Compiler.Profiles[2].ShortCircuit);
			Assert.Equal("Mine", copy.Compiler.ActiveProfile);
			Assert.NotSame(settings.Compiler.Profiles[0], copy.Compiler.Profiles[0]);
		}

		[Fact]
		public void The_effective_settings_never_share_the_profile_objects_with_the_global_ones()
		{
			AppSettings global = CreateGlobal();
			global.Compiler.ActiveProfile = "Debug";

			CompilerSettings effective = Create(global).Settings.Compiler;
			effective.Profiles[0].Name = "Changed";

			Assert.Equal("Debug", global.Compiler.Profiles[0].Name);
		}

		[Fact]
		public void A_settings_file_without_profiles_gets_the_default_ones_and_an_empty_list_stays_empty()
		{
			AppPaths paths = AppPaths.FromBaseDirectory(this._workspace.Root);
			Directory.CreateDirectory(paths.SettingsDirectory);

			File.WriteAllText(paths.SettingsFile, "{ \"compiler\": { \"optimization\": \"full\" } }", new UTF8Encoding(false));
			SettingsService older = new SettingsService(paths, new LegacySettingsImporter(NullLogger<LegacySettingsImporter>.Instance), NullLogger<SettingsService>.Instance);
			older.Load();
			Assert.Equal(new[] { "Debug", "Release" }, older.Settings.Compiler.Profiles.Select(profile => profile.Name));
			Assert.True(older.Settings.Compiler.MassCompileOnlyChanged);
			Assert.False(older.Settings.Compiler.ListUpToDateScripts);

			File.WriteAllText(paths.SettingsFile, "{ \"compiler\": { \"profiles\": [], \"activeProfile\": \"Gone\", \"massCompileOnlyChanged\": false } }", new UTF8Encoding(false));
			SettingsService custom = new SettingsService(paths, new LegacySettingsImporter(NullLogger<LegacySettingsImporter>.Instance), NullLogger<SettingsService>.Instance);
			custom.Load();
			Assert.Empty(custom.Settings.Compiler.Profiles);
			Assert.Equal("Gone", custom.Settings.Compiler.ActiveProfile);
			Assert.False(custom.Settings.Compiler.MassCompileOnlyChanged);
			Assert.Null(custom.Effective.ActiveCompileProfile);
		}

		[Fact]
		public void The_project_file_keeps_its_profiles_and_the_properties_it_does_not_know()
		{
			String folder = this._workspace.Root;
			String json = "{ \"version\": 1, \"futureThing\": 42, \"activeCompileProfile\": \"Fast\", "
				+ "\"compileProfiles\": [ { \"name\": \"Fast\", \"optimization\": \"full\", \"shortCircuit\": true, \"preprocessorDefine\": \"FAST\", \"later\": { \"x\": 1 } } ] }";

			Assert.True(ProjectSettingsFile.TryParse(Encoding.UTF8.GetBytes(json), folder, out ProjectSettings? settings, out _));
			Assert.Equal("Fast", settings!.ActiveCompileProfile);
			CompileProfile profile = Assert.Single(settings.CompileProfiles);
			Assert.Equal(OptimizationLevel.Full, profile.Optimization);
			Assert.True(profile.ShortCircuit);
			Assert.Null(profile.DebugInfo);
			Assert.Equal("FAST", profile.PreprocessorDefine);

			String written = Encoding.UTF8.GetString(ProjectSettingsFile.Serialize(folder, settings));
			Assert.Contains("futureThing", written, StringComparison.Ordinal);
			Assert.Contains("\"later\"", written, StringComparison.Ordinal);
			Assert.Contains("\"optimization\": \"full\"", written, StringComparison.Ordinal);
			Assert.DoesNotContain("debugInfo", written, StringComparison.Ordinal);

			Assert.True(ProjectSettingsFile.TryParse(Encoding.UTF8.GetBytes(written), folder, out ProjectSettings? again, out _));
			Assert.Equal("Fast", again!.ActiveCompileProfile);
			Assert.Single(again.CompileProfiles);
		}

		[Fact]
		public void A_project_file_of_the_previous_version_has_no_profiles()
		{
			Assert.True(ProjectSettingsFile.TryParse(Encoding.UTF8.GetBytes("{ \"version\": 1, \"optimization\": \"full\" }"), this._workspace.Root, out ProjectSettings? settings, out _));

			Assert.Empty(settings!.CompileProfiles);
			Assert.Null(settings.ActiveCompileProfile);
			Assert.Equal(new[] { ProjectOptions.Optimization }, settings.GetOverriddenOptions());
		}

		[Fact]
		public void A_damaged_profile_list_is_repaired_when_the_project_file_is_read()
		{
			String json = "{ \"compileProfiles\": [ { \"name\": \"\" }, { \"name\": \"Ok\" }, { \"name\": \"ok\" } ] }";

			Assert.True(ProjectSettingsFile.TryParse(Encoding.UTF8.GetBytes(json), this._workspace.Root, out ProjectSettings? settings, out _));

			Assert.Equal("Ok", Assert.Single(settings!.CompileProfiles).Name);
		}
	}
}