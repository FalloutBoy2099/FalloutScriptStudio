namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.Collections.Generic;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Tests.Shell.Support;
	using Xunit;
	using static FalloutScriptStudio.Tests.Shell.MainViewModelToolsTestData;

	[Collection("Test collection for FalloutScriptStudio.Tests.Shell.MainViewModelToolsTests")]
	public sealed class MainViewModelSettingsTests
	{
		[Fact]
		public void The_menu_toggles_read_and_write_the_settings()
		{
			using ShellHarness harness = new ShellHarness();

			harness.Main.SaveAsUtf8 = true;
			harness.Main.AutoTrimTrailingSpaces = false;
			harness.Main.ShowWhitespace = true;
			harness.Main.ShowIndentLine = false;
			harness.Main.DefineToScriptsHeader = true;
			harness.Main.AutoOpenMessageEditor = true;
			harness.Main.DecompileAsFallout1 = true;
			harness.Main.UseOldDecompiler = true;

			AppSettings settings = harness.Settings.Settings;
			Assert.True(settings.Editor.SaveScriptsAsUtf8);
			Assert.False(settings.Editor.AutoRemoveTrailingSpaces);
			Assert.True(settings.Editor.ShowTabCharacters);
			Assert.False(settings.Editor.ShowVerticalRuler);
			Assert.True(settings.Messages.RegisterScriptAllowDefine);
			Assert.True(settings.Messages.OpenEditorAutomatically);
			Assert.True(settings.Compiler.DecompileForFallout1);
			Assert.True(settings.Compiler.UseLegacyDecompiler);
		}

		[Fact]
		public void Toggles_follow_changes_made_elsewhere_in_the_settings()
		{
			using ShellHarness harness = new ShellHarness();
			String? changed = null;
			harness.Main.PropertyChanged += (_, e) =>
			{
				if (e.PropertyName == nameof(harness.Main.SaveAsUtf8))
				{
					changed = e.PropertyName;
				}
			};

			harness.Settings.Update(settings => settings.Editor.SaveScriptsAsUtf8 = true);

			Assert.NotNull(changed);
			Assert.True(harness.Main.SaveAsUtf8);
		}

		[Fact]
		public void The_old_decompiler_option_is_only_available_with_the_external_decompiler()
		{
			using ShellHarness harness = new ShellHarness();
			List<String?> changed = [];
			harness.Main.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

			harness.Settings.Update(settings => settings.Compiler.Decompiler = DecompilerKind.BuiltIn);
			Assert.False(harness.Main.CanUseOldDecompiler);

			changed.Clear();
			harness.Settings.Update(settings => settings.Compiler.Decompiler = DecompilerKind.ExternalInt2Ssl);
			Assert.True(harness.Main.CanUseOldDecompiler);
			Assert.Contains(nameof(harness.Main.CanUseOldDecompiler), changed);

			harness.Main.DecompileAsFallout1 = true;
			Assert.True(harness.Settings.Settings.Compiler.DecompileForFallout1);
		}

		[Fact]
		public void The_message_encoding_options_exclude_each_other()
		{
			using ShellHarness harness = new ShellHarness();

			harness.Main.UseOemMessageEncoding = true;
			Assert.Equal(TextEncodingKind.Oem866, harness.Settings.Settings.Editor.FileEncoding);
			Assert.False(harness.Main.UseWindowsMessageEncoding);
			harness.Main.UseWindowsMessageEncoding = true;
			Assert.Equal(TextEncodingKind.Ansi, harness.Settings.Settings.Editor.FileEncoding);
			Assert.False(harness.Main.UseOemMessageEncoding);
		}

		[Fact]
		public void The_preprocessor_define_list_starts_with_none_and_selecting_one_stores_it_and_reparses()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Settings.PreprocessorDefines.Items.AddRange(new[] { "DEBUG", " ", "TEST" });
				await harness.OpenFileAsync("a.ssl", Script);
				Int32 startedBefore = harness.Parser.Started;

				// The list is read when the view model starts and after the settings dialog; mimic the dialog.
				await harness.Main.SettingsAsync();
				Assert.Equal(new[] { String.Empty, "DEBUG", "RELEASE", "TEST" }, harness.Main.PreprocessorDefineItems);
				Assert.Equal(String.Empty, harness.Main.SelectedPreprocessorDefine);

				harness.Main.SelectedPreprocessorDefine = "DEBUG";
				Assert.Equal("DEBUG", harness.Settings.Settings.Compiler.PreprocessorDefine);
				Assert.EndsWith("[DEBUG]", harness.Main.Title, StringComparison.Ordinal);
				Assert.True(await Eventually.TrueAsync(() => harness.Parser.Started > startedBefore));

				harness.Main.SelectedPreprocessorDefine = String.Empty;
				Assert.Null(harness.Settings.Settings.Compiler.PreprocessorDefine);
			});
		}

		[Fact]
		public void The_settings_dialog_refreshes_the_parse_data()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				await harness.OpenFileAsync("a.ssl", Script);
				Int32 macroReloads = harness.Parser.MacroReloads;

				await harness.Main.SettingsAsync();

				Assert.Contains("settings", harness.ToolWindows.Calls);
				Assert.True(harness.Parser.MacroReloads > macroReloads);
			});
		}
	}
}