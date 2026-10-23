namespace FalloutScriptStudio.Tests.Startup
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.TestSupport;
	using FalloutScriptStudio.UiTesting;
	using Xunit;

	/// <summary>The decisions of the opt-in UI test mode: when it is on, where its data goes, what it changes and what it leaves alone in a normal run.</summary>
	public sealed class UiTestModeTests
	{
		private static readonly String Program = Path.Combine(Path.GetPathRoot(Environment.CurrentDirectory)!, "repo", "FalloutScriptStudio", "bin", "Debug", "net10.0-windows");

		private static UiTestMode Read(Dictionary<String, String> variables, Func<String, Boolean>? exists = null)
		{
			return UiTestMode.FromEnvironment(name => variables.TryGetValue(name, out String? value) ? value : null, Program, exists ?? (_ => false), () => new Guid("11111111-2222-3333-4444-555555555555"));
		}

		[Theory]
		[InlineData("1")]
		[InlineData("true")]
		[InlineData("TRUE")]
		[InlineData("yes")]
		[InlineData(" 1 ")]
		public void The_mode_is_on_for_the_accepted_values(String value)
		{
			UiTestMode mode = Read(new Dictionary<String, String> { [UiTestMode.EnabledVariable] = value });

			Assert.True(mode.IsEnabled);
		}

		[Theory]
		[InlineData(null)]
		[InlineData("")]
		[InlineData("0")]
		[InlineData("false")]
		[InlineData("no")]
		[InlineData("2")]
		public void The_mode_is_off_for_everything_else_and_changes_nothing(String? value)
		{
			Dictionary<String, String> variables = new() { [UiTestMode.DataVariable] = Path.Combine(TestFiles.Work, "somewhere"), [UiTestMode.LanguageVariable] = "fr", [UiTestMode.ThemeVariable] = "dark" };
			if (value is not null)
			{
				variables[UiTestMode.EnabledVariable] = value;
			}

			UiTestMode mode = Read(variables);

			Assert.Same(UiTestMode.Off, mode);
			Assert.False(mode.IsEnabled);
			Assert.Null(mode.DataDirectory);
			Assert.True(mode.OpensFirstRunSettings);
			Assert.True(mode.ActivatesWindows);
			Assert.False(mode.OverridesLayout);
			Assert.Equal(Program, mode.GetInstanceKey(Program));
		}

		[Fact]
		public void The_mode_that_is_on_never_opens_the_first_run_window_and_never_activates_a_window()
		{
			UiTestMode mode = Read(new Dictionary<String, String> { [UiTestMode.EnabledVariable] = "1" });

			Assert.False(mode.OpensFirstRunSettings);
			Assert.False(mode.ActivatesWindows);
			Assert.True(mode.OverridesLayout);
		}

		[Fact]
		public void The_language_is_english_and_the_theme_light_unless_the_environment_says_otherwise()
		{
			UiTestMode defaults = Read(new Dictionary<String, String> { [UiTestMode.EnabledVariable] = "1" });
			UiTestMode custom = Read(new Dictionary<String, String> { [UiTestMode.EnabledVariable] = "1", [UiTestMode.LanguageVariable] = " ru ", [UiTestMode.ThemeVariable] = "Dark" });

			Assert.Equal("en", defaults.Language);
			Assert.Equal(AppTheme.Light, defaults.Theme);
			Assert.Equal("ru", custom.Language);
			Assert.Equal(AppTheme.Dark, custom.Theme);
		}

		[Theory]
		[InlineData("light", AppTheme.Light)]
		[InlineData("dark", AppTheme.Dark)]
		[InlineData("DARK", AppTheme.Dark)]
		[InlineData("system", AppTheme.System)]
		[InlineData("unknown", AppTheme.Light)]
		public void The_theme_is_read_from_its_variable(String value, AppTheme expected)
		{
			UiTestMode mode = Read(new Dictionary<String, String> { [UiTestMode.EnabledVariable] = "1", [UiTestMode.ThemeVariable] = value });

			Assert.Equal(expected, mode.Theme);
		}

		[Fact]
		public void The_data_folder_comes_from_its_variable()
		{
			String data = Path.Combine(TestFiles.Work, "ui-smoke-data");

			UiTestMode mode = Read(new Dictionary<String, String> { [UiTestMode.EnabledVariable] = "1", [UiTestMode.DataVariable] = data });

			Assert.Equal(data, mode.DataDirectory);
		}

		[Fact]
		public void The_default_data_folder_is_a_new_folder_of_the_work_folder_in_the_test_files_of_the_repository()
		{
			String testFiles = Path.Combine(Path.GetPathRoot(Program)!, "repo", "_test_files");

			UiTestMode mode = Read(new Dictionary<String, String> { [UiTestMode.EnabledVariable] = "1" }, folder => folder == testFiles);

			Assert.Equal(Path.Combine(testFiles, "work", "ui-smoke-11111111222233334444555555555555"), mode.DataDirectory);
		}

		[Fact]
		public void The_default_data_folder_sits_next_to_the_program_when_there_are_no_test_files()
		{
			UiTestMode mode = Read(new Dictionary<String, String> { [UiTestMode.EnabledVariable] = "1" });

			Assert.Equal(Path.Combine(Program, "_test_files", "work", "ui-smoke-11111111222233334444555555555555"), mode.DataDirectory);
		}

		[Fact]
		public void Under_test_everything_the_program_writes_goes_to_the_data_folder()
		{
			String data = Path.Combine(TestFiles.Work, "ui-smoke-paths");
			UiTestMode mode = Read(new Dictionary<String, String> { [UiTestMode.EnabledVariable] = "1", [UiTestMode.DataVariable] = data });

			AppPaths paths = mode.CreatePaths(Program);

			Assert.Equal(Path.Combine(data, "settings"), paths.SettingsDirectory);
			Assert.Equal(Path.Combine(data, "scrTemp"), paths.ScratchDirectory);
			Assert.Equal(Path.Combine(data, "templates"), paths.TemplatesDirectory);
			Assert.Equal(Path.Combine(data, "sse.log"), paths.LogFile);
			Assert.Equal(Path.Combine(Program, "resources"), paths.ResourcesDirectory);
			Assert.Equal(data, mode.GetInstanceKey(Program));
		}

		[Fact]
		public void A_normal_run_keeps_the_paths_of_the_installation()
		{
			using TestFolder program = new("ui-test-mode");

			AppPaths paths = UiTestMode.Off.CreatePaths(program.Path);

			Assert.Equal(Path.Combine(program.Path, "settings"), paths.SettingsDirectory);
			Assert.Equal(Path.Combine(program.Path, "sse.log"), paths.LogFile);
		}
	}
}