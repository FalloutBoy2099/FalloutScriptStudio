namespace FalloutScriptStudio.Core.Tests.Configuration
{
	using System;
	using System.IO;
	using System.Text;
	using FalloutScriptStudio.Core.Configuration;
	using FalloutScriptStudio.Core.Formatting;
	using FalloutScriptStudio.Core.Tests.Compilation;
	using FalloutScriptStudio.TestSupport;
	using Microsoft.Extensions.Logging.Abstractions;
	using Xunit;

	/// <summary>The style of the formatter as a setting: defaults, the conversion to options, persistence and the per project override.</summary>
	public sealed class FormatSettingsTests : IDisposable
	{
		private readonly TempWorkspace _workspace = new TempWorkspace();

		public void Dispose()
		{
			this._workspace.Dispose();
		}

		private SettingsService CreateService()
		{
			return new SettingsService(
				AppPaths.FromBaseDirectory(this._workspace.Root),
				new LegacySettingsImporter(NullLogger<LegacySettingsImporter>.Instance),
				NullLogger<SettingsService>.Instance);
		}

		private static FormatSettings CreateStyle()
		{
			return new FormatSettings
			{
				Indentation = IndentStyle.Tabs,
				IndentSize = 4,
				Reindent = true,
				ContinuationIndent = 2,
				AlignContinuationToBracket = true,
				Begin = BeginPlacement.NextLine,
				ElseBegin = ElseBeginStyle.Split,
				Else = ElsePlacement.OwnLine,
				OperatorSpacing = SpacingStyle.Remove,
				AssignmentSpacing = SpacingStyle.Keep,
				CommaSpacing = SpacingStyle.Remove,
				ParenthesisSpacing = InnerSpacingStyle.One,
				FormatPreprocessorLines = false,
				BlankLinesBetweenProcedures = 2,
				MaxConsecutiveBlankLines = 3,
				TrimTrailingWhitespace = true,
				EnsureFinalNewline = true,
				AlignDefines = true,
				MaxLineLength = 100,
				FormatOnSave = true,
			};
		}

		[Fact]
		public void TheDefaults_ReproduceTheOriginalFormatter()
		{
			FormatSettings style = new FormatSettings();

			Assert.Equal(FormatOptions.Default, style.ToOptions(new EditorSettings()));
			Assert.False(style.FormatOnSave);
			Assert.False(style.ToOptions(new EditorSettings()).IsStructural);
		}

		[Fact]
		public void TheIndentationFollowsTheEditorUnlessTheStyleChoosesItsOwn()
		{
			EditorSettings editor = new EditorSettings { TabsToSpaces = false, TabSize = 6 };

			FormatOptions fromEditor = new FormatSettings().ToOptions(editor);
			FormatOptions spaces = new FormatSettings { Indentation = IndentStyle.Spaces, IndentSize = 2 }.ToOptions(editor);
			FormatOptions tabs = new FormatSettings { Indentation = IndentStyle.Tabs }.ToOptions(new EditorSettings { TabsToSpaces = true, TabSize = 5 });

			Assert.True(fromEditor.UseTabs);
			Assert.Equal(6, fromEditor.IndentSize);
			Assert.False(spaces.UseTabs);
			Assert.Equal(2, spaces.IndentSize);
			Assert.True(tabs.UseTabs);
			Assert.Equal(5, tabs.IndentSize);
		}

		[Fact]
		public void EveryOptionReachesTheFormatOptions()
		{
			FormatOptions options = CreateStyle().ToOptions(new EditorSettings());

			Assert.True(options.UseTabs);
			Assert.Equal(4, options.IndentSize);
			Assert.True(options.Reindent);
			Assert.Equal(2, options.ContinuationIndent);
			Assert.True(options.AlignContinuationToBracket);
			Assert.Equal(BeginPlacement.NextLine, options.Begin);
			Assert.Equal(ElseBeginStyle.Split, options.ElseBegin);
			Assert.Equal(ElsePlacement.OwnLine, options.Else);
			Assert.Equal(SpacingStyle.Remove, options.OperatorSpacing);
			Assert.Equal(SpacingStyle.Keep, options.AssignmentSpacing);
			Assert.Equal(SpacingStyle.Remove, options.CommaSpacing);
			Assert.Equal(InnerSpacingStyle.One, options.ParenthesisSpacing);
			Assert.False(options.FormatPreprocessorLines);
			Assert.Equal(2, options.BlankLinesBetweenProcedures);
			Assert.Equal(3, options.MaxConsecutiveBlankLines);
			Assert.True(options.TrimTrailingWhitespace);
			Assert.True(options.EnsureFinalNewline);
			Assert.True(options.AlignDefines);
			Assert.Equal(100, options.MaxLineLength);
		}

		[Fact]
		public void Normalize_RepairsOutOfRangeValues()
		{
			FormatSettings style = new FormatSettings
			{
				IndentSize = 99,
				ContinuationIndent = 9,
				BlankLinesBetweenProcedures = 9,
				MaxConsecutiveBlankLines = 9,
				MaxLineLength = 3,
				Begin = (BeginPlacement)42,
				OperatorSpacing = (SpacingStyle)42,
				Indentation = (IndentStyle)42,
			};

			style.Normalize();

			Assert.Equal(FormatOptions.MaxIndentSize, style.IndentSize);
			Assert.Equal(4, style.ContinuationIndent);
			Assert.Equal(FormatOptions.MaxBlankLines, style.BlankLinesBetweenProcedures);
			Assert.Equal(FormatOptions.MaxBlankLines, style.MaxConsecutiveBlankLines);
			Assert.Equal(FormatOptions.MinLineLength, style.MaxLineLength);
			Assert.Equal(BeginPlacement.Keep, style.Begin);
			Assert.Equal(SpacingStyle.Add, style.OperatorSpacing);
			Assert.Equal(IndentStyle.FromEditor, style.Indentation);
		}

		[Fact]
		public void TheStyleIsSavedAndLoadedWithTheSettings()
		{
			SettingsService first = this.CreateService();
			first.Settings.Editor.Formatting = CreateStyle();
			first.Save();

			SettingsService second = this.CreateService();
			second.Load();

			FormatSettings loaded = second.Settings.Editor.Formatting;
			Assert.Equal(IndentStyle.Tabs, loaded.Indentation);
			Assert.Equal(BeginPlacement.NextLine, loaded.Begin);
			Assert.Equal(SpacingStyle.Remove, loaded.OperatorSpacing);
			Assert.Equal(InnerSpacingStyle.One, loaded.ParenthesisSpacing);
			Assert.Equal(2, loaded.BlankLinesBetweenProcedures);
			Assert.Equal(100, loaded.MaxLineLength);
			Assert.True(loaded.FormatOnSave);
			Assert.Equal(first.Settings.Editor.Formatting.ToOptions(first.Settings.Editor), loaded.ToOptions(second.Settings.Editor));
		}

		[Fact]
		public void ASettingsFileWithoutTheStyle_GetsTheDefaults_AndUnknownPropertiesAreIgnored()
		{
			AppPaths paths = AppPaths.FromBaseDirectory(this._workspace.Root);
			Directory.CreateDirectory(paths.SettingsDirectory);
			File.WriteAllText(paths.SettingsFile, "{ \"version\": 1, \"isFirstRunCompleted\": true, \"editor\": { \"tabSize\": 5, \"formatting\": { \"reindent\": true, \"somethingNew\": 3 } } }", new UTF8Encoding(false));

			SettingsService service = this.CreateService();
			service.Load();

			Assert.Equal(5, service.Settings.Editor.TabSize);
			Assert.True(service.Settings.Editor.Formatting.Reindent);
			Assert.Equal(SpacingStyle.Add, service.Settings.Editor.Formatting.OperatorSpacing);
			Assert.Equal(FormatOptions.KeepBlankLines, service.Settings.Editor.Formatting.BlankLinesBetweenProcedures);
		}

		[Fact]
		public void AnOptionOfANewerVersion_IsKeptWhenTheSettingsAreSavedAndWhenTheStyleIsCopied()
		{
			AppPaths paths = AppPaths.FromBaseDirectory(this._workspace.Root);
			Directory.CreateDirectory(paths.SettingsDirectory);
			File.WriteAllText(paths.SettingsFile, "{ \"version\": 1, \"isFirstRunCompleted\": true, \"editor\": { \"formatting\": { \"reindent\": true, \"somethingNew\": 3 } } }", new UTF8Encoding(false));
			SettingsService service = this.CreateService();
			service.Load();

			service.Settings.Editor.Formatting.Clone().CopyFrom(service.Settings.Editor.Formatting);
			FormatSettings copy = new FormatSettings();
			copy.CopyFrom(service.Settings.Editor.Formatting);
			service.Save();

			Assert.Contains("somethingNew", File.ReadAllText(paths.SettingsFile), StringComparison.Ordinal);
			Assert.NotNull(copy.UnknownProperties);
			Assert.Equal(3, copy.UnknownProperties!["somethingNew"].GetInt32());
			Assert.NotSame(service.Settings.Editor.Formatting.UnknownProperties, copy.UnknownProperties);
		}

		[Fact]
		public void ACopyOfTheSettings_DoesNotShareTheStyle()
		{
			AppSettings settings = new AppSettings();
			AppSettings copy = settings.Clone();

			copy.Editor.Formatting.Reindent = true;

			Assert.False(settings.Editor.Formatting.Reindent);
		}

		[Fact]
		public void TheEffectiveStyle_IsTheOneOfTheProject_WhenItHasItsOwn()
		{
			AppSettings global = new AppSettings();
			global.Editor.Formatting.Reindent = true;
			global.Editor.TabsToSpaces = false;
			ProjectSettings project = new ProjectSettings { Formatting = new FormatSettings { Begin = BeginPlacement.SameLine } };

			EffectiveSettings inherited = new EffectiveSettings(global, new PreprocessorDefines(), @"C:\mod", new ProjectSettings { Name = "Mod" });
			EffectiveSettings overridden = new EffectiveSettings(global, new PreprocessorDefines(), @"C:\mod", project);

			Assert.Same(global.Editor.Formatting, inherited.Formatting);
			Assert.True(inherited.ToFormatOptions().Reindent);
			Assert.Same(project.Formatting, overridden.Formatting);
			Assert.False(overridden.ToFormatOptions().Reindent);
			Assert.Equal(BeginPlacement.SameLine, overridden.ToFormatOptions().Begin);
			Assert.True(overridden.ToFormatOptions().UseTabs);
			Assert.Contains(ProjectOptions.Formatting, overridden.GetOverriddenOptions());
			Assert.DoesNotContain(ProjectOptions.Formatting, inherited.GetOverriddenOptions());
		}

		[Fact]
		public void TheProjectFileKeepsTheStyle_AndAnOlderFileHasNone()
		{
			using TestFolder folder = new TestFolder("project-format");
			ProjectSettings settings = new ProjectSettings { Name = "Mod", Formatting = CreateStyle() };

			Byte[] content = ProjectSettingsFile.Serialize(folder.Path, settings);
			Boolean valid = ProjectSettingsFile.TryParse(content, folder.Path, out ProjectSettings? loaded, out _);
			Boolean oldValid = ProjectSettingsFile.TryParse(Encoding.UTF8.GetBytes("{ \"name\": \"Old\" }"), folder.Path, out ProjectSettings? old, out _);

			Assert.True(valid);
			Assert.Equal(CreateStyle().ToOptions(new EditorSettings()), loaded!.Formatting!.ToOptions(new EditorSettings()));
			Assert.True(loaded.Formatting.FormatOnSave);
			Assert.True(oldValid);
			Assert.Null(old!.Formatting);
			Assert.Contains("\"formatting\"", Encoding.UTF8.GetString(content), StringComparison.Ordinal);
			Assert.DoesNotContain("\"formatting\"", Encoding.UTF8.GetString(ProjectSettingsFile.Serialize(folder.Path, new ProjectSettings { Name = "Plain" })), StringComparison.Ordinal);
		}

		[Fact]
		public void ACloneOfTheProjectSettings_DoesNotShareTheStyle()
		{
			ProjectSettings settings = new ProjectSettings { Formatting = new FormatSettings() };
			ProjectSettings copy = settings.Clone();

			copy.Formatting!.Reindent = true;

			Assert.False(settings.Formatting.Reindent);
		}
	}
}