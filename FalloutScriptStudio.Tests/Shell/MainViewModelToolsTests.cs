namespace FalloutScriptStudio.Tests.Shell
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;
	using FalloutScriptStudio.Core.Compilation;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Documents;
	using FalloutScriptStudio.Services;
	using FalloutScriptStudio.Tests.Shell.Support;
	using FalloutScriptStudio.ViewModels.Main;
	using FalloutScriptStudio.Workspace;
	using Xunit;

	public sealed class MainViewModelToolsTests
	{
		private const String Script = "procedure start begin\r\nend\r\n";

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
			List<String?> changed = new List<String?>();
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

		[Fact]
		public void The_first_run_flow_shows_settings_and_marks_the_run_as_completed()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Dialogs.ConfirmAnswer = false;
				Assert.False(harness.Settings.Settings.IsFirstRunCompleted);

				await harness.Main.RunFirstRunAsync();

				Assert.Contains("settings", harness.ToolWindows.Calls);
				Assert.True(harness.Settings.Settings.IsFirstRunCompleted);

				harness.ToolWindows.Calls.Clear();
				await harness.Main.RunFirstRunAsync();
				Assert.Empty(harness.ToolWindows.Calls);
			});
		}

		[Fact]
		public void The_theme_command_applies_and_stores_the_mode()
		{
			using ShellHarness harness = new ShellHarness();

			harness.Main.SetThemeCommand.Execute(AppTheme.Dark);

			Assert.Equal(AppTheme.Dark, harness.Theme.Current);
			Assert.Equal(HighlightTheme.Dark, harness.Settings.Settings.Editor.Theme);
			Assert.Equal(AppTheme.Dark, harness.Main.CurrentTheme);
		}

		[Fact]
		public void Panel_toggles_and_the_view_options_are_persisted_in_the_layout_file()
		{
			using ShellHarness harness = new ShellHarness();
			harness.Main.ShowFunctionTree = true;
			harness.Main.ShowRightBrowser = false;
			harness.Main.IsOutputMinimized = true;
			harness.Main.ShowLineNumbers = false;
			harness.Main.HighlightParseErrors = false;
			harness.Main.RightTabIndex = 2;

			Assert.False(harness.ViewOptions.ShowLineNumbers);
			Assert.False(harness.ViewOptions.HighlightParseErrors);
			ShellLayout layout = new ShellLayoutStore(harness.Paths).Load();
			Assert.True(layout.ShowFunctionTree);
			Assert.False(layout.ShowRightBrowser);
			Assert.True(layout.LogMinimized);
			Assert.False(layout.ShowLineNumbers);
			Assert.False(layout.HighlightParseErrors);
			Assert.Equal(2, layout.RightTabIndex);
		}

		[Fact]
		public void The_log_window_toggle_is_stored_in_the_window_settings()
		{
			using ShellHarness harness = new ShellHarness();

			harness.Main.ShowLog = false;

			Assert.False(harness.Settings.Settings.Window.ShowLog);
			harness.Main.ShowLog = true;
			Assert.True(harness.Settings.Settings.Window.ShowLog);
		}

		[Fact]
		public void A_request_to_show_the_output_makes_the_panel_visible_again()
		{
			using ShellHarness harness = new ShellHarness();
			harness.Main.ShowLog = false;
			harness.Main.IsOutputMinimized = true;

			harness.Output.ShowTab(FalloutScriptStudio.Workspace.OutputTab.Errors);

			Assert.True(harness.Main.ShowLog);
			Assert.False(harness.Main.IsOutputMinimized);
		}

		[Fact]
		public void The_scripts_list_editor_opens_without_a_script()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();

				await harness.Main.ScriptsListAsync();

				Assert.Equal("register:<none>", Assert.Single(harness.ScriptTools.Calls));
			});
		}

		[Theory]
		[InlineData("toolongname1.ssl", "8 characters")]
		[InlineData("a b.ssl", "spaces")]
		public void Registering_checks_the_script_name(String fileName, String expectedMessage)
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				await harness.OpenFileAsync(fileName, Script);

				await harness.Main.RegisterScriptAsync();

				Assert.Contains(expectedMessage, Assert.Single(harness.Dialogs.Messages), StringComparison.Ordinal);
				Assert.Empty(harness.ScriptTools.Calls);
			});
		}

		[Fact]
		public void Registering_a_valid_script_opens_the_registration_window()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				await harness.OpenFileAsync("npc.ssl", Script);

				await harness.Main.RegisterScriptAsync();

				Assert.Equal("register:npc.ssl", Assert.Single(harness.ScriptTools.Calls));
			});
		}

		[Fact]
		public void Global_scripts_are_confirmed_once_by_the_script_tool_service_not_by_the_main_view_model()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				await harness.OpenFileAsync("glob.ssl", Script);
				harness.Dialogs.ConfirmAnswer = false;

				await harness.Main.RegisterScriptAsync();

				Assert.Equal("register:glob.ssl", Assert.Single(harness.ScriptTools.Calls));
				Assert.Empty(harness.Dialogs.Confirmations);
			});
		}

		[Fact]
		public void Unsaved_scripts_cannot_be_registered()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Workspace.CreateNew(Script);

				await harness.Main.RegisterScriptAsync();

				Assert.Single(harness.Dialogs.Messages);
				Assert.Empty(harness.ScriptTools.Calls);
			});
		}

		[Fact]
		public void Including_a_header_inserts_an_include_line_after_the_existing_ones()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				String headers = harness.Folder.Combine("headers");
				harness.Settings.Update(settings => settings.Paths.HeadersDirectory = headers);
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", "#include \"a.h\"\r\n\r\nprocedure start;\r\n\r\nprocedure start begin\r\nend\r\n");
				FakeEditorSurface editor = harness.AttachEditor(document);
				await harness.Workspace.RequestParseAsync(document, true);
				harness.ToolWindows.HeaderAnswer = System.IO.Path.Combine(headers, "sub", "b.h");

				await harness.Main.IncludeFileToCodeAsync();

				Assert.StartsWith("#include \"a.h\"\r\n#include \"sub/b.h\"\r\n", document.TextDocument.Text, StringComparison.Ordinal);
				Assert.Equal(2, editor.LastGoTo!.Value.Line);
			});
		}

		[Fact]
		public void Headers_chosen_with_Ctrl_are_included_while_the_picker_stays_open()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				String headers = harness.Folder.Combine("headers");
				harness.Settings.Update(settings => settings.Paths.HeadersDirectory = headers);
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", "procedure start;\r\n\r\nprocedure start begin\r\nend\r\n");
				harness.AttachEditor(document);
				await harness.Workspace.RequestParseAsync(document, true);
				harness.ToolWindows.KeepOpenHeaders.Add(System.IO.Path.Combine(headers, "a.h"));
				harness.ToolWindows.KeepOpenHeaders.Add(System.IO.Path.Combine(headers, "b.h"));
				harness.ToolWindows.HeaderAnswer = System.IO.Path.Combine(headers, "c.h");

				await harness.Main.IncludeFileToCodeAsync();

				String text = document.TextDocument.Text;
				Assert.Contains("#include \"a.h\"", text, StringComparison.Ordinal);
				Assert.Contains("#include \"b.h\"", text, StringComparison.Ordinal);
				Assert.Contains("#include \"c.h\"", text, StringComparison.Ordinal);
				Assert.Single(harness.ToolWindows.Calls, call => call == "pick-header");
			});
		}

		[Fact]
		public void A_picker_that_was_only_used_with_Ctrl_includes_nothing_more_when_it_is_closed()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				String headers = harness.Folder.Combine("headers");
				harness.Settings.Update(settings => settings.Paths.HeadersDirectory = headers);
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", "procedure start;\r\n\r\nprocedure start begin\r\nend\r\n");
				harness.AttachEditor(document);
				await harness.Workspace.RequestParseAsync(document, true);
				harness.ToolWindows.KeepOpenHeaders.Add(System.IO.Path.Combine(headers, "a.h"));
				harness.ToolWindows.HeaderAnswer = null;

				await harness.Main.IncludeFileToCodeAsync();

				Assert.Single(System.Text.RegularExpressions.Regex.Matches(document.TextDocument.Text, "#include"));
			});
		}

		[Fact]
		public void Headers_chosen_with_Ctrl_are_opened_while_the_picker_stays_open()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				String one = harness.Folder.Write("headers/one.h", "#define ONE 1\r\n");
				String two = harness.Folder.Write("headers/two.h", "#define TWO 2\r\n");
				harness.ToolWindows.KeepOpenHeaders.Add(one);
				harness.ToolWindows.HeaderAnswer = two;

				await harness.Main.BrowseHeadersAsync();

				Assert.Equal(2, harness.Workspace.Documents.Count);
			});
		}

		[Fact]
		public void Help_opens_the_sfall_documentation_site_in_the_default_browser()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();

				await harness.Main.HelpAsync();

				Assert.Equal("https://sfall-team.github.io/sfall/", MainViewModel.DocumentationUrl);
				Assert.Equal(MainViewModel.DocumentationUrl, Assert.Single(harness.Shell.Opened));
				Assert.Empty(harness.Dialogs.Messages);
			});
		}

		[Fact]
		public void Help_tells_the_user_the_address_when_no_browser_could_be_started()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Shell.OpenWithShellResult = false;

				await harness.Main.HelpAsync();

				String message = Assert.Single(harness.Dialogs.Messages);
				Assert.Contains(MainViewModel.DocumentationUrl, message, StringComparison.Ordinal);
			});
		}

		[Fact]
		public void All_includes_of_the_script_are_opened_in_the_background()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Folder.Write("scripts/one.h", "#define ONE 1\r\n");
				harness.Folder.Write("scripts/two.h", "#define TWO 2\r\n");
				DocumentViewModel document = await harness.OpenFileAsync("scripts/a.ssl", "#include \"one.h\"\r\n#include \"two.h\"\r\n#include \"missing.h\"\r\n");

				await harness.Main.OpenAllIncludesAsync();

				Assert.Equal(3, harness.Workspace.Documents.Count);
				Assert.Same(document, harness.Workspace.ActiveDocument);
			});
		}

		[Fact]
		public void The_message_editor_command_opens_the_associated_file_of_a_saved_script()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("scripts/npc.ssl", Script);
				harness.Folder.Write("scripts/npc.msg", "{100}{}{Hi}\r\n");

				await harness.Main.OpenMessageEditorAsync();

				Assert.Equal(document.FilePath, Assert.Single(harness.MessageEditor.Opened));
				Assert.NotNull(Assert.Single(harness.MessageEditor.Targets));
			});
		}

		[Fact]
		public void Without_a_script_the_message_editor_opens_empty()
		{
			using ShellHarness harness = new ShellHarness();

			harness.Main.OpenMessageEditorCommand.Execute(null);

			Assert.Null(Assert.Single(harness.MessageEditor.Opened));
		}

		[Fact]
		public void The_flowchart_needs_an_associated_message_file()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				await harness.OpenFileAsync("scripts/npc.ssl", Script);

				await harness.Main.ShowFlowchartAsync();
				Assert.Empty(harness.ScriptTools.Calls);

				harness.Folder.Write("scripts/npc.msg", "{100}{}{Hi}\r\n");
				await harness.Main.ShowFlowchartAsync();
				await harness.Main.ShowDialoguePreviewAsync();
				Assert.Equal(new[] { "flowchart:npc.ssl", "preview:npc.ssl" }, harness.ScriptTools.Calls);
			});
		}

		[Fact]
		public void Compile_commands_reach_the_compiler()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				harness.Settings.Update(settings => settings.Compiler.CompileToSourceDirectory = true);
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);

				await harness.Main.CompileAsync();
				await harness.Main.CompileAllOpenAsync();

				Assert.Equal(new[] { document.FilePath, document.FilePath }, harness.Compiler.Compiled);
			});
		}

		[Fact]
		public void Updating_the_parser_data_parses_immediately()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				DocumentViewModel document = await harness.OpenFileAsync("a.ssl", Script);
				Int32 before = harness.Parser.Started;

				await harness.Main.UpdateParserDataAsync();

				Assert.True(harness.Parser.Started > before);
				Assert.NotNull(document.ProgramInfo);
			});
		}

		[Fact]
		public void The_project_folder_command_reports_a_missing_folder_and_opens_an_existing_one()
		{
			ShellStaRunner.Run(async () =>
			{
				using ShellHarness harness = new ShellHarness();
				await harness.Main.OpenProjectFolderAsync();
				Assert.Single(harness.Dialogs.Messages);

				harness.Settings.Update(settings => settings.Paths.SolutionFolder = harness.Folder.Path);
				await harness.Main.OpenProjectFolderAsync();
				Assert.Equal(harness.Folder.Path, Assert.Single(harness.Shell.Folders));
				Assert.Equal("Project: " + harness.Folder.Path, harness.Main.ProjectText);
			});
		}

		[Fact]
		public void The_about_command_opens_the_about_window()
		{
			using ShellHarness harness = new ShellHarness();

			harness.Main.AboutCommand.Execute(null);

			Assert.Contains("about", harness.ToolWindows.Calls);
		}
	}
}
